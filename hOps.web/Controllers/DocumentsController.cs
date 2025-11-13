using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class DocumentsController : BaseController
    {
        private const string LastFolderCookieName = "hops_last_document_folder";
        private const int FolderPreferenceExpiryDays = 30;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<DocumentsController> logger) : base(context, userManager)
        {
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? folderId = null, string? sort = null, string? direction = null, string? search = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var (sortField, sortDirection) = NormalizeSort(sort, direction);
            var searchQuery = NormalizeSearch(search);

            var effectiveFolderId = folderId;
            if (!effectiveFolderId.HasValue && Request.Cookies.TryGetValue(LastFolderCookieName, out var storedFolder))
            {
                if (string.Equals(storedFolder, "-1", StringComparison.Ordinal))
                {
                    effectiveFolderId = -1;
                }
                else if (int.TryParse(storedFolder, out var parsedFolderId))
                {
                    effectiveFolderId = parsedFolderId;
                }
            }

            var viewModel = await BuildIndexViewModelAsync(user, null, null, effectiveFolderId, sortField, sortDirection, searchQuery);
            PersistFolderPreference(viewModel.SelectedFolderId, viewModel.ShowingUnassignedOnly);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload([Bind(Prefix = "Form")] DocumentUploadFormViewModel form, int? currentFolderId, string? currentSort, string? currentDirection, string? currentSearch)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var (sortField, sortDirection) = NormalizeSort(currentSort, currentDirection);
            var searchQuery = NormalizeSearch(currentSearch);

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessiblePropertySet = accessiblePropertyIds.ToHashSet();

            if (form.File == null || form.File.Length == 0)
            {
                ModelState.AddModelError(nameof(form.File), "Please select a file to upload.");
            }

            form.Title = string.IsNullOrWhiteSpace(form.Title) ? null : form.Title.Trim();
            form.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();

            switch (form.AccessScope)
            {
                case DocumentAccessScope.PropertyOnly:
                    if (!form.PropertyId.HasValue)
                    {
                        ModelState.AddModelError(nameof(form.PropertyId), "Select the property this document belongs to.");
                    }
                    else if (!accessiblePropertySet.Contains(form.PropertyId.Value))
                    {
                        ModelState.AddModelError(nameof(form.PropertyId), "You do not have access to the selected property.");
                    }
                    form.SelectedPropertyIds = new List<int>();
                    break;

                case DocumentAccessScope.SelectedProperties:
                    form.SelectedPropertyIds = form.SelectedPropertyIds?.Distinct().ToList() ?? new List<int>();
                    if (!form.SelectedPropertyIds.Any())
                    {
                        ModelState.AddModelError(nameof(form.SelectedPropertyIds), "Select at least one property.");
                    }
                    else if (form.SelectedPropertyIds.Any(id => !accessiblePropertySet.Contains(id)))
                    {
                        ModelState.AddModelError(nameof(form.SelectedPropertyIds), "You do not have access to one or more selected properties.");
                    }
                    break;

                case DocumentAccessScope.AllUsers:
                    form.SelectedPropertyIds = new List<int>();
                    form.PropertyId = null;
                    break;

                default:
                    ModelState.AddModelError(nameof(form.AccessScope), "Invalid access option.");
                    break;
            }

            if (form.FolderId.HasValue)
            {
                var folderExists = await _context.DocumentFolders.AnyAsync(f => f.Id == form.FolderId.Value);
                if (!folderExists)
                {
                    ModelState.AddModelError(nameof(form.FolderId), "Selected folder was not found.");
                    form.FolderId = null;
                }
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildIndexViewModelAsync(
                    user,
                    form,
                    null,
                    currentFolderId ?? form.FolderId,
                    sortField,
                    sortDirection,
                    searchQuery);
                return View("Index", invalidModel);
            }

            var uploadsDirectory = Path.Combine(_environment.WebRootPath, "uploads", "documents");
            Directory.CreateDirectory(uploadsDirectory);

            var extension = Path.GetExtension(form.File!.FileName);
            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var storedPath = Path.Combine(uploadsDirectory, storedFileName);

            await using (var stream = System.IO.File.Create(storedPath))
            {
                await form.File.CopyToAsync(stream);
            }

            var relativePath = Path.Combine("/uploads/documents", storedFileName).Replace("\\", "/");

            var document = new Document
            {
                Title = form.Title,
                Description = form.Description,
                FilePath = relativePath,
                OriginalFileName = form.File.FileName,
                ContentType = string.IsNullOrWhiteSpace(form.File.ContentType) ? null : form.File.ContentType,
                FileSizeBytes = form.File.Length,
                FolderId = form.FolderId,
                AccessScope = form.AccessScope,
                PropertyId = form.AccessScope == DocumentAccessScope.PropertyOnly ? form.PropertyId : null,
                UploadedById = user.Id,
                UploadedAtUtc = DateTime.UtcNow
            };

            if (form.AccessScope == DocumentAccessScope.SelectedProperties)
            {
                foreach (var propertyId in form.SelectedPropertyIds.Distinct())
                {
                    document.DocumentProperties.Add(new DocumentProperty
                    {
                        PropertyId = propertyId
                    });
                }
            }

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            TempData["DocumentSuccess"] = "Document uploaded successfully.";
            if (form.FolderId.HasValue)
            {
                return RedirectToAction(nameof(Index), new { folderId = form.FolderId.Value, sort = sortField, direction = sortDirection, search = searchQuery });
            }

            if (currentFolderId.HasValue && currentFolderId.Value == -1)
            {
                return RedirectToAction(nameof(Index), new { folderId = -1, sort = sortField, direction = sortDirection, search = searchQuery });
            }

            return RedirectToAction(nameof(Index), new { sort = sortField, direction = sortDirection, search = searchQuery });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Move(int id, int? folderId, int? currentFolderId, string? currentSort, string? currentDirection, string? currentSearch)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var (sortField, sortDirection) = NormalizeSort(currentSort, currentDirection);
            var searchQuery = NormalizeSearch(currentSearch);

            var document = await _context.Documents
                .Include(d => d.DocumentProperties)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var (accessibleFolders, accessibleFolderIds) = await GetAccessibleFoldersAsync(accessiblePropertyIds);

            if (!UserCanAccessDocument(document, accessiblePropertyIds, accessibleFolderIds))
            {
                return Forbid();
            }

            if (folderId.HasValue)
            {
                if (!accessibleFolderIds.Contains(folderId.Value))
                {
                    return Forbid();
                }

                var folderExists = accessibleFolders.Any(f => f.Id == folderId.Value);
                if (!folderExists)
                {
                    TempData["DocumentError"] = "Selected folder was not found.";
                    return RedirectToAction(nameof(Index), new { folderId = currentFolderId, sort = sortField, direction = sortDirection, search = searchQuery });
                }
            }

            document.FolderId = folderId;
            await _context.SaveChangesAsync();

            TempData["DocumentSuccess"] = folderId.HasValue
                ? "Document moved successfully."
                : "Document removed from folder.";

            return RedirectToAction(nameof(Index), new { folderId = currentFolderId, sort = sortField, direction = sortDirection, search = searchQuery });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFolder([Bind(Prefix = "FolderForm")] DocumentFolderFormViewModel form, int? currentFolderId, string? currentSort, string? currentDirection, string? currentSearch)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            form.Name = form.Name?.Trim() ?? string.Empty;

            DocumentFolder? parentFolder = null;
            if (form.ParentFolderId.HasValue)
            {
                parentFolder = await _context.DocumentFolders
                    .Include(f => f.FolderProperties)
                    .FirstOrDefaultAsync(f => f.Id == form.ParentFolderId.Value);
                if (parentFolder == null)
                {
                    ModelState.AddModelError(nameof(form.ParentFolderId), "Selected parent folder was not found.");
                }
            }

            if (string.IsNullOrWhiteSpace(form.Name))
            {
                ModelState.AddModelError(nameof(form.Name), "Folder name is required.");
            }

            var (sortField, sortDirection) = NormalizeSort(currentSort, currentDirection);
            var searchQuery = NormalizeSearch(currentSearch);
            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessiblePropertySet = accessiblePropertyIds.ToHashSet();

            form.SelectedPropertyIds = form.SelectedPropertyIds?.Distinct().ToList() ?? new List<int>();

            if (form.Visibility == DocumentFolderVisibility.SelectedProperties)
            {
                if (form.SelectedPropertyIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(form.SelectedPropertyIds), "Select at least one property for this folder.");
                }
                else if (form.SelectedPropertyIds.Any(id => !accessiblePropertySet.Contains(id)))
                {
                    ModelState.AddModelError(nameof(form.SelectedPropertyIds), "You do not have access to one or more selected properties.");
                }
            }
            else
            {
                form.SelectedPropertyIds = new List<int>();
            }

            if (!ModelState.IsValid)
            {
                var invalidViewModel = await BuildIndexViewModelAsync(user, null, form, currentFolderId, sortField, sortDirection, searchQuery);
                return View("Index", invalidViewModel);
            }

            var folder = new DocumentFolder
            {
                Name = form.Name,
                ParentFolderId = form.ParentFolderId,
                Visibility = form.Visibility,
                CreatedById = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            if (form.Visibility == DocumentFolderVisibility.SelectedProperties)
            {
                foreach (var propertyId in form.SelectedPropertyIds)
                {
                    folder.FolderProperties.Add(new DocumentFolderProperty
                    {
                        PropertyId = propertyId
                    });
                }
            }

            _context.DocumentFolders.Add(folder);
            await _context.SaveChangesAsync();

            TempData["DocumentSuccess"] = "Folder created successfully.";
            return RedirectToAction(nameof(Index), new { folderId = folder.Id, sort = sortField, direction = sortDirection, search = searchQuery });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFolderSettings([Bind(Prefix = "FolderSettingsForm")] DocumentFolderSettingsForm form, int? currentFolderId, string? currentSort, string? currentDirection, string? currentSearch)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var (sortField, sortDirection) = NormalizeSort(currentSort, currentDirection);
            var searchQuery = NormalizeSearch(currentSearch);
            form.Name = form.Name?.Trim() ?? string.Empty;

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessiblePropertySet = accessiblePropertyIds.ToHashSet();
            var (_, accessibleFolderIds) = await GetAccessibleFoldersAsync(accessiblePropertySet);

            var folder = await _context.DocumentFolders
                .Include(f => f.FolderProperties)
                .Include(f => f.Documents)
                    .ThenInclude(d => d.DocumentProperties)
                .FirstOrDefaultAsync(f => f.Id == form.FolderId);

            if (folder == null)
            {
                return NotFound();
            }

            if (!accessibleFolderIds.Contains(folder.Id))
            {
                return Forbid();
            }

            form.SelectedPropertyIds = form.SelectedPropertyIds?
                .Where(accessiblePropertySet.Contains)
                .Distinct()
                .ToList() ?? new List<int>();

            if (string.IsNullOrWhiteSpace(form.Name))
            {
                ModelState.AddModelError(nameof(form.Name), "Folder name is required.");
            }

            if (form.Visibility == DocumentFolderVisibility.SelectedProperties)
            {
                if (!form.SelectedPropertyIds.Any())
                {
                    ModelState.AddModelError(nameof(form.SelectedPropertyIds), "Select at least one property for this folder.");
                }
            }
            else
            {
                form.SelectedPropertyIds.Clear();
            }

            if (!ModelState.IsValid)
            {
                var invalidViewModel = await BuildIndexViewModelAsync(
                    user,
                    null,
                    null,
                    currentFolderId ?? form.FolderId,
                    sortField,
                    sortDirection,
                    searchQuery,
                    form);
                return View("Index", invalidViewModel);
            }

            folder.Name = form.Name;
            folder.Visibility = form.Visibility;
            folder.FolderProperties.Clear();

            foreach (var propertyId in form.SelectedPropertyIds)
            {
                folder.FolderProperties.Add(new DocumentFolderProperty
                {
                    DocumentFolderId = folder.Id,
                    PropertyId = propertyId
                });
            }

            if (form.ApplyToDocuments)
            {
                var folderDocuments = await _context.Documents
                    .Where(d => d.FolderId == folder.Id)
                    .Include(d => d.DocumentProperties)
                    .ToListAsync();

                foreach (var document in folderDocuments)
                {
                    ApplyFolderVisibilityToDocument(document, form);
                }
            }

            await _context.SaveChangesAsync();

            TempData["DocumentSuccess"] = "Folder settings updated.";
            var targetFolderId = currentFolderId ?? form.FolderId;
            return RedirectToAction(nameof(Index), new { folderId = targetFolderId, sort = sortField, direction = sortDirection, search = searchQuery });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDocumentVisibility(DocumentVisibilityForm form, int? currentFolderId, string? currentSort, string? currentDirection, string? currentSearch)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var (sortField, sortDirection) = NormalizeSort(currentSort, currentDirection);
            var searchQuery = NormalizeSearch(currentSearch);

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessiblePropertySet = accessiblePropertyIds.ToHashSet();
            var accessibleFolderIds = (await GetAccessibleFoldersAsync(accessiblePropertySet)).AccessibleFolderIds;

            var document = await _context.Documents
                .Include(d => d.DocumentProperties)
                .Include(d => d.Folder)
                .FirstOrDefaultAsync(d => d.Id == form.DocumentId);

            if (document == null)
            {
                return NotFound();
            }

            if (!UserCanAccessDocument(document, accessiblePropertySet, accessibleFolderIds))
            {
                return Forbid();
            }

            form.SelectedPropertyIds = form.SelectedPropertyIds?
                .Where(accessiblePropertySet.Contains)
                .Distinct()
                .ToList() ?? new List<int>();

            switch (form.AccessScope)
            {
                case DocumentAccessScope.AllUsers:
                    form.SelectedPropertyIds.Clear();
                    form.PropertyId = null;
                    break;
                case DocumentAccessScope.PropertyOnly:
                    if (!form.PropertyId.HasValue || !accessiblePropertySet.Contains(form.PropertyId.Value))
                    {
                        ModelState.AddModelError(nameof(form.PropertyId), "Select a valid property.");
                    }
                    form.SelectedPropertyIds.Clear();
                    break;
                case DocumentAccessScope.SelectedProperties:
                    if (!form.SelectedPropertyIds.Any())
                    {
                        ModelState.AddModelError(nameof(form.SelectedPropertyIds), "Select at least one property.");
                    }
                    form.PropertyId = null;
                    break;
                default:
                    ModelState.AddModelError(nameof(form.AccessScope), "Invalid visibility option.");
                    break;
            }

            if (!ModelState.IsValid)
            {
                TempData["DocumentError"] = "Unable to update document visibility.";
                return RedirectToAction(nameof(Index), new { folderId = currentFolderId, sort = sortField, direction = sortDirection, search = searchQuery });
            }

            if (form.AccessScope == DocumentAccessScope.AllUsers)
            {
                ApplyDocumentVisibility(document, DocumentAccessScope.AllUsers, null, Array.Empty<int>());
            }
            else if (form.AccessScope == DocumentAccessScope.PropertyOnly)
            {
                ApplyDocumentVisibility(document, DocumentAccessScope.PropertyOnly, form.PropertyId, Array.Empty<int>());
            }
            else
            {
                ApplyDocumentVisibility(document, DocumentAccessScope.SelectedProperties, null, form.SelectedPropertyIds);
            }

            await _context.SaveChangesAsync();

            TempData["DocumentSuccess"] = "Document visibility updated.";
            return RedirectToAction(nameof(Index), new { folderId = currentFolderId, sort = sortField, direction = sortDirection, search = searchQuery });
        }

        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var document = await _context.Documents
                .Include(d => d.DocumentProperties)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessibleFolderIds = (await GetAccessibleFoldersAsync(accessiblePropertyIds)).AccessibleFolderIds;
            if (!UserCanAccessDocument(document, accessiblePropertyIds, accessibleFolderIds))
            {
                return Forbid();
            }

            var normalizedPath = document.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var physicalPath = Path.Combine(_environment.WebRootPath, normalizedPath);

            if (!System.IO.File.Exists(physicalPath))
            {
                _logger.LogWarning("Requested document file was not found. DocumentId={DocumentId}, Path={Path}", document.Id, physicalPath);
                return NotFound();
            }

            var downloadName = string.IsNullOrWhiteSpace(document.OriginalFileName)
                ? Path.GetFileName(physicalPath)
                : document.OriginalFileName;

            var contentType = string.IsNullOrWhiteSpace(document.ContentType)
                ? "application/octet-stream"
                : document.ContentType;

            return PhysicalFile(physicalPath, contentType, downloadName);
        }

        private async Task<DocumentsIndexViewModel> BuildIndexViewModelAsync(
            ApplicationUser user,
            DocumentUploadFormViewModel? uploadForm,
            DocumentFolderFormViewModel? folderForm,
            int? selectedFolderId,
            string sortField,
            string sortDirection,
            string? searchQuery,
            DocumentFolderSettingsForm? folderSettingsForm = null)
        {
            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessiblePropertySet = accessiblePropertyIds.ToHashSet();

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty != null && !accessiblePropertySet.Contains(currentProperty.Id))
            {
                currentProperty = null;
            }

            var (folderEntities, accessibleFolderIds) = await GetAccessibleFoldersAsync(accessiblePropertySet);
            var normalizedSearch = NormalizeSearch(searchQuery);
            var folderLookup = folderEntities.ToDictionary(f => f.Id);
            var showUnassignedOnly = selectedFolderId.HasValue && selectedFolderId.Value == -1;
            var actualSelectedFolderId = showUnassignedOnly ? (int?)null : selectedFolderId;

            if (actualSelectedFolderId.HasValue && !accessibleFolderIds.Contains(actualSelectedFolderId.Value))
            {
                actualSelectedFolderId = null;
            }

            var effectiveFolderForm = folderForm == null
                ? new DocumentFolderFormViewModel
                {
                    ParentFolderId = actualSelectedFolderId,
                    Visibility = DocumentFolderVisibility.Global,
                    SelectedPropertyIds = new List<int>()
                }
                : new DocumentFolderFormViewModel
                {
                    Name = folderForm.Name,
                    ParentFolderId = folderForm.ParentFolderId,
                    Visibility = folderForm.Visibility,
                    SelectedPropertyIds = folderForm.SelectedPropertyIds?.Distinct().ToList() ?? new List<int>()
                };

            if (folderForm == null && actualSelectedFolderId.HasValue && folderLookup.TryGetValue(actualSelectedFolderId.Value, out var activeFolder))
            {
                effectiveFolderForm.Visibility = activeFolder.Visibility;
                if (activeFolder.Visibility == DocumentFolderVisibility.SelectedProperties)
                {
                    effectiveFolderForm.SelectedPropertyIds = activeFolder.FolderProperties
                        .Select(fp => fp.PropertyId)
                        .Where(accessiblePropertySet.Contains)
                        .Distinct()
                        .ToList();
                }
            }

            DocumentFolderSettingsForm? effectiveFolderSettings = null;
            if (folderSettingsForm != null)
            {
                effectiveFolderSettings = new DocumentFolderSettingsForm
                {
                    FolderId = folderSettingsForm.FolderId,
                    Name = folderSettingsForm.Name,
                    Visibility = folderSettingsForm.Visibility,
                    SelectedPropertyIds = folderSettingsForm.SelectedPropertyIds?
                        .Where(accessiblePropertySet.Contains)
                        .Distinct()
                        .ToList() ?? new List<int>(),
                    ApplyToDocuments = folderSettingsForm.ApplyToDocuments
                };
            }
            else if (actualSelectedFolderId.HasValue && folderLookup.TryGetValue(actualSelectedFolderId.Value, out var selectedFolder))
            {
                var selectedIds = selectedFolder.Visibility == DocumentFolderVisibility.SelectedProperties
                    ? selectedFolder.FolderProperties
                        .Select(fp => fp.PropertyId)
                        .Where(accessiblePropertySet.Contains)
                        .Distinct()
                        .ToList()
                    : new List<int>();

                effectiveFolderSettings = new DocumentFolderSettingsForm
                {
                    FolderId = selectedFolder.Id,
                    Name = selectedFolder.Name,
                    Visibility = selectedFolder.Visibility,
                    SelectedPropertyIds = selectedIds
                };
            }

            DocumentUploadFormViewModel effectiveUploadForm;
            if (uploadForm == null)
            {
                effectiveUploadForm = new DocumentUploadFormViewModel
                {
                    AccessScope = accessiblePropertySet.Count > 0 ? DocumentAccessScope.PropertyOnly : DocumentAccessScope.AllUsers,
                    PropertyId = currentProperty?.Id,
                    FolderId = actualSelectedFolderId,
                    Description = null
                };
                if (currentProperty != null)
                {
                    effectiveUploadForm.SelectedPropertyIds.Add(currentProperty.Id);
                }
            }
            else
            {
                effectiveUploadForm = new DocumentUploadFormViewModel
                {
                    Title = uploadForm.Title,
                    Description = uploadForm.Description,
                    AccessScope = uploadForm.AccessScope,
                    PropertyId = uploadForm.PropertyId,
                    SelectedPropertyIds = uploadForm.SelectedPropertyIds?.Distinct().ToList() ?? new List<int>(),
                    FolderId = uploadForm.FolderId
                };
            }

            if (effectiveUploadForm.FolderId.HasValue && !folderLookup.ContainsKey(effectiveUploadForm.FolderId.Value))
            {
                effectiveUploadForm.FolderId = actualSelectedFolderId;
            }
            else if (!effectiveUploadForm.FolderId.HasValue && actualSelectedFolderId.HasValue)
            {
                effectiveUploadForm.FolderId = actualSelectedFolderId;
            }

            if (showUnassignedOnly)
            {
                effectiveUploadForm.FolderId = null;
            }

            if (effectiveUploadForm.AccessScope == DocumentAccessScope.PropertyOnly && !effectiveUploadForm.PropertyId.HasValue && currentProperty != null)
            {
                effectiveUploadForm.PropertyId = currentProperty.Id;
            }

            if (effectiveUploadForm.AccessScope == DocumentAccessScope.SelectedProperties && effectiveUploadForm.SelectedPropertyIds.Count == 0 && currentProperty != null)
            {
                effectiveUploadForm.SelectedPropertyIds.Add(currentProperty.Id);
            }

            var propertyOptions = await _context.Properties
                .Where(p => accessiblePropertySet.Contains(p.Id))
                .OrderBy(p => p.Name)
                .Select(p => new DocumentPropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsSelected = effectiveUploadForm.SelectedPropertyIds.Contains(p.Id)
                })
                .ToListAsync();

            effectiveFolderForm.SelectedPropertyIds = effectiveFolderForm.SelectedPropertyIds
                .Where(accessiblePropertySet.Contains)
                .Distinct()
                .ToList();

            if (effectiveFolderForm.Visibility != DocumentFolderVisibility.SelectedProperties)
            {
                effectiveFolderForm.SelectedPropertyIds.Clear();
            }

            var folderPropertyOptions = propertyOptions
                .Select(option => new DocumentPropertyOptionViewModel
                {
                    Id = option.Id,
                    Name = option.Name,
                    Code = option.Code,
                    IsSelected = effectiveFolderForm.SelectedPropertyIds.Contains(option.Id)
                })
                .ToList();

            var folderSettingsPropertyOptions = propertyOptions
                .Select(option => new DocumentPropertyOptionViewModel
                {
                    Id = option.Id,
                    Name = option.Name,
                    Code = option.Code,
                    IsSelected = effectiveFolderSettings?.SelectedPropertyIds.Contains(option.Id) ?? false
                })
                .ToList();

            var documents = await _context.Documents
                .Include(d => d.UploadedBy)
                .Include(d => d.Property)
                .Include(d => d.Folder)
                .Include(d => d.DocumentProperties)
                    .ThenInclude(dp => dp.Property)
                .ToListAsync();

            var accessibleDocuments = documents
                .Where(d => UserCanAccessDocument(d, accessiblePropertySet, accessibleFolderIds))
                .ToList();

            var sortedDocuments = SortDocuments(accessibleDocuments, sortField, sortDirection);
            var folderPathMap = BuildFolderPathMap(folderEntities);

            var filteredDocuments = sortedDocuments
                .Where(d => showUnassignedOnly
                    ? !d.FolderId.HasValue
                    : !actualSelectedFolderId.HasValue || d.FolderId == actualSelectedFolderId.Value)
                .Select(d => ToListItemViewModel(d, folderPathMap))
                .ToList();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var lowered = normalizedSearch.ToLowerInvariant();
                filteredDocuments = filteredDocuments
                    .Where(d => DocumentMatchesSearch(d, lowered))
                    .ToList();
            }

            var folderTree = BuildFolderTree(folderEntities, accessibleDocuments, actualSelectedFolderId);
            var folderOptions = folderTree
                .Select(node => new DocumentFolderOptionViewModel
                {
                    Id = node.Id,
                    Name = node.Name,
                    Level = node.Level,
                    IsSelected = effectiveUploadForm.FolderId == node.Id
                })
                .ToList();

            var childFolders = BuildChildFolderList(folderEntities, accessibleDocuments, actualSelectedFolderId, showUnassignedOnly, folderPathMap);

            return new DocumentsIndexViewModel
            {
                Documents = filteredDocuments,
                Form = effectiveUploadForm,
                FolderOptions = folderOptions,
                FolderTree = folderTree,
                FolderForm = effectiveFolderForm,
                FolderSettingsForm = effectiveFolderSettings,
                FolderPropertyOptions = folderPropertyOptions,
                SelectedFolderPropertyOptions = folderSettingsPropertyOptions,
                ChildFolders = childFolders,
                SelectedFolderId = actualSelectedFolderId,
                ShowingUnassignedOnly = showUnassignedOnly,
                UnassignedDocumentCount = accessibleDocuments.Count(d => !d.FolderId.HasValue),
                TotalDocumentCount = accessibleDocuments.Count,
                PropertyOptions = propertyOptions,
                CurrentPropertyId = currentProperty?.Id,
                CurrentPropertyName = currentProperty?.Name,
                SortField = sortField,
                SortDirection = sortDirection,
                SearchQuery = normalizedSearch ?? string.Empty
            };
        }

        private static DocumentListItemViewModel ToListItemViewModel(Document document, IReadOnlyDictionary<int, string> folderPathMap)
        {
            var displayName = ResolveDocumentDisplayName(document);

            var uploaderName = FormatUserName(document.UploadedBy);

            var propertyLabels = document.AccessScope switch
            {
                DocumentAccessScope.AllUsers => Array.Empty<string>(),
                DocumentAccessScope.PropertyOnly when document.Property != null
                    => new[] { $"{document.Property.Name} ({document.Property.Code})" },
                DocumentAccessScope.SelectedProperties
                    => document.DocumentProperties
                        .Where(dp => dp.Property != null)
                        .Select(dp => $"{dp.Property!.Name} ({dp.Property!.Code})")
                        .Distinct()
                        .OrderBy(label => label)
                        .ToArray(),
                _ => Array.Empty<string>()
            };
            var propertyIds = document.AccessScope switch
            {
                DocumentAccessScope.PropertyOnly when document.PropertyId.HasValue
                    => new[] { document.PropertyId.Value },
                DocumentAccessScope.SelectedProperties
                    => document.DocumentProperties
                        .Select(dp => dp.PropertyId)
                        .Distinct()
                        .ToArray(),
                _ => Array.Empty<int>()
            };

            var accessSummary = document.AccessScope switch
            {
                DocumentAccessScope.AllUsers => "All users",
                DocumentAccessScope.PropertyOnly => document.Property != null
                    ? $"Users at {document.Property.Name}"
                    : "Users at selected property",
                DocumentAccessScope.SelectedProperties => propertyLabels.Length > 0
                    ? $"Users at selected properties ({propertyLabels.Length})"
                    : "Users at selected properties",
                _ => "Restricted"
            };

            string? folderPath = null;
            if (document.FolderId.HasValue && folderPathMap.TryGetValue(document.FolderId.Value, out var resolvedPath))
            {
                folderPath = resolvedPath;
            }

            return new DocumentListItemViewModel
            {
                Id = document.Id,
                DisplayName = displayName,
                OriginalFileName = document.OriginalFileName,
                FileSizeBytes = document.FileSizeBytes,
                FileSizeDisplay = FormatFileSize(document.FileSizeBytes),
                UploadedAtUtc = document.UploadedAtUtc,
                UploadedByDisplayName = uploaderName,
                AccessScope = document.AccessScope,
                AccessSummary = accessSummary,
                TargetProperties = propertyLabels,
                TargetPropertyIds = propertyIds,
                AccessPropertyId = document.AccessScope == DocumentAccessScope.PropertyOnly ? document.PropertyId : null,
                FolderId = document.FolderId,
                FolderPath = folderPath,
                Description = document.Description
            };
        }

        private static string ResolveDocumentDisplayName(Document document)
        {
            if (!string.IsNullOrWhiteSpace(document.Title))
            {
                return document.Title!;
            }

            if (!string.IsNullOrWhiteSpace(document.OriginalFileName))
            {
                return document.OriginalFileName;
            }

            return "Document";
        }

        private static Dictionary<int, string> BuildFolderPathMap(IEnumerable<DocumentFolder> folders)
        {
            var lookup = folders.ToDictionary(f => f.Id);
            var cache = new Dictionary<int, string>();

            foreach (var folder in folders)
            {
                BuildFolderPath(folder, lookup, cache);
            }

            return cache;
        }

        private static string BuildFolderPath(DocumentFolder folder, IReadOnlyDictionary<int, DocumentFolder> lookup, Dictionary<int, string> cache)
        {
            if (cache.TryGetValue(folder.Id, out var existing))
            {
                return existing;
            }

            var path = folder.Name;
            if (folder.ParentFolderId.HasValue && lookup.TryGetValue(folder.ParentFolderId.Value, out var parent))
            {
                path = $"{BuildFolderPath(parent, lookup, cache)} / {folder.Name}";
            }

            cache[folder.Id] = path;
            return path;
        }

        private static List<DocumentFolderTreeItemViewModel> BuildFolderTree(
            IReadOnlyCollection<DocumentFolder> folders,
            IReadOnlyCollection<Document> accessibleDocuments,
            int? selectedFolderId)
        {
            var childrenLookup = folders
                .GroupBy(f => f.ParentFolderId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Name).ToList());

            var documentCounts = accessibleDocuments
                .GroupBy(d => d.FolderId ?? 0)
                .ToDictionary(g => g.Key, g => g.Count());

            var tree = new List<DocumentFolderTreeItemViewModel>();

            void AddChildren(int parentKey, int level)
            {
                if (!childrenLookup.TryGetValue(parentKey, out var children))
                {
                    return;
                }

                foreach (var child in children)
                {
                    var count = documentCounts.TryGetValue(child.Id, out var value) ? value : 0;
                    tree.Add(new DocumentFolderTreeItemViewModel
                    {
                        Id = child.Id,
                        Name = child.Name,
                        Level = level,
                        DocumentCount = count,
                        IsSelected = selectedFolderId.HasValue && selectedFolderId.Value == child.Id,
                        ParentFolderId = child.ParentFolderId
                    });

                    AddChildren(child.Id, level + 1);
                }
            }

            AddChildren(0, 0);
            return tree;
        }

        private static string FormatUserName(ApplicationUser? user)
        {
            if (user == null)
            {
                return "Unknown user";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName))
            {
                parts.Add(user.FirstName);
            }

            if (!string.IsNullOrWhiteSpace(user.LastName))
            {
                parts.Add(user.LastName);
            }

            if (parts.Count > 0)
            {
                return string.Join(" ", parts);
            }

            return string.IsNullOrWhiteSpace(user.Email) ? "Unknown user" : user.Email!;
        }

        private async Task<List<int>> GetAccessiblePropertyIdsAsync(ApplicationUser user)
        {
            return await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();
        }

        private static bool UserCanAccessDocument(Document document, IReadOnlyCollection<int> accessiblePropertyIds, ISet<int> accessibleFolderIds)
        {
            if (document.FolderId.HasValue && accessibleFolderIds != null && !accessibleFolderIds.Contains(document.FolderId.Value))
            {
                return false;
            }

            if (document.AccessScope == DocumentAccessScope.AllUsers)
            {
                return true;
            }

            if (!accessiblePropertyIds.Any())
            {
                return false;
            }

            return document.AccessScope switch
            {
                DocumentAccessScope.PropertyOnly => document.PropertyId.HasValue && accessiblePropertyIds.Contains(document.PropertyId.Value),
                DocumentAccessScope.SelectedProperties => document.DocumentProperties.Any(dp => accessiblePropertyIds.Contains(dp.PropertyId)),
                _ => false
            };
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            double size = bytes;
            string[] units = { "KB", "MB", "GB", "TB" };
            var unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        private static (string Field, string Direction) NormalizeSort(string? sortField, string? sortDirection)
        {
            var field = string.IsNullOrWhiteSpace(sortField) ? "uploaded" : sortField.Trim().ToLowerInvariant();
            if (field != "name" && field != "uploaded" && field != "size" && field != "shared" && field != "folder")
            {
                field = "uploaded";
            }

            var direction = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
            return (field, direction);
        }

        private static List<Document> SortDocuments(IEnumerable<Document> documents, string sortField, string sortDirection)
        {
            var ascending = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

            IOrderedEnumerable<Document> ordered = sortField switch
            {
                "name" => ascending
                    ? documents.OrderBy(d => ResolveDocumentDisplayName(d), StringComparer.OrdinalIgnoreCase)
                    : documents.OrderByDescending(d => ResolveDocumentDisplayName(d), StringComparer.OrdinalIgnoreCase),
                "size" => ascending
                    ? documents.OrderBy(d => d.FileSizeBytes)
                    : documents.OrderByDescending(d => d.FileSizeBytes),
                "shared" => ascending
                    ? documents.OrderBy(d => d.AccessScope).ThenBy(d => d.Property?.Name ?? string.Empty)
                    : documents.OrderByDescending(d => d.AccessScope).ThenByDescending(d => d.Property?.Name ?? string.Empty),
                "folder" => ascending
                    ? documents.OrderBy(d => d.Folder?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    : documents.OrderByDescending(d => d.Folder?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                "uploaded" => ascending
                    ? documents.OrderBy(d => d.UploadedAtUtc)
                    : documents.OrderByDescending(d => d.UploadedAtUtc),
                _ => ascending
                    ? documents.OrderBy(d => d.UploadedAtUtc)
                    : documents.OrderByDescending(d => d.UploadedAtUtc)
            };

            return ordered.ThenBy(d => d.Id).ToList();
        }

        private void PersistFolderPreference(int? selectedFolderId, bool showingUnassignedOnly)
        {
            if (showingUnassignedOnly)
            {
                Response.Cookies.Append(LastFolderCookieName, "-1", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(FolderPreferenceExpiryDays),
                    HttpOnly = false,
                    IsEssential = true
                });
                return;
            }

            if (selectedFolderId.HasValue)
            {
                Response.Cookies.Append(LastFolderCookieName, selectedFolderId.Value.ToString(), new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(FolderPreferenceExpiryDays),
                    HttpOnly = false,
                    IsEssential = true
                });
            }
            else
            {
                Response.Cookies.Delete(LastFolderCookieName);
            }
        }

        private static List<DocumentFolderListItemViewModel> BuildChildFolderList(
            IReadOnlyCollection<DocumentFolder> folders,
            IReadOnlyCollection<Document> accessibleDocuments,
            int? selectedFolderId,
            bool showingUnassignedOnly,
            IReadOnlyDictionary<int, string> folderPathMap)
        {
            var parentKey = showingUnassignedOnly ? 0 : selectedFolderId ?? 0;
            var documentCounts = accessibleDocuments
                .Where(d => d.FolderId.HasValue)
                .GroupBy(d => d.FolderId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            return folders
                .Where(f => (f.ParentFolderId ?? 0) == parentKey)
                .OrderBy(f => f.Name)
                .Select(f => new DocumentFolderListItemViewModel
                {
                    Id = f.Id,
                    Name = f.Name,
                    DisplayPath = folderPathMap.TryGetValue(f.Id, out var path) ? path : f.Name,
                    DocumentCount = documentCounts.TryGetValue(f.Id, out var count) ? count : 0
                })
                .ToList();
        }

        private async Task<(List<DocumentFolder> Folders, HashSet<int> AccessibleFolderIds)> GetAccessibleFoldersAsync(IReadOnlyCollection<int> accessiblePropertyIds)
        {
            var allFolders = await _context.DocumentFolders
                .Include(f => f.FolderProperties)
                .OrderBy(f => f.Name)
                .ToListAsync();

            var lookup = allFolders.ToDictionary(f => f.Id);
            var cache = new Dictionary<int, bool>();

            bool IsAccessible(DocumentFolder folder)
            {
                if (cache.TryGetValue(folder.Id, out var cached))
                {
                    return cached;
                }

                if (!FolderVisibleToUser(folder, accessiblePropertyIds))
                {
                    cache[folder.Id] = false;
                    return false;
                }

                if (folder.ParentFolderId.HasValue && lookup.TryGetValue(folder.ParentFolderId.Value, out var parent))
                {
                    if (!IsAccessible(parent))
                    {
                        cache[folder.Id] = false;
                        return false;
                    }
                }

                cache[folder.Id] = true;
                return true;
            }

            var accessibleFolders = allFolders.Where(IsAccessible).ToList();
            var accessibleIds = accessibleFolders.Select(f => f.Id).ToHashSet();
            return (accessibleFolders, accessibleIds);
        }

        private static bool FolderVisibleToUser(DocumentFolder folder, IReadOnlyCollection<int> accessiblePropertyIds)
        {
            if (folder.Visibility == DocumentFolderVisibility.Global)
            {
                return true;
            }

            if (accessiblePropertyIds.Count == 0)
            {
                return false;
            }

            var assignedPropertyIds = folder.FolderProperties
                .Select(fp => fp.PropertyId)
                .Distinct()
                .ToList();

            if (assignedPropertyIds.Count == 0)
            {
                return false;
            }

            return assignedPropertyIds.Any(accessiblePropertyIds.Contains);
        }

        private static string? NormalizeSearch(string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return null;
            }

            return searchTerm.Trim();
        }

        private static bool DocumentMatchesSearch(DocumentListItemViewModel document, string loweredTerm)
        {
            bool Contains(string? value)
            {
                return !string.IsNullOrWhiteSpace(value) &&
                    value.ToLowerInvariant().Contains(loweredTerm);
            }

            if (Contains(document.DisplayName) ||
                Contains(document.OriginalFileName) ||
                Contains(document.Description) ||
                Contains(document.FolderPath))
            {
                return true;
            }

            return document.TargetProperties.Any(Contains);
        }

        private static void ApplyDocumentVisibility(Document document, DocumentAccessScope accessScope, int? propertyId, IEnumerable<int> selectedPropertyIds)
        {
            document.AccessScope = accessScope;
            document.DocumentProperties.Clear();

            switch (accessScope)
            {
                case DocumentAccessScope.AllUsers:
                    document.PropertyId = null;
                    break;
                case DocumentAccessScope.PropertyOnly:
                    document.PropertyId = propertyId;
                    break;
                case DocumentAccessScope.SelectedProperties:
                    document.PropertyId = null;
                    foreach (var property in selectedPropertyIds.Distinct())
                    {
                        document.DocumentProperties.Add(new DocumentProperty
                        {
                            DocumentId = document.Id,
                            PropertyId = property
                        });
                    }
                    break;
            }
        }

        private static void ApplyFolderVisibilityToDocument(Document document, DocumentFolderSettingsForm form)
        {
            if (form.Visibility == DocumentFolderVisibility.Global)
            {
                ApplyDocumentVisibility(document, DocumentAccessScope.AllUsers, null, Array.Empty<int>());
                return;
            }

            var targets = form.SelectedPropertyIds?.Distinct().ToList() ?? new List<int>();
            if (targets.Count == 1)
            {
                ApplyDocumentVisibility(document, DocumentAccessScope.PropertyOnly, targets[0], Array.Empty<int>());
            }
            else
            {
                ApplyDocumentVisibility(document, DocumentAccessScope.SelectedProperties, null, targets);
            }
        }
    }
}

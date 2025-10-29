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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class DocumentsController : BaseController
    {
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
        public async Task<IActionResult> Index(int? folderId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var viewModel = await BuildIndexViewModelAsync(user, null, null, folderId);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload([Bind(Prefix = "Form")] DocumentUploadFormViewModel form, int? currentFolderId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessiblePropertySet = accessiblePropertyIds.ToHashSet();

            if (form.File == null || form.File.Length == 0)
            {
                ModelState.AddModelError(nameof(form.File), "Please select a file to upload.");
            }

            form.Title = string.IsNullOrWhiteSpace(form.Title) ? null : form.Title.Trim();

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
                    currentFolderId ?? form.FolderId);
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
                return RedirectToAction(nameof(Index), new { folderId = form.FolderId.Value });
            }

            if (currentFolderId.HasValue && currentFolderId.Value == -1)
            {
                return RedirectToAction(nameof(Index), new { folderId = -1 });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFolder([Bind(Prefix = "FolderForm")] DocumentFolderFormViewModel form, int? currentFolderId)
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
                parentFolder = await _context.DocumentFolders.FirstOrDefaultAsync(f => f.Id == form.ParentFolderId.Value);
                if (parentFolder == null)
                {
                    ModelState.AddModelError(nameof(form.ParentFolderId), "Selected parent folder was not found.");
                }
            }

            if (string.IsNullOrWhiteSpace(form.Name))
            {
                ModelState.AddModelError(nameof(form.Name), "Folder name is required.");
            }

            if (!ModelState.IsValid)
            {
                var invalidViewModel = await BuildIndexViewModelAsync(user, null, form, currentFolderId);
                return View("Index", invalidViewModel);
            }

            var folder = new DocumentFolder
            {
                Name = form.Name,
                ParentFolderId = form.ParentFolderId,
                CreatedById = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.DocumentFolders.Add(folder);
            await _context.SaveChangesAsync();

            TempData["DocumentSuccess"] = "Folder created successfully.";
            return RedirectToAction(nameof(Index), new { folderId = folder.Id });
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
            if (!UserCanAccessDocument(document, accessiblePropertyIds))
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
            int? selectedFolderId)
        {
            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var accessiblePropertySet = accessiblePropertyIds.ToHashSet();

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty != null && !accessiblePropertySet.Contains(currentProperty.Id))
            {
                currentProperty = null;
            }

            var folderEntities = await _context.DocumentFolders
                .Include(f => f.ParentFolder)
                .OrderBy(f => f.Name)
                .ToListAsync();

            var folderLookup = folderEntities.ToDictionary(f => f.Id);
            var showUnassignedOnly = selectedFolderId.HasValue && selectedFolderId.Value == -1;
            var actualSelectedFolderId = showUnassignedOnly ? (int?)null : selectedFolderId;

            if (actualSelectedFolderId.HasValue && !folderLookup.ContainsKey(actualSelectedFolderId.Value))
            {
                actualSelectedFolderId = null;
            }

            DocumentUploadFormViewModel effectiveUploadForm;
            if (uploadForm == null)
            {
                effectiveUploadForm = new DocumentUploadFormViewModel
                {
                    AccessScope = accessiblePropertySet.Count > 0 ? DocumentAccessScope.PropertyOnly : DocumentAccessScope.AllUsers,
                    PropertyId = currentProperty?.Id,
                    FolderId = actualSelectedFolderId
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

            var documents = await _context.Documents
                .Include(d => d.UploadedBy)
                .Include(d => d.Property)
                .Include(d => d.Folder)
                .Include(d => d.DocumentProperties)
                    .ThenInclude(dp => dp.Property)
                .OrderByDescending(d => d.UploadedAtUtc)
                .ToListAsync();

            var accessibleDocuments = documents
                .Where(d => UserCanAccessDocument(d, accessiblePropertySet))
                .ToList();

            var folderPathMap = BuildFolderPathMap(folderEntities);

            var filteredDocuments = accessibleDocuments
                .Where(d => showUnassignedOnly
                    ? !d.FolderId.HasValue
                    : !actualSelectedFolderId.HasValue || d.FolderId == actualSelectedFolderId.Value)
                .Select(d => ToListItemViewModel(d, folderPathMap))
                .ToList();

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

            var effectiveFolderForm = folderForm == null
                ? new DocumentFolderFormViewModel
                {
                    ParentFolderId = actualSelectedFolderId
                }
                : new DocumentFolderFormViewModel
                {
                    Name = folderForm.Name,
                    ParentFolderId = folderForm.ParentFolderId
                };

            return new DocumentsIndexViewModel
            {
                Documents = filteredDocuments,
                Form = effectiveUploadForm,
                FolderOptions = folderOptions,
                FolderTree = folderTree,
                FolderForm = effectiveFolderForm,
                SelectedFolderId = actualSelectedFolderId,
                ShowingUnassignedOnly = showUnassignedOnly,
                UnassignedDocumentCount = accessibleDocuments.Count(d => !d.FolderId.HasValue),
                TotalDocumentCount = accessibleDocuments.Count,
                PropertyOptions = propertyOptions,
                CurrentPropertyId = currentProperty?.Id,
                CurrentPropertyName = currentProperty?.Name
            };
        }

        private static DocumentListItemViewModel ToListItemViewModel(Document document, IReadOnlyDictionary<int, string> folderPathMap)
        {
            var displayName = !string.IsNullOrWhiteSpace(document.Title)
                ? document.Title!
                : (string.IsNullOrWhiteSpace(document.OriginalFileName) ? "Document" : document.OriginalFileName);

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
                FolderId = document.FolderId,
                FolderPath = folderPath
            };
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

        private static bool UserCanAccessDocument(Document document, IReadOnlyCollection<int> accessiblePropertyIds)
        {
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
    }
}

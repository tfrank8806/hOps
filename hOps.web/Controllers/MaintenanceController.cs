#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using hOps.web.ViewModels.Maintenance;
using hOps.web.ViewModels.PreventiveMaintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hOps.web.Services.Localization;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Maintenance")]
    public class MaintenanceController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly ITranslationService _translationService;
        private const int ChecklistTemplateRowLimit = 500;
        public MaintenanceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITranslationService translationService)
            : base(context, userManager)
        {
            _db = context;
            _translationService = translationService;
        }

        private string GetActiveLanguage()
            => HttpContext?.Items?["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;

        private string Translate(string key, string? fallback = null)
            => _translationService.Translate(key, GetActiveLanguage(), fallback ?? key);

        private bool IsDefaultLanguage(string language)
            => string.Equals(language, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);

        private CancellationToken RequestCancellationToken
            => HttpContext?.RequestAborted ?? CancellationToken.None;

        [HttpGet("PMs/Checklists")]
        public async Task<IActionResult> PmChecklists(int? propertyId = null, int? checklistId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var viewModel = await BuildChecklistPageAsync(currentUser, roles, propertyId, checklistId);
            if (viewModel == null)
            {
                return Forbid();
            }

            ViewBag.PmChecklistMessage = TempData["PmChecklistMessage"];
            ViewBag.PmChecklistError = TempData["PmChecklistError"];
            return View("PmChecklists", viewModel);
        }

        [HttpPost("PMs/Checklists/Save")]
        public async Task<IActionResult> SaveChecklist(MaintenancePmChecklistSaveRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            var selectedProperty = properties.FirstOrDefault(p => p.Id == request.PropertyId);
            if (selectedProperty == null)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var editorOverride = BuildEditorFromRequest(request);
                var viewModel = await BuildChecklistPageAsync(currentUser, roles, request.PropertyId, request.Id, editorOverride);
                if (viewModel == null)
                {
                    return Forbid();
                }

                return View("PmChecklists", viewModel);
            }

            var areaLabels = request.ChecklistType == PreventiveMaintenanceChecklistType.Area
                ? ExtractAreaOptions(request.AreaOptionsText)
                : new List<string>();
            var areaJson = request.ChecklistType == PreventiveMaintenanceChecklistType.Area
                ? MaintenanceChecklistHelper.BuildAreaOptionsJson(areaLabels)
                : "[]";

            var now = DateTime.UtcNow;

            if (request.Id.HasValue && request.Id.Value > 0)
            {
                var checklist = await _db.PreventiveMaintenanceChecklists
                    .FirstOrDefaultAsync(c => c.Id == request.Id.Value && c.PropertyId == selectedProperty.Id);
                if (checklist == null)
                {
                    TempData["PmChecklistError"] = Translate("Checklist not found.");
                    return RedirectToAction(nameof(PmChecklists), new { propertyId = selectedProperty.Id });
                }

                var hasSessions = await _db.PreventiveMaintenanceSessions
                    .AnyAsync(s => s.ChecklistId == checklist.Id);
                if (hasSessions && checklist.ChecklistType != request.ChecklistType)
                {
                    TempData["PmChecklistError"] = Translate("Checklist type cannot be changed after PM sessions have been recorded.");
                    return RedirectToAction(nameof(PmChecklists), new { propertyId = selectedProperty.Id, checklistId = checklist.Id });
                }

                checklist.Name = request.Name.Trim();
                checklist.IsActive = request.IsActive;
                checklist.ChecklistType = request.ChecklistType;
                checklist.AreaOptionsJson = areaJson;
                checklist.UpdatedAtUtc = now;
                checklist.UpdatedById = currentUser.Id;
            }
            else
            {
                var nextSortOrder = await GetNextChecklistSortOrderAsync(selectedProperty.Id);
                var checklist = new PreventiveMaintenanceChecklist
                {
                    PropertyId = selectedProperty.Id,
                    Name = request.Name.Trim(),
                    ChecklistType = request.ChecklistType,
                    AreaOptionsJson = areaJson,
                    IsActive = request.IsActive,
                    SortOrder = nextSortOrder,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    CreatedById = currentUser.Id,
                    UpdatedById = currentUser.Id
                };
                _db.PreventiveMaintenanceChecklists.Add(checklist);
                await _db.SaveChangesAsync();

                TempData["PmChecklistMessage"] = Translate("Checklist created.");
                return RedirectToAction(nameof(PmChecklists), new { propertyId = selectedProperty.Id, checklistId = checklist.Id });
            }

            await _db.SaveChangesAsync();
            TempData["PmChecklistMessage"] = Translate("Checklist updated.");
            return RedirectToAction(nameof(PmChecklists), new { propertyId = selectedProperty.Id, checklistId = request.Id });
        }

        [HttpPost("PMs/Checklists/Frequency")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveChecklistSettings(int propertyId, int frequencyPerYear)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            var selectedProperty = properties.FirstOrDefault(p => p.Id == propertyId);
            if (selectedProperty == null)
            {
                return Forbid();
            }

            var normalizedFrequency = Math.Clamp(frequencyPerYear, 1, 52);
            var now = DateTime.UtcNow;

            var setting = await _db.PreventiveMaintenanceSettings
                .FirstOrDefaultAsync(s => s.PropertyId == propertyId);

            if (setting == null)
            {
                setting = new PreventiveMaintenanceSetting
                {
                    PropertyId = propertyId,
                    FrequencyPerYear = normalizedFrequency,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = currentUser.Id
                };
                _db.PreventiveMaintenanceSettings.Add(setting);
            }
            else
            {
                setting.FrequencyPerYear = normalizedFrequency;
                setting.UpdatedAtUtc = now;
                setting.UpdatedByUserId = currentUser.Id;
            }

            await _db.SaveChangesAsync();

            TempData["PmChecklistMessage"] = Translate("PM frequency updated.");
            return RedirectToAction(nameof(PmChecklists), new { propertyId });
        }

        [HttpPost("PMs/Checklists/Reorder")]
        public async Task<IActionResult> ReorderChecklists([FromBody] MaintenancePmChecklistReorderRequest? request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            if (request == null || request.PropertyId <= 0 || request.ChecklistIds == null || request.ChecklistIds.Count == 0)
            {
                return BadRequest(new { error = Translate("Provide a property and checklist order.") });
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            var property = properties.FirstOrDefault(p => p.Id == request.PropertyId);
            if (property == null)
            {
                return Forbid();
            }

            var checklists = await _db.PreventiveMaintenanceChecklists
                .Where(c => c.PropertyId == property.Id)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToListAsync();

            if (!checklists.Any())
            {
                return BadRequest(new { error = Translate("No checklists exist for this property.") });
            }

            var propertyChecklistIds = checklists.Select(c => c.Id).ToHashSet();
            var invalidIds = request.ChecklistIds.Where(id => !propertyChecklistIds.Contains(id)).ToList();
            if (invalidIds.Any())
            {
                return BadRequest(new { error = Translate("One or more checklists are invalid for this property.") });
            }

            var orderLookup = request.ChecklistIds
                .Select((id, index) => (id, index))
                .GroupBy(x => x.id)
                .ToDictionary(group => group.Key, group => group.First().index);

            var nextOrder = orderLookup.Count;
            var now = DateTime.UtcNow;
            foreach (var checklist in checklists)
            {
                if (orderLookup.TryGetValue(checklist.Id, out var order))
                {
                    checklist.SortOrder = order;
                }
                else
                {
                    checklist.SortOrder = nextOrder++;
                }

                checklist.UpdatedAtUtc = now;
                checklist.UpdatedById = currentUser.Id;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("PMs/Checklists/{id:int}/Toggle")]
        public async Task<IActionResult> ToggleChecklist(int id, int propertyId, bool isActive)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            if (properties.All(p => p.Id != propertyId))
            {
                return Forbid();
            }

            var checklist = await _db.PreventiveMaintenanceChecklists
                .FirstOrDefaultAsync(c => c.Id == id && c.PropertyId == propertyId);
            if (checklist == null)
            {
                TempData["PmChecklistError"] = Translate("Checklist not found.");
                return RedirectToAction(nameof(PmChecklists), new { propertyId });
            }

            checklist.IsActive = isActive;
            checklist.UpdatedAtUtc = DateTime.UtcNow;
            checklist.UpdatedById = currentUser.Id;
            await _db.SaveChangesAsync();

            TempData["PmChecklistMessage"] = isActive ? Translate("Checklist activated.") : Translate("Checklist deactivated.");
            return RedirectToAction(nameof(PmChecklists), new { propertyId, checklistId = id });
        }

        [HttpPost("PMs/Checklists/{id:int}/Delete")]
        public async Task<IActionResult> DeleteChecklist(int id, int propertyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            if (properties.All(p => p.Id != propertyId))
            {
                return Forbid();
            }

            var checklist = await _db.PreventiveMaintenanceChecklists
                .FirstOrDefaultAsync(c => c.Id == id && c.PropertyId == propertyId);
            if (checklist == null)
            {
                TempData["PmChecklistError"] = Translate("Checklist not found.");
                return RedirectToAction(nameof(PmChecklists), new { propertyId });
            }

            var hasSessions = await _db.PreventiveMaintenanceSessions
                .AnyAsync(s => s.ChecklistId == id);
            if (hasSessions)
            {
                TempData["PmChecklistError"] = Translate("Completed PM sessions reference this checklist. Deactivate it instead of deleting.");
                return RedirectToAction(nameof(PmChecklists), new { propertyId, checklistId = id });
            }

            var tasks = await _db.PreventiveMaintenanceTasks
                .Where(t => t.ChecklistId == id)
                .ToListAsync();
            if (tasks.Any())
            {
                _db.PreventiveMaintenanceTasks.RemoveRange(tasks);
            }

            _db.PreventiveMaintenanceChecklists.Remove(checklist);
            await _db.SaveChangesAsync();

            TempData["PmChecklistMessage"] = Translate("Checklist deleted.");
            return RedirectToAction(nameof(PmChecklists), new { propertyId });
        }

        [HttpGet("PMs/Checklists/{checklistId:int}/DownloadCsv")]
        public async Task<IActionResult> DownloadChecklistCsv(int checklistId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var checklist = await _db.PreventiveMaintenanceChecklists
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == checklistId);
            if (checklist == null)
            {
                return NotFound();
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            if (properties.All(p => p.Id != checklist.PropertyId))
            {
                return Forbid();
            }

            var tasks = await _db.PreventiveMaintenanceTasks
                .AsNoTracking()
                .Where(t => t.ChecklistId == checklistId)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var builder = new StringBuilder();
            var activeLanguage = GetActiveLanguage();
            var isDefaultLanguage = IsDefaultLanguage(activeLanguage);
            var cancellationToken = RequestCancellationToken;
            var headerTask = _translationService.Translate("Task", activeLanguage, "Task");
            var headerDescription = _translationService.Translate("Description", activeLanguage, "Description");
            builder.AppendLine($"{EscapeCsvCell(headerTask)},{EscapeCsvCell(headerDescription)}");
            foreach (var task in tasks)
            {
                var title = task.Name ?? string.Empty;
                var description = task.Description ?? string.Empty;
                if (!isDefaultLanguage)
                {
                    var entityId = task.Id.ToString(CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        var translatedTitle = await _translationService.TranslateDynamicAsync(
                            "PmChecklistTask",
                            entityId,
                            "Title",
                            title,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translatedTitle))
                        {
                            title = translatedTitle;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        var translatedDescription = await _translationService.TranslateDynamicAsync(
                            "PmChecklistTask",
                            entityId,
                            "Description",
                            description,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translatedDescription))
                        {
                            description = translatedDescription;
                        }
                    }
                }

                builder.AppendLine($"{EscapeCsvCell(title)},{EscapeCsvCell(description)}");
            }

            var payload = Encoding.UTF8.GetBytes(builder.ToString());
            var fileName = $"pm-checklist-{CreateSafeFileName(checklist.Name)}.csv";
            return File(payload, "text/csv", fileName);
        }

        [HttpGet("PMs/Checklists/Template/Download")]
        public IActionResult DownloadChecklistTemplateCsv()
        {
            var builder = new StringBuilder();
            var activeLanguage = GetActiveLanguage();
            var headerTask = _translationService.Translate("Task", activeLanguage, "Task");
            var headerDescription = _translationService.Translate("Description", activeLanguage, "Description");
            builder.AppendLine($"{EscapeCsvCell(headerTask)},{EscapeCsvCell(headerDescription)}");

            var sampleTask1 = _translationService.Translate("Inspect HVAC filters", activeLanguage, "Inspect HVAC filters");
            var sampleDescription1 = _translationService.Translate("Replace or clean as needed; record readings", activeLanguage, "Replace or clean as needed; record readings");
            var sampleTask2 = _translationService.Translate("Check fire extinguisher pressure", activeLanguage, "Check fire extinguisher pressure");
            var sampleDescription2 = _translationService.Translate("Verify seal is intact and tag is signed", activeLanguage, "Verify seal is intact and tag is signed");

            builder.AppendLine($"{EscapeCsvCell(sampleTask1)},{EscapeCsvCell(sampleDescription1)}");
            builder.AppendLine($"{EscapeCsvCell(sampleTask2)},{EscapeCsvCell(sampleDescription2)}");
            var payload = Encoding.UTF8.GetBytes(builder.ToString());
            return File(payload, "text/csv", "pm-checklist-template.csv");
        }

        [HttpPost("PMs/Checklists/Template/Upload")]
        public async Task<IActionResult> UploadChecklistTemplate([Bind(Prefix = "TemplateUpload")] MaintenancePmTemplateUploadViewModel form)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            var property = properties.FirstOrDefault(p => p.Id == form.PropertyId);
            if (property == null)
            {
                return Forbid();
            }

            if (form.CsvFile == null || form.CsvFile.Length == 0)
            {
                ModelState.AddModelError(nameof(form.CsvFile), Translate("Select a CSV file to upload."));
            }

            List<(string Title, string? Description)> rows = new();
            if (ModelState.IsValid && form.CsvFile != null)
            {
                try
                {
                    rows = await ParseChecklistTemplateCsvAsync(form.CsvFile);
                    if (rows.Count == 0)
                    {
                        ModelState.AddModelError(nameof(form.CsvFile), Translate("The file did not contain any checklist rows."));
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(form.CsvFile), Translate(ex.Message, ex.Message));
                }
            }

            if (!ModelState.IsValid)
            {
                var viewModel = await BuildChecklistPageAsync(currentUser, roles, form.PropertyId, null, null, form);
                if (viewModel == null)
                {
                    return Forbid();
                }
                return View("PmChecklists", viewModel);
            }

            var areaLabels = form.ChecklistType == PreventiveMaintenanceChecklistType.Area
                ? ExtractAreaOptions(form.AreaOptionsText)
                : new List<string>();
            var areaJson = form.ChecklistType == PreventiveMaintenanceChecklistType.Area
                ? MaintenanceChecklistHelper.BuildAreaOptionsJson(areaLabels)
                : "[]";

            var now = DateTime.UtcNow;
            var nextSortOrder = await GetNextChecklistSortOrderAsync(property.Id);
            var checklist = new PreventiveMaintenanceChecklist
            {
                PropertyId = property.Id,
                Name = form.Name.Trim(),
                ChecklistType = form.ChecklistType,
                AreaOptionsJson = areaJson,
                IsActive = true,
                SortOrder = nextSortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedById = currentUser.Id,
                UpdatedById = currentUser.Id
            };

            _db.PreventiveMaintenanceChecklists.Add(checklist);
            await _db.SaveChangesAsync();

            var tasks = rows
                .Select((row, index) => new PreventiveMaintenanceTask
                {
                    ChecklistId = checklist.Id,
                    PropertyId = property.Id,
                    Name = row.Title,
                    Description = row.Description,
                    SortOrder = index,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                })
                .ToList();

            if (tasks.Any())
            {
                _db.PreventiveMaintenanceTasks.AddRange(tasks);
                await _db.SaveChangesAsync();
            }

            var activeLanguage = GetActiveLanguage();
            var isDefaultLanguage = IsDefaultLanguage(activeLanguage);
            var checklistIdString = checklist.Id.ToString(CultureInfo.InvariantCulture);
            var nameDisplay = checklist.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(nameDisplay) && !isDefaultLanguage)
            {
                var translatedName = await _translationService.TranslateDynamicAsync(
                    "PmChecklist",
                    checklistIdString,
                    "Name",
                    nameDisplay,
                    _translationService.DefaultLanguage,
                    activeLanguage,
                    RequestCancellationToken);
                if (!string.IsNullOrWhiteSpace(translatedName))
                {
                    nameDisplay = translatedName;
                }
            }

            var messageTemplate = rows.Count == 1
                ? Translate("Checklist '{0}' created with {1} task.")
                : Translate("Checklist '{0}' created with {1} tasks.");
            TempData["PmChecklistMessage"] = string.Format(CultureInfo.InvariantCulture, messageTemplate, nameDisplay, rows.Count);
            return RedirectToAction(nameof(PmChecklists), new { propertyId = property.Id, checklistId = checklist.Id });
        }

        [HttpGet("PMs/Checklists/{checklistId:int}/Tasks")]
        public async Task<IActionResult> ChecklistTasks(int checklistId, int? propertyId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var checklist = await _db.PreventiveMaintenanceChecklists
                .Include(c => c.Property)
                .FirstOrDefaultAsync(c => c.Id == checklistId);
            if (checklist == null)
            {
                return NotFound();
            }

            var properties = await GetManageablePropertiesAsync(currentUser, roles);
            if (properties.All(p => p.Id != checklist.PropertyId))
            {
                return Forbid();
            }

            var tasks = await _db.PreventiveMaintenanceTasks
                .Where(t => t.ChecklistId == checklist.Id)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Id)
                .Select(t => new PmSetupTaskRow
                {
                    Id = t.Id,
                    Title = t.Name,
                    Description = t.Description,
                    SortOrder = t.SortOrder
                })
                .ToListAsync();

            if (!tasks.Any())
            {
                tasks.Add(new PmSetupTaskRow());
            }

            var viewModel = new MaintenancePmChecklistTasksViewModel
            {
                ChecklistId = checklist.Id,
                PropertyId = checklist.PropertyId,
                PropertyName = checklist.Property.Name,
                ChecklistName = checklist.Name,
                ChecklistType = checklist.ChecklistType,
                Tasks = tasks,
                AreaOptions = MaintenanceChecklistHelper.ParseAreaOptions(checklist.AreaOptionsJson)
            };

            ViewBag.PmChecklistMessage = TempData["PmChecklistMessage"];
            ViewBag.PmChecklistError = TempData["PmChecklistError"];
            return View("PmChecklistTasks", viewModel);
        }

        [HttpPost("PMs/Checklists/{checklistId:int}/Tasks")]
        public async Task<IActionResult> SaveChecklistTasks(int checklistId, int propertyId, List<PmSetupTaskRow> tasks)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!UserCanManageMaintenance(roles))
            {
                return Forbid();
            }

            var checklist = await _db.PreventiveMaintenanceChecklists
                .FirstOrDefaultAsync(c => c.Id == checklistId && c.PropertyId == propertyId);
            if (checklist == null)
            {
                TempData["PmChecklistError"] = Translate("Checklist not found.");
                return RedirectToAction(nameof(PmChecklists), new { propertyId });
            }

            var sanitizedTasks = (tasks ?? new List<PmSetupTaskRow>())
                .Where(t => !string.IsNullOrWhiteSpace(t.Title))
                .Select((t, index) => new
                {
                    t.Id,
                    Title = t.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(t.Description) ? null : t.Description.Trim(),
                    SortOrder = index
                })
                .ToList();

            var existingTasks = await _db.PreventiveMaintenanceTasks
                .Where(t => t.ChecklistId == checklistId)
                .ToListAsync();

            var retainedIds = new HashSet<int>();
            var now = DateTime.UtcNow;

            foreach (var row in sanitizedTasks)
            {
                if (row.Id > 0)
                {
                    var existing = existingTasks.FirstOrDefault(t => t.Id == row.Id);
                    if (existing != null)
                    {
                        retainedIds.Add(existing.Id);
                        existing.Name = row.Title;
                        existing.Description = row.Description;
                        existing.SortOrder = row.SortOrder;
                        existing.UpdatedAtUtc = now;
                        continue;
                    }
                }

                var newTask = new PreventiveMaintenanceTask
                {
                    ChecklistId = checklistId,
                    PropertyId = propertyId,
                    Name = row.Title,
                    Description = row.Description,
                    SortOrder = row.SortOrder,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                _db.PreventiveMaintenanceTasks.Add(newTask);
            }

            var toRemove = existingTasks
                .Where(t => !retainedIds.Contains(t.Id))
                .ToList();
            if (toRemove.Any())
            {
                _db.PreventiveMaintenanceTasks.RemoveRange(toRemove);
            }

            await _db.SaveChangesAsync();
            TempData["PmChecklistMessage"] = Translate("Checklist tasks saved.");
            return RedirectToAction(nameof(ChecklistTasks), new { checklistId, propertyId });
        }

        private async Task<MaintenancePmChecklistsPageViewModel?> BuildChecklistPageAsync(
            ApplicationUser user,
            IList<string> roles,
            int? propertyId,
            int? checklistId,
            MaintenancePmChecklistEditorViewModel? editorOverride = null,
            MaintenancePmTemplateUploadViewModel? templateOverride = null)
        {
            var properties = await GetManageablePropertiesAsync(user, roles);
            if (!properties.Any())
            {
                return null;
            }

            var selectedProperty = propertyId.HasValue && properties.Any(p => p.Id == propertyId.Value)
                ? properties.First(p => p.Id == propertyId.Value)
                : properties.First();

            var setting = await _db.PreventiveMaintenanceSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.PropertyId == selectedProperty.Id);

            var checklistEntities = await _db.PreventiveMaintenanceChecklists
                .AsNoTracking()
                .Where(c => c.PropertyId == selectedProperty.Id)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToListAsync();

            var taskCounts = await _db.PreventiveMaintenanceTasks
                .Where(t => t.PropertyId == selectedProperty.Id)
                .GroupBy(t => t.ChecklistId)
                .Select(g => new { ChecklistId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChecklistId, x => x.Count);

            var sessionCounts = await _db.PreventiveMaintenanceSessions
                .Where(s => s.PropertyId == selectedProperty.Id)
                .GroupBy(s => s.ChecklistId)
                .Select(g => new { ChecklistId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChecklistId, x => x.Count);

            var summaries = checklistEntities
                .Select(c => new MaintenancePmChecklistSummaryViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    ChecklistType = c.ChecklistType,
                    IsActive = c.IsActive,
                    TaskCount = taskCounts.TryGetValue(c.Id, out var taskCount) ? taskCount : 0,
                    SessionCount = sessionCounts.TryGetValue(c.Id, out var sessionCount) ? sessionCount : 0,
                    AreaOptions = MaintenanceChecklistHelper.ParseAreaOptions(c.AreaOptionsJson)
                })
                .ToList();

            MaintenancePmChecklistEditorViewModel editor;
            if (editorOverride != null)
            {
                editor = editorOverride;
                editor.PropertyId = selectedProperty.Id;
            }
            else
            {
                var editorChecklist = checklistId.HasValue
                    ? checklistEntities.FirstOrDefault(c => c.Id == checklistId.Value)
                    : checklistEntities.FirstOrDefault();
                editor = BuildEditorViewModel(editorChecklist, selectedProperty.Id, sessionCounts);
            }

            return new MaintenancePmChecklistsPageViewModel
            {
                PropertyId = selectedProperty.Id,
                PropertyName = selectedProperty.Name,
                AccessibleProperties = properties,
                Checklists = summaries,
                ChecklistEditor = editor,
                TemplateUpload = templateOverride ?? new MaintenancePmTemplateUploadViewModel
                {
                    PropertyId = selectedProperty.Id,
                    ChecklistType = templateOverride?.ChecklistType ?? PreventiveMaintenanceChecklistType.Room,
                    Name = templateOverride?.Name ?? string.Empty,
                    AreaOptionsText = templateOverride?.AreaOptionsText
                },
                FrequencyPerYear = setting?.FrequencyPerYear ?? 0
            };
        }

        private async Task<List<(string Title, string? Description)>> ParseChecklistTemplateCsvAsync(IFormFile file)
        {
            var rows = new List<(string Title, string? Description)>();

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            var lineNumber = 0;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                if (rows.Count >= ChecklistTemplateRowLimit)
                {
                    throw new InvalidOperationException($"Templates can include up to {ChecklistTemplateRowLimit} rows.");
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var cells = SplitCsvLine(line);
                if (lineNumber == 1 && cells.Count > 0 && cells[0].Trim().Equals("task", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var title = cells.Count > 0 ? cells[0].Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var description = cells.Count > 1 ? cells[1].Trim() : null;

                if (title.Length > 200)
                {
                    title = title.Substring(0, 200).Trim();
                }

                if (!string.IsNullOrWhiteSpace(description) && description!.Length > 1000)
                {
                    description = description.Substring(0, 1000).Trim();
                }

                rows.Add((title, string.IsNullOrWhiteSpace(description) ? null : description));
            }

            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            if (line == null)
            {
                return values;
            }

            var builder = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    values.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                builder.Append(ch);
            }

            values.Add(builder.ToString());
            return values;
        }

        private static string EscapeCsvCell(string? value)
        {
            var text = value ?? string.Empty;
            var needsQuotes = text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needsQuotes)
            {
                return text;
            }

            var sanitized = text.Replace("\"", "\"\"", StringComparison.Ordinal);
            return $"\"{sanitized}\"";
        }

        private static string CreateSafeFileName(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "pm-checklist";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(title.Length);
            foreach (var ch in title.Trim())
            {
                builder.Append(invalid.Contains(ch) ? '-' : ch);
            }

            var cleaned = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(cleaned) ? "pm-checklist" : cleaned.ToLowerInvariant();
        }

        private MaintenancePmChecklistEditorViewModel BuildEditorViewModel(
            PreventiveMaintenanceChecklist? checklist,
            int propertyId,
            IReadOnlyDictionary<int, int> sessionCounts)
        {
            if (checklist == null)
            {
                return new MaintenancePmChecklistEditorViewModel
                {
                    PropertyId = propertyId,
                    ChecklistType = PreventiveMaintenanceChecklistType.Room,
                    AreaOptionsText = string.Empty,
                    IsActive = true,
                    CanChangeType = true
                };
            }

            var sessionCount = sessionCounts.TryGetValue(checklist.Id, out var existingSessions) ? existingSessions : 0;
            var areaOptionsText = string.Join(Environment.NewLine, MaintenanceChecklistHelper.ParseAreaOptions(checklist.AreaOptionsJson));

            return new MaintenancePmChecklistEditorViewModel
            {
                Id = checklist.Id,
                PropertyId = checklist.PropertyId,
                Name = checklist.Name,
                ChecklistType = checklist.ChecklistType,
                IsActive = checklist.IsActive,
                AreaOptionsText = areaOptionsText,
                CanChangeType = sessionCount == 0,
                HasExistingSessions = sessionCount > 0
            };
        }

        private MaintenancePmChecklistEditorViewModel BuildEditorFromRequest(MaintenancePmChecklistSaveRequest request)
        {
            return new MaintenancePmChecklistEditorViewModel
            {
                Id = request.Id,
                PropertyId = request.PropertyId,
                Name = request.Name,
                ChecklistType = request.ChecklistType,
                IsActive = request.IsActive,
                AreaOptionsText = request.AreaOptionsText,
                CanChangeType = true,
                HasExistingSessions = false
            };
        }

        private async Task<int> GetNextChecklistSortOrderAsync(int propertyId)
        {
            var maxSortOrder = await _db.PreventiveMaintenanceChecklists
                .Where(c => c.PropertyId == propertyId)
                .Select(c => (int?)c.SortOrder)
                .MaxAsync();

            return maxSortOrder.HasValue ? maxSortOrder.Value + 1 : 0;
        }

        private async Task<List<Property>> GetManageablePropertiesAsync(ApplicationUser user, IList<string> roles)
        {
            var normalizedRoles = roles.Select(r => r ?? string.Empty).ToList();
            var isAdmin = normalizedRoles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            var isManager = normalizedRoles.Any(r => r.Equals("Manager", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin && !isManager)
            {
                return new List<Property>();
            }

            if (isAdmin)
            {
                return await _db.Properties
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }

            var accessibleIds = await _db.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();

            return await _db.Properties
                .Where(p => accessibleIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        private static List<string> ExtractAreaOptions(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            var entries = text
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => MaintenanceChecklistHelper.NormalizeAreaLabel(entry))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => label!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return entries;
        }

        private static bool UserCanManageMaintenance(IList<string> roles)
        {
            return roles.Any(r =>
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        }
    }
}

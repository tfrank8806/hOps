#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using hOps.web.ViewModels.Maintenance;
using hOps.web.ViewModels.PreventiveMaintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Maintenance")]
    public class MaintenanceController : BaseController
    {
        private readonly ApplicationDbContext _db;
        public MaintenanceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _db = context;
        }

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
                    TempData["PmChecklistError"] = "Checklist not found.";
                    return RedirectToAction(nameof(PmChecklists), new { propertyId = selectedProperty.Id });
                }

                var hasSessions = await _db.PreventiveMaintenanceSessions
                    .AnyAsync(s => s.ChecklistId == checklist.Id);
                if (hasSessions && checklist.ChecklistType != request.ChecklistType)
                {
                    TempData["PmChecklistError"] = "Checklist type cannot be changed after PM sessions have been recorded.";
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
                var checklist = new PreventiveMaintenanceChecklist
                {
                    PropertyId = selectedProperty.Id,
                    Name = request.Name.Trim(),
                    ChecklistType = request.ChecklistType,
                    AreaOptionsJson = areaJson,
                    IsActive = request.IsActive,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    CreatedById = currentUser.Id,
                    UpdatedById = currentUser.Id
                };
                _db.PreventiveMaintenanceChecklists.Add(checklist);
                await _db.SaveChangesAsync();

                TempData["PmChecklistMessage"] = "Checklist created.";
                return RedirectToAction(nameof(PmChecklists), new { propertyId = selectedProperty.Id, checklistId = checklist.Id });
            }

            await _db.SaveChangesAsync();
            TempData["PmChecklistMessage"] = "Checklist updated.";
            return RedirectToAction(nameof(PmChecklists), new { propertyId = selectedProperty.Id, checklistId = request.Id });
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
                TempData["PmChecklistError"] = "Checklist not found.";
                return RedirectToAction(nameof(PmChecklists), new { propertyId });
            }

            checklist.IsActive = isActive;
            checklist.UpdatedAtUtc = DateTime.UtcNow;
            checklist.UpdatedById = currentUser.Id;
            await _db.SaveChangesAsync();

            TempData["PmChecklistMessage"] = isActive ? "Checklist activated." : "Checklist deactivated.";
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
                TempData["PmChecklistError"] = "Checklist not found.";
                return RedirectToAction(nameof(PmChecklists), new { propertyId });
            }

            var hasSessions = await _db.PreventiveMaintenanceSessions
                .AnyAsync(s => s.ChecklistId == id);
            if (hasSessions)
            {
                TempData["PmChecklistError"] = "Completed PM sessions reference this checklist. Deactivate it instead of deleting.";
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

            TempData["PmChecklistMessage"] = "Checklist deleted.";
            return RedirectToAction(nameof(PmChecklists), new { propertyId });
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
                TempData["PmChecklistError"] = "Checklist not found.";
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
            TempData["PmChecklistMessage"] = "Checklist tasks saved.";
            return RedirectToAction(nameof(ChecklistTasks), new { checklistId, propertyId });
        }

        private async Task<MaintenancePmChecklistsPageViewModel?> BuildChecklistPageAsync(
            ApplicationUser user,
            IList<string> roles,
            int? propertyId,
            int? checklistId,
            MaintenancePmChecklistEditorViewModel? editorOverride = null)
        {
            var properties = await GetManageablePropertiesAsync(user, roles);
            if (!properties.Any())
            {
                return null;
            }

            var selectedProperty = propertyId.HasValue && properties.Any(p => p.Id == propertyId.Value)
                ? properties.First(p => p.Id == propertyId.Value)
                : properties.First();

            var checklistEntities = await _db.PreventiveMaintenanceChecklists
                .AsNoTracking()
                .Where(c => c.PropertyId == selectedProperty.Id)
                .OrderByDescending(c => c.IsActive)
                .ThenBy(c => c.Name)
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
                ChecklistEditor = editor
            };
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

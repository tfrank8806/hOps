#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Utilities;
using hOps.web.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Maintenance/Logs")]
    public class MaintenanceLogsController : BaseController
    {
        private const int MaxEntryDisplayCount = 500;
        private const int TemplateColumnImportLimit = 120;
        private const string LogsIndexView = "~/Views/Maintenance/Logs/Index.cshtml";
        private const string LogsEditorView = "~/Views/Maintenance/Logs/Editor.cshtml";
        private const string LogsCreateView = "~/Views/Maintenance/Logs/Create.cshtml";
        private const string LogsChecklistView = "~/Views/Maintenance/Logs/Checklist.cshtml";
        private const string LogsDetailView = "~/Views/Maintenance/Logs/Detail.cshtml";
        private const string EmergencyLightLogView = "~/Views/Maintenance/Logs/EmergencyExitLights.cshtml";
        private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        private static readonly string[] AllowedChecklistExtensions = { ".csv", ".xlsx" };
        private const long ChecklistFileMaxBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedCompletionAttachmentExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
        private const string PhotoUploadFolder = "uploads/maintenance-logs";
        private const string ChecklistUploadFolder = "uploads/maintenance-log-checklists";
        private const string CompletionAttachmentFolder = "uploads/maintenance-log-completions";
        private const int MaxCompletionAttachments = 10;

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly IUserTimeZoneService _timeZoneService; // Property-specific timezones are not stored yet, so we rely on the viewer's timezone.
        private readonly IMaintenanceLogCycleService _cycleService;
        private static readonly JsonSerializerOptions EntrySerializerOptions = new(JsonSerializerDefaults.Web);

        public MaintenanceLogsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IUserTimeZoneService timeZoneService,
            IMaintenanceLogCycleService cycleService)
            : base(context, userManager)
        {
            _db = context;
            _environment = environment;
            _timeZoneService = timeZoneService;
            _cycleService = cycleService;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index(
            MaintenanceLogScheduleType? schedule = null,
            string? status = null,
            string? completion = null,
            string? name = null,
            int history = 0)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to view maintenance logs.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = UserCanManage(roles);
            var historyBlocks = Math.Clamp(history, 0, 12);
            var filters = new MaintenanceLogIndexFilterViewModel
            {
                ScheduleFilter = schedule,
                StatusFilter = (status ?? string.Empty).Trim(),
                CompletionFilter = (completion ?? string.Empty).Trim(),
                NameQuery = (name ?? string.Empty).Trim()
            };

            var templates = await _db.MaintenanceLogTemplates
                .Where(t => t.PropertyId == property.Id)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.Name)
                .ToListAsync();

            var timeZone = _timeZoneService.GetTimeZone();
            var templateItems = new List<MaintenanceLogTemplateListItemViewModel>();
            foreach (var template in templates)
            {
                if (!SupportsCycleRendering(template.ScheduleType))
                {
                    templateItems.Add(new MaintenanceLogTemplateListItemViewModel
                    {
                        TemplateId = template.Id,
                        Name = template.Name,
                        ScheduleType = template.ScheduleType,
                        ScheduleSummary = MaintenanceLogTemplateHelper.BuildScheduleSummary(template),
                        IsActive = template.IsActive,
                        ChecklistFilePath = template.ChecklistFilePath,
                        VisibleCycles = Array.Empty<MaintenanceLogCycleHistoryItemViewModel>(),
                        LatestStatus = MaintenanceLogCycleStatusKind.Upcoming,
                        IsOverdue = false
                    });
                    continue;
                }

                var cycles = await BuildCycleHistoryAsync(template, timeZone, historyBlocks);
                var orderedCycles = cycles
                    .OrderByDescending(cycle => cycle.StartLocal)
                    .ToList();
                var latestStatus = orderedCycles.FirstOrDefault()?.Status ?? MaintenanceLogCycleStatusKind.Upcoming;

                var item = new MaintenanceLogTemplateListItemViewModel
                {
                    TemplateId = template.Id,
                    Name = template.Name,
                    ScheduleType = template.ScheduleType,
                    ScheduleSummary = MaintenanceLogTemplateHelper.BuildScheduleSummary(template),
                    IsActive = template.IsActive,
                    ChecklistFilePath = template.ChecklistFilePath,
                    VisibleCycles = orderedCycles,
                    LatestStatus = latestStatus,
                    IsOverdue = latestStatus == MaintenanceLogCycleStatusKind.Overdue
                };

                if (!PassesFilters(item, filters))
                {
                    continue;
                }

                templateItems.Add(item);
            }

            var ordered = templateItems
                .OrderByDescending(item => item.IsOverdue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var daily = ordered.Where(item => item.ScheduleType == MaintenanceLogScheduleType.Daily).ToList();
            var weekly = ordered.Where(item => item.ScheduleType == MaintenanceLogScheduleType.Weekly).ToList();
            var monthly = ordered.Where(item => item.ScheduleType == MaintenanceLogScheduleType.Monthly).ToList();
            var other = ordered.Where(item =>
                item.ScheduleType != MaintenanceLogScheduleType.Daily &&
                item.ScheduleType != MaintenanceLogScheduleType.Weekly &&
                item.ScheduleType != MaintenanceLogScheduleType.Monthly &&
                item.ScheduleType != MaintenanceLogScheduleType.Quarterly &&
                item.ScheduleType != MaintenanceLogScheduleType.Yearly &&
                item.ScheduleType != MaintenanceLogScheduleType.BiAnnual).ToList();
            var quarterly = ordered.Where(item => item.ScheduleType == MaintenanceLogScheduleType.Quarterly).ToList();
            var annual = ordered.Where(item => item.ScheduleType == MaintenanceLogScheduleType.Yearly).ToList();
            var biAnnual = ordered.Where(item => item.ScheduleType == MaintenanceLogScheduleType.BiAnnual).ToList();

            var viewModel = new MaintenanceLogsIndexViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = canManage,
                Filters = filters,
                AdditionalHistoryBlocks = historyBlocks,
                DailyTemplates = daily,
                WeeklyTemplates = weekly,
                MonthlyTemplates = monthly,
                QuarterlyTemplates = quarterly,
                AnnualTemplates = annual,
                BiAnnualTemplates = biAnnual,
                OtherTemplates = other,
                CanLoadMoreHistory = historyBlocks < 12
            };

            ViewBag.MaintenanceLogMessage = TempData["MaintenanceLogMessage"];
            ViewBag.MaintenanceLogError = TempData["MaintenanceLogError"];

            return View(LogsIndexView, viewModel);
        }
        [HttpGet("EmergencyExitLights")]
        public async Task<IActionResult> EmergencyExitLights()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to view maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = UserCanManage(roles);
            ViewBag.LocalNow = _timeZoneService.ConvertToUserTime(DateTime.UtcNow);
            ViewBag.EmergencyLightLogMessage = TempData["EmergencyLightLogMessage"];
            ViewBag.EmergencyLightLogError = TempData["EmergencyLightLogError"];

            var viewModel = await BuildEmergencyLightLogViewModelAsync(property, canManage);
            return View(EmergencyLightLogView, viewModel);
        }

        [HttpPost("EmergencyExitLights/Record")]
        public async Task<IActionResult> RecordEmergencyExitLightTest(EmergencyLightTestEntryInputModel input)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EmergencyLightLogError"] = "Select a property before recording light testing.";
                return RedirectToAction(nameof(EmergencyExitLights));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                TempData["EmergencyLightLogError"] = "You do not have permission to record testing.";
                return RedirectToAction(nameof(EmergencyExitLights));
            }

            var resolvedLocation = input.Location?.Trim();
            if (string.IsNullOrWhiteSpace(resolvedLocation))
            {
                TempData["EmergencyLightLogError"] = "Enter a location before recording testing.";
                return RedirectToAction(nameof(EmergencyExitLights));
            }

            if (resolvedLocation.Length > 160)
            {
                resolvedLocation = resolvedLocation[..160];
            }

            if (!input.TestDate.HasValue)
            {
                TempData["EmergencyLightLogError"] = "Select the testing date.";
                return RedirectToAction(nameof(EmergencyExitLights));
            }

            var localDate = input.TestDate.Value.Date;
            var userTimeZone = _timeZoneService.GetTimeZone();
            var localDateTime = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
            var testedAtUtc = TimeZoneInfo.ConvertTimeToUtc(localDateTime, userTimeZone);

            var entry = new EmergencyLightTestEntry
            {
                PropertyId = property.Id,
                Location = resolvedLocation,
                TestedAtUtc = testedAtUtc,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = user.Id
            };

            _db.EmergencyLightTestEntries.Add(entry);
            await _db.SaveChangesAsync();

            var localDisplayDate = _timeZoneService.ConvertToUserTime(entry.TestedAtUtc).ToString("MMM d, yyyy");
            TempData["EmergencyLightLogMessage"] = $"Logged Emergency/Exit Light testing for {resolvedLocation} on {localDisplayDate}.";

            return RedirectToAction(nameof(EmergencyExitLights));
        }
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before creating a maintenance log.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var weeklyDefaults = new bool[7];
            weeklyDefaults[(int)DayOfWeek.Monday] = true;
            var viewModel = new MaintenanceLogCreateViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                WeeklyDays = weeklyDefaults
            };

            return View(LogsCreateView, viewModel);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(
            MaintenanceLogCreateViewModel viewModel)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before creating a maintenance log.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            viewModel.PropertyId = property.Id;
            viewModel.PropertyName = property.Name;
            viewModel.WeeklyDays ??= new bool[7];
            var trimmedName = viewModel.Name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ModelState.AddModelError(nameof(viewModel.Name), "Enter a name for the log.");
            }

            if (viewModel.ScheduleType == MaintenanceLogScheduleType.None)
            {
                ModelState.AddModelError(nameof(viewModel.ScheduleType), "Select a cycle type.");
            }

            if (viewModel.ScheduleType == MaintenanceLogScheduleType.Weekly)
            {
                var weeklySelection = GetSelectedDays(viewModel.WeeklyDays);
                if (!weeklySelection.Any())
                {
                    ModelState.AddModelError("WeeklyDays", "Select at least one day for weekly logs.");
                }
            }

            if (RequiresDayOfMonth(viewModel.ScheduleType) && !viewModel.DayOfMonth.HasValue)
            {
                ModelState.AddModelError(nameof(viewModel.DayOfMonth), "Enter the due day for this schedule.");
            }

            if (!ModelState.IsValid)
            {
                return View(LogsCreateView, viewModel);
            }

            var maxDisplayOrder = await _db.MaintenanceLogTemplates
                .Where(t => t.PropertyId == property.Id)
                .OrderByDescending(t => t.DisplayOrder)
                .Select(t => (int?)t.DisplayOrder)
                .FirstOrDefaultAsync() ?? -1;

            var template = new MaintenanceLogTemplate
            {
                Name = trimmedName!,
                PropertyId = property.Id,
                ScheduleType = viewModel.ScheduleType,
                WeeklyDaysBitmask = viewModel.ScheduleType == MaintenanceLogScheduleType.Weekly
                    ? MaintenanceLogTemplateHelper.BuildWeeklyBitmask(GetSelectedDays(viewModel.WeeklyDays))
                    : 0,
                DayOfMonth = RequiresDayOfMonth(viewModel.ScheduleType)
                    ? Math.Clamp(viewModel.DayOfMonth!.Value, 1, 31)
                    : null,
                DueTimeLocal = viewModel.DueTimeLocal,
                IsActive = true,
                DisplayOrder = maxDisplayOrder + 1,
                ColumnsJson = MaintenanceLogTemplateHelper.BuildColumnsJson(BuildDefaultColumns()),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.MaintenanceLogTemplates.Add(template);
            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Maintenance log created.";
            return RedirectToAction(nameof(Detail), new { id = template.Id });
        }

        [HttpGet("{id:int}/Checklist")]
        public async Task<IActionResult> Checklist(int id, string? returnUrl = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to manage checklists.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var viewModel = new MaintenanceLogChecklistViewModel
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                PropertyName = property.Name,
                ChecklistFileName = template.ChecklistFileName ?? Path.GetFileName(template.ChecklistFilePath ?? string.Empty),
                ChecklistFilePath = template.ChecklistFilePath,
                ChecklistFileSizeBytes = template.ChecklistFileSizeBytes,
                ReturnUrl = ResolveReturnUrl(returnUrl)
            };

            return View(LogsChecklistView, viewModel);
        }

        [HttpPost("{id:int}/Checklist")]
        public async Task<IActionResult> Checklist(
            int id,
            MaintenanceLogChecklistViewModel viewModel,
            IFormFile? checklistFile = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to manage checklists.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            viewModel.TemplateId = template.Id;
            viewModel.TemplateName = template.Name;
            viewModel.PropertyName = property.Name;
            viewModel.ChecklistFileName = template.ChecklistFileName ?? Path.GetFileName(template.ChecklistFilePath ?? string.Empty);
            viewModel.ChecklistFilePath = template.ChecklistFilePath;
            viewModel.ChecklistFileSizeBytes = template.ChecklistFileSizeBytes;
            viewModel.ReturnUrl = ResolveReturnUrl(viewModel.ReturnUrl);

            if (viewModel.RemoveChecklist && checklistFile is { Length: > 0 })
            {
                ModelState.AddModelError(nameof(viewModel.RemoveChecklist), "Choose either remove or upload, not both.");
            }

            var checklistValidationError = ValidateChecklistFile(checklistFile);
            if (!string.IsNullOrEmpty(checklistValidationError))
            {
                ModelState.AddModelError("ChecklistFile", checklistValidationError);
            }

            if (!ModelState.IsValid)
            {
                return View(LogsChecklistView, viewModel);
            }

            var updated = false;
            if (viewModel.RemoveChecklist)
            {
                if (!string.IsNullOrWhiteSpace(template.ChecklistFilePath))
                {
                    DeleteChecklistFile(template.ChecklistFilePath);
                }

                template.ChecklistFilePath = null;
                template.ChecklistFileName = null;
                template.ChecklistFileSizeBytes = null;
                updated = true;
            }
            else if (checklistFile is { Length: > 0 })
            {
                if (!string.IsNullOrWhiteSpace(template.ChecklistFilePath))
                {
                    DeleteChecklistFile(template.ChecklistFilePath);
                }

                var savedChecklist = await SaveChecklistFileAsync(checklistFile);
                template.ChecklistFilePath = savedChecklist.FilePath;
                template.ChecklistFileName = savedChecklist.OriginalFileName;
                template.ChecklistFileSizeBytes = savedChecklist.FileSizeBytes;
                updated = true;
            }

            if (updated)
            {
                template.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["MaintenanceLogMessage"] = viewModel.RemoveChecklist
                    ? "Checklist removed."
                    : "Checklist updated.";
            }
            else
            {
                TempData["MaintenanceLogMessage"] = "No checklist changes were made.";
            }

            return RedirectToSafeReturn(viewModel.ReturnUrl);
        }
        [HttpGet("{id:int}")]
        public IActionResult Detail(int id, string? windowKey = null, int history = 0)
        {
            return RedirectToAction(nameof(Cycle), new { templateId = id, windowKey, history });
        }
        [HttpGet("{id:int}/Export.csv")]
        public IActionResult Export(int id, DateTime? start = null, DateTime? end = null)
        {
            return NotFound();
        }
        [HttpGet("{templateId:int}/Cycle")]
        public async Task<IActionResult> Cycle(int templateId, string? windowKey = null, int history = 0)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to view maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = UserCanManage(roles);

            var template = await _db.MaintenanceLogTemplates
                .Include(t => t.Property)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            if (!SupportsCycleRendering(template.ScheduleType))
            {
                TempData["MaintenanceLogError"] = "Cycle-based tracking currently supports daily, weekly, monthly, bi-annual, quarterly, or annual logs.";
                return RedirectToAction(nameof(Index));
            }

            var historyBlocks = Math.Clamp(history, 0, 12);
            var timeZone = _timeZoneService.GetTimeZone();
            var detailResult = await BuildCycleDetailViewResultAsync(template, timeZone, canManage, historyBlocks, windowKey);

            if (detailResult.ViewModel == null)
            {
                TempData["MaintenanceLogError"] = "No cycle history is available for this template.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.HistoryBlocks = historyBlocks;
            ViewBag.AllCycles = detailResult.AllCycles;
            ViewBag.TemplateId = template.Id;
            ViewBag.PropertyName = property.Name;
            ViewBag.MaintenanceLogMessage = TempData["MaintenanceLogMessage"];
            ViewBag.MaintenanceLogError = TempData["MaintenanceLogError"];

            return View(LogsDetailView, detailResult.ViewModel);
        }

        [HttpPost("{templateId:int}/Cycles/{windowKey}/Complete")]
        public async Task<IActionResult> CreateCycleCompletion(
            int templateId,
            string windowKey,
            MaintenanceLogCycleCompletionInputModel input,
            int history = 0)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to record maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .Include(t => t.Property)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.PropertyId == property.Id);

            if (template == null)
            {
                return NotFound();
            }

            if (!SupportsCycleRendering(template.ScheduleType))
            {
                TempData["MaintenanceLogError"] = "Cycle-based tracking currently supports daily, weekly, monthly, bi-annual, quarterly, or annual logs.";
                return RedirectToAction(nameof(Index));
            }

            input.TemplateId = template.Id;
            input.WindowKey = windowKey;

            var timeZone = _timeZoneService.GetTimeZone();
            var historyBlocks = Math.Clamp(history, 0, 12);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var completedLocal = input.CompletedAtLocal ?? localNow;
            var targetWindow = _cycleService.BuildWindowForDate(template, completedLocal);
            var attachmentsToAdd = CollectCompletionAttachments(Request.Form.Files, "CompletionAttachments");
            var pendingCycleKey = targetWindow.WindowKey;

            if (completedLocal > localNow.AddMinutes(1))
            {
                ModelState.AddModelError(nameof(input.CompletedAtLocal), "You cannot record completions for future cycles.");
            }

            if (attachmentsToAdd.Count > MaxCompletionAttachments)
            {
                ModelState.AddModelError("CompletionAttachments", $"Upload up to {MaxCompletionAttachments} files per completion.");
            }

            if (!targetWindow.WindowKey.Equals(windowKey, StringComparison.OrdinalIgnoreCase) && !input.ConfirmCycleChange)
            {
                ModelState.AddModelError(nameof(input.CompletedAtLocal), "The selected date falls into a different cycle. Submit again to confirm.");
                ViewBag.CycleChangeTargetKey = pendingCycleKey;
            }

            if (!ModelState.IsValid)
            {
                var detailResult = await BuildCycleDetailViewResultAsync(template, timeZone, true, historyBlocks, windowKey, input, null);
                ViewBag.HistoryBlocks = historyBlocks;
                ViewBag.AllCycles = detailResult.AllCycles;
                ViewBag.TemplateId = template.Id;
                ViewBag.PropertyName = property.Name;
                return View(LogsDetailView, detailResult.ViewModel);
            }

            var completion = new MaintenanceLogCycleCompletion
            {
                TemplateId = template.Id,
                CycleWindowKey = targetWindow.WindowKey,
                ScheduleType = template.ScheduleType,
                CycleStartLocal = targetWindow.StartLocal,
                CycleEndLocal = targetWindow.EndLocal,
                CycleDueLocal = targetWindow.DueLocal,
                Result = input.Result,
                CompletedAtUtc = ConvertLocalToUtc(completedLocal, timeZone),
                CompletedByUserId = user.Id,
                DurationMinutes = input.DurationMinutes,
                Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var savedAttachments = await SaveCompletionAttachmentsAsync(attachmentsToAdd);
            foreach (var attachment in savedAttachments)
            {
                completion.Attachments.Add(attachment);
            }

            _db.MaintenanceLogCycleCompletions.Add(completion);
            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Cycle completion recorded.";
            return RedirectToAction(nameof(Cycle), new { templateId = template.Id, windowKey = targetWindow.WindowKey, history = historyBlocks });
        }

        [HttpPost("{templateId:int}/Cycles/{windowKey}/Completions/{completionId:int}/Edit")]
        public async Task<IActionResult> EditCycleCompletion(
            int templateId,
            string windowKey,
            int completionId,
            MaintenanceLogCycleCompletionInputModel input,
            int history = 0)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to edit maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .Include(t => t.Property)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var completion = await _db.MaintenanceLogCycleCompletions
                .Include(c => c.Attachments)
                .FirstOrDefaultAsync(c => c.Id == completionId && c.TemplateId == template.Id);

            if (completion == null)
            {
                return NotFound();
            }

            if (!await IsLatestCompletionAsync(template.Id, completion.CycleWindowKey, completion.Id))
            {
                TempData["MaintenanceLogError"] = "Only the latest completion in a cycle can be edited.";
                return RedirectToAction(nameof(Cycle), new { templateId = template.Id, windowKey, history });
            }

            input.TemplateId = template.Id;
            input.WindowKey = windowKey;
            input.CompletionId = completionId;

            var timeZone = _timeZoneService.GetTimeZone();
            var historyBlocks = Math.Clamp(history, 0, 12);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var completedLocal = input.CompletedAtLocal ?? localNow;
            var targetWindow = _cycleService.BuildWindowForDate(template, completedLocal);

            var attachmentsToAdd = CollectCompletionAttachments(Request.Form.Files, "EditCompletionAttachments");
            var attachmentsToRemove = completion.Attachments
                .Where(attachment => input.RemoveAttachmentIds.Contains(attachment.Id))
                .ToList();

            var resultingAttachmentCount = completion.Attachments.Count - attachmentsToRemove.Count + attachmentsToAdd.Count;
            if (resultingAttachmentCount > MaxCompletionAttachments)
            {
                ModelState.AddModelError("EditCompletionAttachments", $"Upload up to {MaxCompletionAttachments} files per completion.");
            }

            if (completedLocal > localNow.AddMinutes(1))
            {
                ModelState.AddModelError(nameof(input.CompletedAtLocal), "You cannot record completions for future cycles.");
            }

            if (!targetWindow.WindowKey.Equals(windowKey, StringComparison.OrdinalIgnoreCase) && !input.ConfirmCycleChange)
            {
                ModelState.AddModelError(nameof(input.CompletedAtLocal), "The selected date falls into a different cycle. Submit again to confirm.");
                ViewBag.CycleChangeTargetKey = targetWindow.WindowKey;
            }

            if (!ModelState.IsValid)
            {
                var detailResult = await BuildCycleDetailViewResultAsync(template, timeZone, true, historyBlocks, windowKey, null, input);
                ViewBag.HistoryBlocks = historyBlocks;
                ViewBag.AllCycles = detailResult.AllCycles;
                ViewBag.TemplateId = template.Id;
                ViewBag.PropertyName = property.Name;
                return View(LogsDetailView, detailResult.ViewModel);
            }

            foreach (var attachment in attachmentsToRemove)
            {
                DeleteCompletionAttachmentFile(attachment.FilePath);
                _db.MaintenanceLogCompletionAttachments.Remove(attachment);
            }

            var newAttachments = await SaveCompletionAttachmentsAsync(attachmentsToAdd);
            foreach (var attachment in newAttachments)
            {
                completion.Attachments.Add(attachment);
            }

            completion.CycleWindowKey = targetWindow.WindowKey;
            completion.ScheduleType = template.ScheduleType;
            completion.CycleStartLocal = targetWindow.StartLocal;
            completion.CycleEndLocal = targetWindow.EndLocal;
            completion.CycleDueLocal = targetWindow.DueLocal;
            completion.CompletedAtUtc = ConvertLocalToUtc(completedLocal, timeZone);
            completion.CompletedByUserId = user.Id;
            completion.DurationMinutes = input.DurationMinutes;
            completion.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
            completion.Result = input.Result;
            completion.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Cycle completion updated.";
            return RedirectToAction(nameof(Cycle), new { templateId = template.Id, windowKey = targetWindow.WindowKey, history = historyBlocks });
        }

        [HttpPost("{templateId:int}/Cycles/{windowKey}/Completions/{completionId:int}/Delete")]
        public async Task<IActionResult> DeleteCycleCompletion(int templateId, string windowKey, int completionId, int history = 0)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to edit maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .Include(t => t.Property)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var completion = await _db.MaintenanceLogCycleCompletions
                .Include(c => c.Attachments)
                .FirstOrDefaultAsync(c => c.Id == completionId && c.TemplateId == template.Id);
            if (completion == null)
            {
                return NotFound();
            }

            if (!await IsLatestCompletionAsync(template.Id, completion.CycleWindowKey, completion.Id))
            {
                TempData["MaintenanceLogError"] = "Only the latest completion in a cycle can be deleted.";
                return RedirectToAction(nameof(Cycle), new { templateId = template.Id, windowKey, history });
            }

            foreach (var attachment in completion.Attachments)
            {
                DeleteCompletionAttachmentFile(attachment.FilePath);
            }

            _db.MaintenanceLogCycleCompletions.Remove(completion);
            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Cycle completion removed.";
            return RedirectToAction(nameof(Cycle), new { templateId = template.Id, windowKey, history });
        }

        [HttpPost("{id:int}/Entries")]
        public IActionResult CreateEntry(int id)
        {
            return NotFound();
        }

        [HttpPost("{templateId:int}/Entries/{entryId:int}/Delete")]
        public IActionResult DeleteEntry(int templateId, int entryId)
        {
            return NotFound();
        }
        [HttpGet("{id:int}/Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before editing maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var columns = MaintenanceLogTemplateHelper.ParseColumns(template.ColumnsJson);
            var viewModel = new MaintenanceLogTemplateEditorViewModel
            {
                Id = template.Id,
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = true,
                Name = template.Name,
                ScheduleType = template.ScheduleType,
                DayOfMonth = template.DayOfMonth,
                WeeklyDays = BuildWeeklySelection(template.WeeklyDaysBitmask),
                DueTimeLocal = template.DueTimeLocal,
                IsActive = template.IsActive,
                Columns = BuildColumnEditors(columns),
                ChecklistFilePath = template.ChecklistFilePath,
                ChecklistFileName = template.ChecklistFileName,
                ChecklistFileSizeBytes = template.ChecklistFileSizeBytes
            };

            return View(LogsEditorView, viewModel);
        }

        [HttpPost("{id:int}/Edit")]
        public async Task<IActionResult> Edit(
            int id,
            MaintenanceLogTemplateEditorViewModel viewModel,
            IFormFile? templateCsvFile = null,
            IFormFile? checklistFile = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before editing maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            viewModel.Id = id;
            viewModel.PropertyId = property.Id;
            viewModel.PropertyName = property.Name;
            viewModel.CanManage = true;

            if (templateCsvFile is { Length: > 0 } && (viewModel.Columns == null || !viewModel.Columns.Any()))
            {
                try
                {
                    viewModel.Columns = await ParseTemplateColumnsAsync(templateCsvFile);
                    if (!viewModel.Columns.Any())
                    {
                        ModelState.AddModelError("TemplateCsvFile", "The CSV file did not include any columns.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("TemplateCsvFile", ex.Message);
                }
            }

            var sanitizedColumns = BuildColumnDefinitions(viewModel);
            if (!sanitizedColumns.Any())
            {
                ModelState.AddModelError(string.Empty, "Add at least one column to capture log entries.");
            }

            var checklistValidationError = ValidateChecklistFile(checklistFile);
            if (!string.IsNullOrEmpty(checklistValidationError))
            {
                ModelState.AddModelError("ChecklistFile", checklistValidationError);
            }

            if (!ModelState.IsValid)
            {
                return View(LogsEditorView, viewModel);
            }

            template.Name = viewModel.Name.Trim();
            template.ScheduleType = viewModel.ScheduleType;
            template.WeeklyDaysBitmask = viewModel.ScheduleType == MaintenanceLogScheduleType.Weekly
                ? MaintenanceLogTemplateHelper.BuildWeeklyBitmask(GetSelectedDays(viewModel))
                : 0;
            template.DayOfMonth = RequiresDayOfMonth(viewModel.ScheduleType) ? viewModel.DayOfMonth : null;
            template.DueTimeLocal = viewModel.DueTimeLocal;
            template.IsActive = viewModel.IsActive;
            template.ColumnsJson = MaintenanceLogTemplateHelper.BuildColumnsJson(sanitizedColumns);
            template.UpdatedAtUtc = DateTime.UtcNow;

            if (viewModel.RemoveChecklistFile)
            {
                DeleteChecklistFile(template.ChecklistFilePath);
                template.ChecklistFilePath = null;
                template.ChecklistFileName = null;
                template.ChecklistFileSizeBytes = null;
            }
            else if (checklistFile is { Length: > 0 })
            {
                DeleteChecklistFile(template.ChecklistFilePath);
                var savedChecklist = await SaveChecklistFileAsync(checklistFile);
                template.ChecklistFilePath = savedChecklist.FilePath;
                template.ChecklistFileName = savedChecklist.OriginalFileName;
                template.ChecklistFileSizeBytes = savedChecklist.FileSizeBytes;
            }

            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Maintenance log template updated.";
            return RedirectToAction(nameof(Detail), new { id });
        }
[HttpPost("Reorder")]
        public async Task<IActionResult> Reorder([FromBody] MaintenanceLogTemplateReorderRequest? request)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { error = "Select a property before reordering templates." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            if (request?.TemplateIds == null || request.TemplateIds.Count == 0)
            {
                return BadRequest(new { error = "Provide at least one template when reordering." });
            }

            var templates = await _db.MaintenanceLogTemplates
                .Where(t => t.PropertyId == property.Id)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();

            if (!templates.Any())
            {
                return BadRequest(new { error = "No templates exist for this property." });
            }

            var propertyTemplateIds = templates.Select(t => t.Id).ToHashSet();
            var invalidIds = request.TemplateIds.Where(id => !propertyTemplateIds.Contains(id)).ToList();
            if (invalidIds.Any())
            {
                return BadRequest(new { error = "One or more templates are invalid or belong to another property." });
            }

            var orderLookup = request.TemplateIds
                .Select((id, index) => (id, index))
                .GroupBy(item => item.id)
                .ToDictionary(group => group.Key, group => group.First().index);

            var nextOrder = orderLookup.Count;
            foreach (var template in templates)
            {
                if (orderLookup.TryGetValue(template.Id, out var order))
                {
                    template.DisplayOrder = order;
                }
                else
                {
                    template.DisplayOrder = nextOrder++;
                }

                template.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet("Template/Download.csv")]
        public IActionResult DownloadTemplateCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Label,Type,Required,Options,Notes,Photos");
            builder.AppendLine("\"Area\",\"text\",\"Yes\",,\"No\",\"No\"");
            builder.AppendLine("\"Status\",\"select\",\"Yes\",\"Operational,Out of Service\",\"No\",\"No\"");
            builder.AppendLine("\"Notes\",\"text\",\"No\",,\"Yes\",\"Yes\"");

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv", "maintenance-log-template.csv");
        }

        [HttpPost("Template/Columns/Preview")]
        public async Task<IActionResult> PreviewTemplateCsv(IFormFile? csvFile)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { error = "Select a property before importing columns." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest(new { error = "Choose a CSV file to import." });
            }

            try
            {
                var columns = await ParseTemplateColumnsAsync(csvFile);
                if (!columns.Any())
                {
                    return BadRequest(new { error = "No columns were found in the uploaded template." });
                }

                return Json(new
                {
                    columns = columns.Select(column => new
                    {
                        key = column.Key,
                        label = column.Label,
                        type = column.Type,
                        required = column.Required,
                        optionsText = column.OptionsText,
                        includeNotes = column.IncludeNotes,
                        includePhotos = column.IncludePhotos
                    })
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private async Task<List<MaintenanceLogColumnEditorViewModel>> ParseTemplateColumnsAsync(IFormFile csvFile)
        {
            var rows = new List<MaintenanceLogColumnEditorViewModel>();
            using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            var lineNumber = 0;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (rows.Count >= TemplateColumnImportLimit)
                {
                    throw new InvalidOperationException($"Templates can include up to {TemplateColumnImportLimit} columns.");
                }

                var cells = SplitCsvLine(line);
                if (lineNumber == 1 && cells.Count > 0 && cells[0].Trim().Equals("label", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var label = cells.Count > 0 ? cells[0].Trim() : string.Empty;
                var key = string.Empty;
                var index = 1;

                if (cells.Count > index)
                {
                    var candidate = cells[index].Trim();
                    var normalizedCandidate = candidate.ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(candidate) &&
                        !MaintenanceLogColumnDefinition.AllowedTypes.Contains(normalizedCandidate))
                    {
                        key = candidate;
                        index++;
                    }
                }

                var typeCell = cells.Count > index ? cells[index++].Trim() : string.Empty;
                var requiredCell = cells.Count > index ? cells[index++].Trim() : string.Empty;
                var optionsCell = cells.Count > index ? cells[index++] : string.Empty;
                var notesCell = cells.Count > index ? cells[index++].Trim() : string.Empty;
                var photosCell = cells.Count > index ? cells[index++].Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(label) && label.Length > 160)
                {
                    label = label[..160];
                }

                if (!string.IsNullOrWhiteSpace(key) && key.Length > 64)
                {
                    key = key[..64];
                }

                var normalizedType = string.IsNullOrWhiteSpace(typeCell)
                    ? MaintenanceLogColumnDefinition.DefaultColumnType
                    : typeCell.ToLowerInvariant();

                if (!MaintenanceLogColumnDefinition.AllowedTypes.Contains(normalizedType))
                {
                    throw new InvalidOperationException($"\"{typeCell}\" is not a supported column type (row {lineNumber}).");
                }

                var optionsText = string.Empty;
                if (normalizedType == "select")
                {
                    optionsText = string.Join(
                        Environment.NewLine,
                        MaintenanceLogTemplateHelper.ParseOptions(NormalizeOptionsCell(optionsCell)));
                }

                rows.Add(new MaintenanceLogColumnEditorViewModel
                {
                    Label = label,
                    Key = key,
                    Type = normalizedType,
                    Required = ParseBooleanCell(requiredCell),
                    OptionsText = optionsText,
                    IncludeNotes = ParseBooleanCell(notesCell),
                    IncludePhotos = ParseBooleanCell(photosCell)
                });
            }

            return rows;
        }

        private static bool ParseBooleanCell(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            return normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("y", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("required", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeOptionsCell(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text
                .Replace('|', '\n')
                .Replace(';', '\n');
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

        private async Task<bool> UserCanManageAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return UserCanManage(roles);
        }

        private async Task<EmergencyLightTestingIndexViewModel> BuildEmergencyLightLogViewModelAsync(Property property, bool canRecord)
        {
            var localToday = _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;

            var lastEntryIdResults = await _db.EmergencyLightTestEntries
                .Where(e => e.PropertyId == property.Id)
                .GroupBy(e => e.Location)
                .Select(group => new
                {
                    Location = group.Key,
                    EntryId = group
                        .OrderByDescending(item => item.TestedAtUtc)
                        .ThenByDescending(item => item.CreatedAtUtc)
                        .Select(item => item.Id)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var entryIds = lastEntryIdResults
                .Select(result => result.EntryId)
                .Where(id => id > 0)
                .ToList();

            var lastEntries = entryIds.Count > 0
                ? await _db.EmergencyLightTestEntries
                    .Where(e => entryIds.Contains(e.Id))
                    .Include(e => e.CreatedByUser)
                    .ToListAsync()
                : new List<EmergencyLightTestEntry>();

            var lastEntryLookup = new Dictionary<string, EmergencyLightTestEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in lastEntries)
            {
                var key = entry.Location?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }
                lastEntryLookup[key] = entry;
            }

            var statuses = lastEntryLookup
                .Select(kvp => BuildEmergencyLightLocationStatus(kvp.Key, kvp.Value, localToday))
                .OrderBy(status => status.Location, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var savedLocations = statuses
                .Select(status => status.Location)
                .Where(location => !string.IsNullOrWhiteSpace(location))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var recentEntries = await _db.EmergencyLightTestEntries
                .Where(e => e.PropertyId == property.Id)
                .OrderByDescending(e => e.TestedAtUtc)
                .ThenByDescending(e => e.CreatedAtUtc)
                .Take(50)
                .Include(e => e.CreatedByUser)
                .ToListAsync();

            var recentEntryModels = recentEntries.Select(e => new EmergencyLightTestEntryViewModel
            {
                Id = e.Id,
                Location = e.Location,
                TestDate = e.TestedAtUtc,
                CreatedAtUtc = e.CreatedAtUtc,
                CreatedByName = BuildUserName(e.CreatedByUser)
            }).ToList();

            return new EmergencyLightTestingIndexViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanRecord = canRecord,
                LocationStatuses = statuses,
                RecentEntries = recentEntryModels,
                SavedLocations = savedLocations
            };
        }

        private EmergencyLightTestLocationStatusViewModel BuildEmergencyLightLocationStatus(string location, EmergencyLightTestEntry? entry, DateTime todayLocal)
        {
            DateTime? lastLocal = entry != null ? _timeZoneService.ConvertToUserTime(entry.TestedAtUtc).Date : null;
            DateTime? nextDueLocal = lastLocal?.AddMonths(1);
            var isOverdue = !nextDueLocal.HasValue || nextDueLocal.Value < todayLocal;
            var isDueSoon = nextDueLocal.HasValue && !isOverdue && nextDueLocal.Value <= todayLocal.AddDays(7);

            return new EmergencyLightTestLocationStatusViewModel
            {
                Location = location,
                LastTestDate = lastLocal,
                NextDueDate = nextDueLocal,
                IsOverdue = isOverdue,
                IsDueSoon = isDueSoon,
                LastTestedBy = entry != null ? BuildUserName(entry.CreatedByUser) : null
            };
        }

        private string ResolveReturnUrl(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return Url.Action(nameof(Index)) ?? "/Maintenance/Logs";
        }

        private IActionResult RedirectToSafeReturn(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        private static bool SupportsCycleRendering(MaintenanceLogScheduleType scheduleType)
        {
            return scheduleType is MaintenanceLogScheduleType.Daily
                or MaintenanceLogScheduleType.Weekly
                or MaintenanceLogScheduleType.Monthly
                or MaintenanceLogScheduleType.Quarterly
                or MaintenanceLogScheduleType.Yearly
                or MaintenanceLogScheduleType.BiAnnual;
        }

        private static bool PassesFilters(MaintenanceLogTemplateListItemViewModel item, MaintenanceLogIndexFilterViewModel filters)
        {
            if (filters.ScheduleFilter.HasValue && item.ScheduleType != filters.ScheduleFilter.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.StatusFilter))
            {
                if (!MatchesStatus(item.LatestStatus, filters.StatusFilter))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(filters.CompletionFilter))
            {
                var normalized = filters.CompletionFilter.Trim().ToLowerInvariant();
                var isCompleted = item.VisibleCycles.FirstOrDefault()?.LatestCompletion != null;
                if (normalized == "completed" && !isCompleted)
                {
                    return false;
                }

                if (normalized is "notcompleted" or "pending" && isCompleted)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(filters.NameQuery) &&
                item.Name.IndexOf(filters.NameQuery, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesStatus(MaintenanceLogCycleStatusKind status, string filter)
        {
            var normalized = filter.Trim().ToLowerInvariant();
            return normalized switch
            {
                "passed" => status == MaintenanceLogCycleStatusKind.Passed,
                "failed" => status == MaintenanceLogCycleStatusKind.Failed,
                "due" => status == MaintenanceLogCycleStatusKind.Due,
                "overdue" => status == MaintenanceLogCycleStatusKind.Overdue,
                "upcoming" => status == MaintenanceLogCycleStatusKind.Upcoming,
                _ => true
            };
        }

        private static bool UserCanManage(IList<string> roles)
        {
            return roles.Any(role =>
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<DayOfWeek> GetSelectedDays(MaintenanceLogTemplateEditorViewModel viewModel)
        {
            return GetSelectedDays(viewModel.WeeklyDays);
        }

        private static List<DayOfWeek> GetSelectedDays(bool[]? selections)
        {
            var days = new List<DayOfWeek>();
            if (selections == null || selections.Length == 0)
            {
                return days;
            }

            var limit = Math.Min(selections.Length, 7);
            for (var index = 0; index < limit; index++)
            {
                if (selections[index])
                {
                    days.Add((DayOfWeek)index);
                }
            }

            return days;
        }

        private static bool[] BuildWeeklySelection(int bitmask)
        {
            var selection = new bool[7];
            var days = MaintenanceLogTemplateHelper.ParseWeeklyBitmask(bitmask);
            foreach (var day in days)
            {
                selection[(int)day] = true;
            }

            return selection;
        }

        private static bool RequiresDayOfMonth(MaintenanceLogScheduleType scheduleType)
        {
            return scheduleType == MaintenanceLogScheduleType.Monthly
                || scheduleType == MaintenanceLogScheduleType.Quarterly
                || scheduleType == MaintenanceLogScheduleType.Yearly
                || scheduleType == MaintenanceLogScheduleType.BiAnnual;
        }

        private static List<MaintenanceLogColumnEditorViewModel> BuildColumnEditors(IReadOnlyList<MaintenanceLogColumnDefinition> columns)
        {
            if (columns.Count == 0)
            {
                return new List<MaintenanceLogColumnEditorViewModel>
                {
                    new MaintenanceLogColumnEditorViewModel()
                };
            }

            return columns
                .Select(column => new MaintenanceLogColumnEditorViewModel
                {
                    Key = column.Key,
                    Label = column.Label,
                    Type = column.Type,
                    Required = column.Required,
                    OptionsText = string.Join(Environment.NewLine, column.Options),
                    IncludeNotes = column.IncludeNotes,
                    IncludePhotos = column.IncludePhotos
                })
                .ToList();
        }

        private static List<MaintenanceLogColumnDefinition> BuildDefaultColumns()
        {
            return new List<MaintenanceLogColumnDefinition>
            {
                new MaintenanceLogColumnDefinition
                {
                    Key = "task",
                    Label = "Task / Item",
                    Required = true,
                    Type = MaintenanceLogColumnDefinition.DefaultColumnType
                },
                new MaintenanceLogColumnDefinition
                {
                    Key = "status",
                    Label = "Status",
                    Type = MaintenanceLogColumnDefinition.DefaultColumnType
                }
            };
        }

        private static List<MaintenanceLogColumnDefinition> BuildColumnDefinitions(MaintenanceLogTemplateEditorViewModel viewModel)
        {
            var results = new List<MaintenanceLogColumnDefinition>();
            if (viewModel.Columns == null)
            {
                return results;
            }

            foreach (var editor in viewModel.Columns)
            {
                if (string.IsNullOrWhiteSpace(editor.Label) && string.IsNullOrWhiteSpace(editor.Key))
                {
                    continue;
                }

                var key = string.IsNullOrWhiteSpace(editor.Key)
                    ? MaintenanceLogTemplateHelper.NormalizeColumnKey(editor.Label)
                    : editor.Key.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    key = MaintenanceLogTemplateHelper.NormalizeColumnKey($"column{results.Count + 1}");
                }

                results.Add(new MaintenanceLogColumnDefinition
                {
                    Key = key,
                    Label = string.IsNullOrWhiteSpace(editor.Label) ? key : editor.Label,
                    Type = string.IsNullOrWhiteSpace(editor.Type) ? MaintenanceLogColumnDefinition.DefaultColumnType : editor.Type,
                    Required = editor.Required,
                    Options = MaintenanceLogTemplateHelper.ParseOptions(editor.OptionsText),
                    IncludeNotes = editor.IncludeNotes,
                    IncludePhotos = editor.IncludePhotos
                });
            }

            return results;
        }

        private static DateTime ConvertLocalToUtc(DateTime localValue, TimeZoneInfo timeZone)
        {
            var unspecified = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
        }

        private bool TryCreateWindowFromKey(
            MaintenanceLogTemplate template,
            string? windowKey,
            out MaintenanceLogCycleWindow window)
        {
            window = MaintenanceLogCycleWindow.Empty;
            if (string.IsNullOrWhiteSpace(windowKey))
            {
                return false;
            }

            var parts = windowKey.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            var payload = parts[1];
            DateTime startLocal;
            switch (template.ScheduleType)
            {
                case MaintenanceLogScheduleType.Daily:
                case MaintenanceLogScheduleType.Weekly:
                    if (!DateTime.TryParseExact(payload, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startLocal))
                    {
                        return false;
                    }

                    break;
                case MaintenanceLogScheduleType.Monthly:
                    if (!DateTime.TryParseExact(payload, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out startLocal))
                    {
                        return false;
                    }

                    startLocal = new DateTime(startLocal.Year, startLocal.Month, 1);
                    break;
                default:
                    return false;
            }

            window = _cycleService.BuildWindowForDate(template, startLocal);
            return true;
        }

        private async Task<bool> IsLatestCompletionAsync(int templateId, string windowKey, int completionId)
        {
            var latest = await _db.MaintenanceLogCycleCompletions
                .Where(c => c.TemplateId == templateId && c.CycleWindowKey == windowKey)
                .OrderByDescending(c => c.CompletedAtUtc ?? c.CreatedAtUtc)
                .ThenByDescending(c => c.Id)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            return latest == completionId;
        }

        private async Task<IReadOnlyList<MaintenanceLogCycleHistoryItemViewModel>> BuildCycleHistoryAsync(
            MaintenanceLogTemplate template,
            TimeZoneInfo timeZone,
            int additionalPastBlocks,
            string? focusWindowKey = null)
        {
            var referenceUtc = DateTime.UtcNow;
            var windows = _cycleService.GetVisibleWindows(template, timeZone, referenceUtc, additionalPastBlocks)
                .ToList();

            if (!string.IsNullOrWhiteSpace(focusWindowKey) &&
                windows.All(window => !window.WindowKey.Equals(focusWindowKey, StringComparison.OrdinalIgnoreCase)) &&
                TryCreateWindowFromKey(template, focusWindowKey, out var focalWindow))
            {
                windows.Add(focalWindow);
            }

            if (windows.Count == 0)
            {
                return Array.Empty<MaintenanceLogCycleHistoryItemViewModel>();
            }

            var earliestStart = windows.Min(window => window.StartLocal);
            var latestEnd = windows.Max(window => window.EndLocal);

            var completions = await _db.MaintenanceLogCycleCompletions
                .Include(c => c.CompletedByUser)
                .Include(c => c.Attachments)
                .Where(c => c.TemplateId == template.Id &&
                    c.CycleStartLocal >= earliestStart &&
                    c.CycleEndLocal <= latestEnd)
                .OrderBy(c => c.CycleStartLocal)
                .ThenByDescending(c => c.CompletedAtUtc ?? c.CreatedAtUtc)
                .ToListAsync();

            var legacyEntries = await _db.MaintenanceLogEntries
                .Include(e => e.CreatedByUser)
                .Where(e => e.TemplateId == template.Id &&
                    e.CreatedAtUtc >= ConvertLocalToUtc(earliestStart, timeZone) &&
                    e.CreatedAtUtc <= ConvertLocalToUtc(latestEnd, timeZone))
                .ToListAsync();

            var summary = _cycleService.BuildCycleSummary(
                template,
                timeZone,
                referenceUtc,
                completions,
                legacyEntries,
                additionalPastBlocks);

            var legacyLookup = legacyEntries.ToDictionary(e => e.Id, e => BuildUserName(e.CreatedByUser));

            return summary.Statuses
                .Select(status =>
                {
                    var completionViewModels = status.Completions
                        .Select((completion, index) => new MaintenanceLogCycleCompletionSummaryViewModel
                        {
                            CompletionId = completion.Id,
                            Result = completion.Result,
                            CompletedAtUtc = completion.CompletedAtUtc,
                            CompletedAtLocal = completion.CompletedAtUtc.HasValue
                                ? TimeZoneInfo.ConvertTimeFromUtc(completion.CompletedAtUtc.Value, timeZone)
                                : null,
                            CompletedByUserId = completion.CompletedByUserId,
                            CompletedByName = BuildUserName(completion.CompletedByUser),
                            DurationMinutes = completion.DurationMinutes,
                            Notes = completion.Notes,
                            IsLatest = index == 0,
                            Attachments = completion.Attachments
                                .Select(attachment => new MaintenanceLogCycleAttachmentViewModel
                                {
                                    AttachmentId = attachment.Id,
                                    FilePath = attachment.FilePath,
                                    OriginalFileName = attachment.OriginalFileName,
                                    FileSizeBytes = attachment.FileSizeBytes,
                                    UploadedAtUtc = attachment.UploadedAtUtc
                                })
                                .ToList()
                        })
                        .ToList();

                    var legacyViewModels = status.LegacyEntries
                        .Select(entry => new MaintenanceLogLegacyEntryBridgeViewModel
                        {
                            EntryId = entry.EntryId,
                            CreatedAtUtc = entry.CreatedAtUtc,
                            CreatedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(entry.CreatedAtUtc, timeZone),
                            CreatedByName = legacyLookup.GetValueOrDefault(entry.EntryId)
                        })
                        .ToList();

                    return new MaintenanceLogCycleHistoryItemViewModel
                    {
                        WindowKey = status.Window.WindowKey,
                        StartLocal = status.Window.StartLocal,
                        EndLocal = status.Window.EndLocal,
                        DueLocal = status.Window.DueLocal,
                        Status = status.Status,
                        IsLate = status.IsLate,
                        Completions = completionViewModels,
                        LegacyEntries = legacyViewModels
                    };
                })
                .ToList();
        }

        private async Task<CycleDetailResult> BuildCycleDetailViewResultAsync(
            MaintenanceLogTemplate template,
            TimeZoneInfo timeZone,
            bool canManage,
            int historyBlocks,
            string? windowKey,
            MaintenanceLogCycleCompletionInputModel? completionOverride = null,
            MaintenanceLogCycleCompletionInputModel? editOverride = null)
        {
            var cycles = await BuildCycleHistoryAsync(template, timeZone, historyBlocks, windowKey);
            var ordered = cycles
                .OrderByDescending(cycle => cycle.StartLocal)
                .ToList();

            var selected = string.IsNullOrWhiteSpace(windowKey)
                ? ordered.FirstOrDefault()
                : ordered.FirstOrDefault(cycle => cycle.WindowKey.Equals(windowKey, StringComparison.OrdinalIgnoreCase));

            selected ??= ordered.FirstOrDefault();
            if (selected == null)
            {
                return new CycleDetailResult(null, ordered);
            }

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

            var completionForm = completionOverride ?? new MaintenanceLogCycleCompletionInputModel
            {
                TemplateId = template.Id,
                WindowKey = selected.WindowKey,
                CompletedAtLocal = localNow,
                Result = MaintenanceLogCompletionResult.Passed
            };

            if (!completionForm.CompletedAtLocal.HasValue)
            {
                completionForm.CompletedAtLocal = localNow;
            }

            completionForm.TemplateId = template.Id;
            if (string.IsNullOrWhiteSpace(completionForm.WindowKey))
            {
                completionForm.WindowKey = selected.WindowKey;
            }

            MaintenanceLogCycleCompletionInputModel? editForm = null;
            var latestCompletion = selected.LatestCompletion;
            if (latestCompletion != null)
            {
                var latestLocalTime = latestCompletion.CompletedAtUtc.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(latestCompletion.CompletedAtUtc.Value, timeZone)
                    : localNow;

                editForm = editOverride ?? new MaintenanceLogCycleCompletionInputModel
                {
                    TemplateId = template.Id,
                    WindowKey = selected.WindowKey,
                    CompletionId = latestCompletion.CompletionId,
                    CompletedAtLocal = latestLocalTime,
                    DurationMinutes = latestCompletion.DurationMinutes,
                    Notes = latestCompletion.Notes,
                    Result = latestCompletion.Result
                };

                if (!editForm.CompletedAtLocal.HasValue)
                {
                    editForm.CompletedAtLocal = latestLocalTime;
                }
            }

            var detail = new MaintenanceLogCycleDetailViewModel
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                ScheduleType = template.ScheduleType,
                ScheduleSummary = MaintenanceLogTemplateHelper.BuildScheduleSummary(template),
                ChecklistFilePath = template.ChecklistFilePath,
                CanManage = canManage,
                Cycle = selected,
                CompletionForm = completionForm,
                EditForm = editForm,
                PriorCompletions = selected.Completions.Skip(1).ToList()
            };

            return new CycleDetailResult(detail, ordered);
        }

        private sealed record CycleDetailResult(
            MaintenanceLogCycleDetailViewModel? ViewModel,
            IReadOnlyList<MaintenanceLogCycleHistoryItemViewModel> AllCycles);


        private Dictionary<string, List<IFormFile>> CollectPhotoUploads(IFormFileCollection files, IReadOnlyList<MaintenanceLogColumnDefinition> columns)
        {
            var uploads = new Dictionary<string, List<IFormFile>>(StringComparer.OrdinalIgnoreCase);
            if (files == null || files.Count == 0)
            {
                return uploads;
            }

            var allowedKeys = columns
                .Where(column => column.IncludePhotos)
                .Select(column => column.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!allowedKeys.Any())
            {
                return uploads;
            }

            foreach (var file in files)
            {
                var columnKey = ExtractPhotoColumnKey(file.Name);
                if (columnKey == null || !allowedKeys.Contains(columnKey))
                {
                    continue;
                }

                if (file.Length <= 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) ||
                    !AllowedPhotoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError($"Photos[{columnKey}]", "Upload JPG, PNG, GIF, BMP, or WebP images.");
                    continue;
                }

                if (!uploads.TryGetValue(columnKey, out var list))
                {
                    list = new List<IFormFile>();
                    uploads[columnKey] = list;
                }

                list.Add(file);
            }

            return uploads;
        }

        private async Task<List<string>> SavePhotoFilesAsync(IEnumerable<IFormFile> files)
        {
            var saved = new List<string>();
            var uploadRoot = Path.Combine(_environment.WebRootPath, PhotoUploadFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in files)
            {
                if (file.Length <= 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".jpg";
                }

                var sanitizedExtension = extension.StartsWith(".") ? extension : $".{extension}";
                var uniqueName = $"{Guid.NewGuid():N}{sanitizedExtension}";
                var physicalPath = Path.Combine(uploadRoot, uniqueName);

                using (var stream = System.IO.File.Create(physicalPath))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/{PhotoUploadFolder.Replace('\\', '/')}/{uniqueName}";
                saved.Add(relativePath);
            }

            return saved;
        }

        private string? ValidateChecklistFile(IFormFile? file)
        {
            if (file == null || file.Length <= 0)
            {
                return null;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedChecklistExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return "Upload a CSV or Excel file (.csv, .xlsx) for the checklist.";
            }

            if (file.Length > ChecklistFileMaxBytes)
            {
                var maxMb = ChecklistFileMaxBytes / (1024 * 1024);
                return $"Checklist files must be {maxMb} MB or smaller.";
            }

            return null;
        }

        private async Task<ChecklistFileResult> SaveChecklistFileAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".csv";
            }

            var sanitizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
                ? extension
                : $".{extension}";
            var uploadRoot = Path.Combine(_environment.WebRootPath, ChecklistUploadFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(uploadRoot);
            var uniqueName = $"{Guid.NewGuid():N}{sanitizedExtension}";
            var physicalPath = Path.Combine(uploadRoot, uniqueName);

            using (var stream = System.IO.File.Create(physicalPath))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/{ChecklistUploadFolder.Replace('\\', '/')}/{uniqueName}";
            return new ChecklistFileResult(relativePath, Path.GetFileName(file.FileName), file.Length);
        }

        private void DeleteChecklistFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var trimmed = relativePath.TrimStart('~').TrimStart('/');
            var physicalPath = Path.Combine(_environment.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
            {
                try
                {
                    System.IO.File.Delete(physicalPath);
                }
                catch
                {
                    // Ignore cleanup errors to avoid blocking template updates.
                }
            }
        }

        private List<IFormFile> CollectCompletionAttachments(IFormFileCollection files, string fieldPrefix)
        {
            var uploads = new List<IFormFile>();
            if (files == null || files.Count == 0)
            {
                return uploads;
            }

            var normalizedPrefix = string.IsNullOrWhiteSpace(fieldPrefix) ? "CompletionAttachments" : fieldPrefix;
            foreach (var file in files)
            {
                if (string.IsNullOrWhiteSpace(file.Name) ||
                    !file.Name.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (file.Length <= 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) ||
                    !AllowedCompletionAttachmentExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(normalizedPrefix, "Upload JPG, PNG, or PDF files.");
                    continue;
                }

                uploads.Add(file);
            }

            return uploads;
        }

        private async Task<List<MaintenanceLogCompletionAttachment>> SaveCompletionAttachmentsAsync(IEnumerable<IFormFile> files)
        {
            var saved = new List<MaintenanceLogCompletionAttachment>();
            var uploadRoot = Path.Combine(_environment.WebRootPath, CompletionAttachmentFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".dat";
                }

                var normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
                    ? extension
                    : $".{extension}";

                var uniqueName = $"{Guid.NewGuid():N}{normalizedExtension}";
                var physicalPath = Path.Combine(uploadRoot, uniqueName);

                using (var stream = System.IO.File.Create(physicalPath))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/{CompletionAttachmentFolder.Replace('\\', '/')}/{uniqueName}";
                saved.Add(new MaintenanceLogCompletionAttachment
                {
                    FilePath = relativePath,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length,
                    UploadedAtUtc = DateTime.UtcNow
                });
            }

            return saved;
        }

        private void DeleteCompletionAttachmentFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var trimmed = relativePath.TrimStart('~').TrimStart('/');
            var physicalPath = Path.Combine(_environment.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
            {
                try
                {
                    System.IO.File.Delete(physicalPath);
                }
                catch
                {
                    // ignore storage cleanup failures
                }
            }
        }

        private sealed record ChecklistFileResult(string FilePath, string? OriginalFileName, long FileSizeBytes);

        private void DeletePhotoFilesFromJson(string? json)
        {
            var values = ParseEntryValues(json);
            foreach (var kvp in values)
            {
                if (!kvp.Key.EndsWith("__photos", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var paths = ParsePhotoList(kvp.Value);
                foreach (var path in paths)
                {
                    DeletePhotoFile(path);
                }
            }
        }

        private void DeletePhotoFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var trimmed = relativePath.TrimStart('~').TrimStart('/');
            var physicalPath = Path.Combine(_environment.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
            {
                try
                {
                    System.IO.File.Delete(physicalPath);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static string? ExtractPhotoColumnKey(string? fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            const string prefix = "Photos[";
            if (!fieldName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            if (!fieldName.EndsWith("]", StringComparison.Ordinal))
            {
                return null;
            }

            return fieldName.Substring(prefix.Length, fieldName.Length - prefix.Length - 1);
        }

        private static (IReadOnlyDictionary<string, string?> Values,
            IReadOnlyDictionary<string, string?> Notes,
            IReadOnlyDictionary<string, IReadOnlyList<string>> Photos) BuildEntryDataDictionaries(
                string? json,
                IReadOnlyList<MaintenanceLogColumnDefinition> columns)
        {
            var raw = ParseEntryValues(json);
            var ordered = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var notes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var photos = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                raw.TryGetValue(column.Key, out var value);
                ordered[column.Key] = value;

                if (column.IncludeNotes)
                {
                    var noteKey = MaintenanceLogTemplateHelper.BuildNotesKey(column.Key);
                    if (!string.IsNullOrWhiteSpace(noteKey) && raw.TryGetValue(noteKey, out var noteValue))
                    {
                        notes[column.Key] = noteValue;
                    }
                    else
                    {
                        notes[column.Key] = null;
                    }
                }

                if (column.IncludePhotos)
                {
                    var photoKey = MaintenanceLogTemplateHelper.BuildPhotosKey(column.Key);
                    if (!string.IsNullOrWhiteSpace(photoKey) && raw.TryGetValue(photoKey, out var photoValue))
                    {
                        photos[column.Key] = ParsePhotoList(photoValue);
                    }
                    else
                    {
                        photos[column.Key] = Array.Empty<string>();
                    }
                }
            }

            return (ordered, notes, photos);
        }

        private static IReadOnlyList<string> ParsePhotoList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<string>();
            }

            try
            {
                var paths = JsonSerializer.Deserialize<List<string>>(json, EntrySerializerOptions);
                var cleaned = paths?
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path.Replace('\\', '/'))
                    .ToList();
                return cleaned ?? new List<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static Dictionary<string, string?> ParseEntryValues(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, EntrySerializerOptions);
                return values != null
                    ? new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string? NormalizeValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length > 500 ? trimmed[..500] : trimmed;
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n');
            var sanitized = value.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{sanitized}\"" : sanitized;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sanitized = new string(value
                .Select(ch => invalid.Contains(ch) ? '-' : ch)
                .ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "logs" : sanitized;
        }

        private static string? BuildUserName(ApplicationUser? user)
        {
            if (user == null)
            {
                return null;
            }

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }

            return user.UserName;
        }
    }
}

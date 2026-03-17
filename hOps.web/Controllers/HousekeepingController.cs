using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Housekeeping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class HousekeepingController : BaseController
    {
        private readonly MentionService _mentionService;
        private readonly IPassOnLogNotificationService _notificationService;
        private readonly IUserTimeZoneService _timeZoneService;
        private readonly ILogger<HousekeepingController> _logger;

        public HousekeepingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MentionService mentionService,
            IPassOnLogNotificationService notificationService,
            IUserTimeZoneService timeZoneService,
            ILogger<HousekeepingController> logger)
            : base(context, userManager)
        {
            _mentionService = mentionService;
            _notificationService = notificationService;
            _timeZoneService = timeZoneService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult DailyRecap()
        {
            var localDate = _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;
            var model = DailyRecapViewModel.CreateDefault(localDate);
            model.EnsureCollectionIntegrity();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MprTracker(int? month, int? year, string? period, DateTime? start, DateTime? end)
        {
            var now = _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;
            var model = new MprTrackerViewModel
            {
                EntryDate = now,
                CanEditStandards = UserCanEditMprStandards(),
                CanManageHousekeepers = UserCanEditMprStandards(),
                LogFilter = new MprTrackerLogFilterViewModel
                {
                    SelectedMonth = month ?? 0,
                    SelectedYear = year ?? 0,
                    PeriodType = string.IsNullOrWhiteSpace(period) ? MprTrackerLogFilterViewModel.PeriodMonth : period!,
                    CustomStartDate = start,
                    CustomEndDate = end
                }
            };

            await PopulateMprTrackerAsync(model);
            ApplyTempDataMessages(model);

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> MprTracker(MprTrackerViewModel model)
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            var canEditStandards = UserCanEditMprStandards();
            model.CanEditStandards = canEditStandards;
            model.CanManageHousekeepers = canEditStandards;
            model.EntryDate = model.EntryDate == default ? _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date : model.EntryDate.Date;

            if (currentProperty == null)
            {
                ModelState.AddModelError(string.Empty, "Select a property to track housekeeping productivity.");
            }

            if (!canEditStandards)
            {
                ModelState.Remove(nameof(model.DepartureStandardMinutes));
                ModelState.Remove(nameof(model.LinenChangeStandardMinutes));
                ModelState.Remove(nameof(model.StayoverStandardMinutes));
                model.ResetStandardsToDefaults();
            }

            if (!model.SelectedHousekeeperId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SelectedHousekeeperId), "Select a housekeeper before saving.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateMprTrackerAsync(model, currentProperty);
                return View(model);
            }

            model.Calculate();

            if (currentProperty == null)
            {
                await PopulateMprTrackerAsync(model);
                return View(model);
            }

            var housekeeper = await _context.HousekeeperProfiles
                .FirstOrDefaultAsync(h => h.Id == model.SelectedHousekeeperId && h.PropertyId == currentProperty.Id && !h.IsDeleted);

            if (housekeeper == null)
            {
                ModelState.AddModelError(nameof(model.SelectedHousekeeperId), "The selected housekeeper could not be found.");
                await PopulateMprTrackerAsync(model, currentProperty);
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var entry = new HousekeepingMprEntry
            {
                PropertyId = currentProperty.Id,
                HousekeeperId = housekeeper.Id,
                HousekeeperName = housekeeper.Name,
                EntryDate = model.EntryDate.Date,
                CheckoutRooms = model.CheckoutRooms,
                LinenChangeRooms = model.LinenChangeRooms,
                StayoverRooms = model.StayoverRooms,
                DndRooms = model.DndRooms,
                HoursWorked = model.HoursWorked,
                TotalMinutesWorked = model.TotalMinutesWorked,
                MinutesPerRoom = model.MinutesPerRoom,
                DepartureStandardMinutes = model.DepartureStandardMinutes,
                LinenChangeStandardMinutes = model.LinenChangeStandardMinutes,
                StayoverStandardMinutes = model.StayoverStandardMinutes,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUser?.Id
            };

            _context.HousekeepingMprEntries.Add(entry);
            await _context.SaveChangesAsync();

            model.EntrySaved = true;
            model.StatusMessage = $"Saved entry for {housekeeper.Name} on {model.EntryDate:MMM d}.";

            await PopulateMprTrackerAsync(model, currentProperty);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddHousekeeper(string name, string? period, int? month, int? year, DateTime? start, DateTime? end)
        {
            var routeValues = BuildFilterRouteValues(period, month, year, start, end);

            if (!UserCanEditMprStandards())
            {
                TempData["MprTrackerError"] = "Only managers can manage housekeeper names.";
                return RedirectToAction(nameof(MprTracker), routeValues);
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                TempData["MprTrackerError"] = "Select a property before managing housekeeper names.";
                return RedirectToAction(nameof(MprTracker), routeValues);
            }

            var trimmedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                TempData["MprTrackerError"] = "Enter a housekeeper name before adding it.";
                return RedirectToAction(nameof(MprTracker), routeValues);
            }

            var normalized = trimmedName.ToUpperInvariant();
            var existing = await _context.HousekeeperProfiles
                .FirstOrDefaultAsync(h => h.PropertyId == currentProperty.Id && h.Name.ToUpper() == normalized);

            if (existing != null)
            {
                if (existing.IsDeleted)
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    await _context.SaveChangesAsync();
                    TempData["MprTrackerStatus"] = $"Restored {existing.Name} to the list.";
                }
                else
                {
                    TempData["MprTrackerError"] = $"{existing.Name} is already listed.";
                }

                return RedirectToAction(nameof(MprTracker), routeValues);
            }

            var profile = new HousekeeperProfile
            {
                PropertyId = currentProperty.Id,
                Name = trimmedName,
                CreatedAt = DateTime.UtcNow
            };

            _context.HousekeeperProfiles.Add(profile);
            await _context.SaveChangesAsync();

            TempData["MprTrackerStatus"] = $"Added {profile.Name} to the list.";
            return RedirectToAction(nameof(MprTracker), routeValues);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHousekeeper(int id, string? period, int? month, int? year, DateTime? start, DateTime? end)
        {
            var routeValues = BuildFilterRouteValues(period, month, year, start, end);

            if (!UserCanEditMprStandards())
            {
                TempData["MprTrackerError"] = "Only managers can delete housekeeper names.";
                return RedirectToAction(nameof(MprTracker), routeValues);
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                TempData["MprTrackerError"] = "Select a property before managing housekeeper names.";
                return RedirectToAction(nameof(MprTracker), routeValues);
            }

            var housekeeper = await _context.HousekeeperProfiles
                .FirstOrDefaultAsync(h => h.Id == id && h.PropertyId == currentProperty.Id);

            if (housekeeper == null)
            {
                TempData["MprTrackerError"] = "The selected housekeeper could not be found.";
                return RedirectToAction(nameof(MprTracker), routeValues);
            }

            housekeeper.IsDeleted = true;
            housekeeper.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["MprTrackerStatus"] = $"Removed {housekeeper.Name} from the dropdown.";
            return RedirectToAction(nameof(MprTracker), routeValues);
        }

        private bool UserCanEditMprStandards()
        {
            return User.IsInRole("Manager") || User.IsInRole("Admin");
        }

        private object BuildFilterRouteValues(string? period, int? month, int? year, DateTime? start, DateTime? end)
        {
            return new
            {
                period = string.IsNullOrWhiteSpace(period) ? null : period,
                month,
                year,
                start = start.HasValue ? start.Value.ToString("yyyy-MM-dd") : null,
                end = end.HasValue ? end.Value.ToString("yyyy-MM-dd") : null
            };
        }

        private void ApplyTempDataMessages(MprTrackerViewModel model)
        {
            if (TempData.TryGetValue("MprTrackerStatus", out var status) && status is string statusMessage)
            {
                model.StatusMessage = statusMessage;
            }

            if (TempData.TryGetValue("MprTrackerError", out var error) && error is string errorMessage)
            {
                model.ErrorMessage = errorMessage;
            }
        }

        private async Task PopulateMprTrackerAsync(MprTrackerViewModel model, Property? currentProperty = null)
        {
            currentProperty ??= ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                model.ErrorMessage ??= "Select a property to manage housekeeping productivity.";
                model.Housekeepers = new List<HousekeeperOptionViewModel>();
                model.LogRows = new List<MprTrackerLogRowViewModel>();
                model.LogDates = new List<DateTime>();
                return;
            }

            var propertyId = currentProperty.Id;
            var allHousekeepers = await _context.HousekeeperProfiles
                .Where(h => h.PropertyId == propertyId)
                .OrderBy(h => h.Name)
                .ToListAsync();

            model.Housekeepers = allHousekeepers
                .Where(h => !h.IsDeleted)
                .Select(h => new HousekeeperOptionViewModel
                {
                    Id = h.Id,
                    Name = h.Name,
                    IsDeleted = h.IsDeleted
                })
                .ToList();

            if (!model.SelectedHousekeeperId.HasValue && model.Housekeepers.Any())
            {
                model.SelectedHousekeeperId = model.Housekeepers.First().Id;
            }

            var now = _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;
            model.LogFilter ??= new MprTrackerLogFilterViewModel();
            if (model.LogFilter.SelectedYear <= 0)
            {
                model.LogFilter.SelectedYear = now.Year;
            }

            if (model.LogFilter.SelectedMonth <= 0)
            {
                model.LogFilter.SelectedMonth = now.Month;
            }

            var (start, end) = model.LogFilter.GetDateRange(now);
            var totalDays = (end.Date - start.Date).Days;
            model.LogDates = new List<DateTime>();
            for (var offset = 0; offset <= totalDays; offset++)
            {
                model.LogDates.Add(start.Date.AddDays(offset));
            }

            var entries = await _context.HousekeepingMprEntries
                .Where(e => e.PropertyId == propertyId && e.EntryDate >= start.Date && e.EntryDate <= end.Date)
                .Include(e => e.Housekeeper)
                .ToListAsync();

            model.LogRows = BuildLogRows(allHousekeepers, entries);
        }

        private static List<MprTrackerLogRowViewModel> BuildLogRows(
            List<HousekeeperProfile> housekeepers,
            IEnumerable<HousekeepingMprEntry> entries)
        {
            var rows = new Dictionary<string, MprTrackerLogRowViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var housekeeper in housekeepers)
            {
                var key = BuildHousekeeperRowKey(housekeeper.Id, housekeeper.Name);
                if (!rows.ContainsKey(key))
                {
                    rows[key] = new MprTrackerLogRowViewModel
                    {
                        HousekeeperId = housekeeper.Id,
                        HousekeeperName = housekeeper.Name,
                        IsDeleted = housekeeper.IsDeleted
                    };
                }
            }

            foreach (var entry in entries)
            {
                var key = BuildHousekeeperRowKey(entry.HousekeeperId, entry.Housekeeper?.Name ?? entry.HousekeeperName);
                if (!rows.TryGetValue(key, out var row))
                {
                    row = new MprTrackerLogRowViewModel
                    {
                        HousekeeperId = entry.HousekeeperId,
                        HousekeeperName = entry.Housekeeper?.Name ?? entry.HousekeeperName,
                        IsDeleted = entry.Housekeeper?.IsDeleted ?? false
                    };
                    rows[key] = row;
                }

                var day = entry.EntryDate.Date;
                if (!row.Cells.TryGetValue(day, out var cell))
                {
                    cell = new MprTrackerLogCellViewModel();
                    row.Cells[day] = cell;
                }

                cell.CheckoutRooms += entry.CheckoutRooms;
                cell.LinenChangeRooms += entry.LinenChangeRooms;
                cell.StayoverRooms += entry.StayoverRooms;
                cell.DndRooms += entry.DndRooms;
                cell.HoursWorked += entry.HoursWorked;
                cell.TotalMinutesWorked += entry.TotalMinutesWorked;
                cell.RecalculateMinutesPerRoom();

                row.Summary.CheckoutRooms += entry.CheckoutRooms;
                row.Summary.LinenChangeRooms += entry.LinenChangeRooms;
                row.Summary.StayoverRooms += entry.StayoverRooms;
                row.Summary.DndRooms += entry.DndRooms;
                row.Summary.HoursWorked += entry.HoursWorked;
                row.Summary.TotalMinutesWorked += entry.TotalMinutesWorked;
            }

            foreach (var row in rows.Values)
            {
                var trackedRooms = row.Summary.CheckoutRooms + row.Summary.StayoverRooms;
                row.Summary.MinutesPerRoom = trackedRooms > 0 && row.Summary.HoursWorked > 0
                    ? Math.Round((row.Summary.HoursWorked * 60m) / trackedRooms, 2, MidpointRounding.AwayFromZero)
                    : null;
            }

            return rows.Values
                .OrderBy(r => r.HousekeeperName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildHousekeeperRowKey(int? id, string name)
        {
            return id.HasValue
                ? $"HK:{id.Value}"
                : $"NAME:{(name ?? string.Empty).Trim().ToUpperInvariant()}";
        }

        [HttpPost]
        public async Task<IActionResult> DailyRecap(DailyRecapViewModel model)
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                ModelState.AddModelError(string.Empty, "Select a property before posting a daily recap.");
            }

            model.EnsureCollectionIntegrity();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid || currentProperty == null)
            {
                return View(model);
            }

            var log = new PassOnLog
            {
                Title = BuildLogTitle(model, currentProperty),
                Body = BuildLogBody(model, currentProperty),
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };

            log.Properties.Add(new PassOnLogProperty
            {
                PropertyId = currentProperty.Id
            });

            _context.PassOnLogs.Add(log);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save daily recap log for property {PropertyId}", currentProperty.Id);
                ModelState.AddModelError(string.Empty, "We could not save the daily recap. Please try again.");
                return View(model);
            }

            var link = Url.Action(nameof(PassOnLogsController.Details), "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action(nameof(PassOnLogsController.Details), "PassOnLogs", new { id = log.Id })
                ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                log.Body,
                currentUser,
                $"Pass On Log: {log.Title}",
                link,
                log.Body);

            var recipients = await _notificationService.GetLogEntryAlertRecipientsAsync(log, currentUser);
            await _notificationService.NotifyLogSubscribersAsync(log, currentUser, link, recipients);
            await _notificationService.SendLogEntryEmailsAsync(log, currentUser, link, recipients);

            TempData["DailyRecapCreatedLogId"] = log.Id;
            TempData["DailyRecapCreatedTitle"] = log.Title;

            return RedirectToAction(nameof(DailyRecap));
        }

        private static string BuildLogTitle(DailyRecapViewModel model, Property property)
        {
            var dateSegment = model.ReportDate.HasValue
                ? model.ReportDate.Value.ToString("MMMM d, yyyy")
                : "Daily Recap";

            var propertyName = string.IsNullOrWhiteSpace(property.Name)
                ? $"Property #{property.Id}"
                : property.Name.Trim();

            return $"{propertyName} \u2013 Daily Recap ({dateSegment})";
        }

        private static string BuildLogBody(DailyRecapViewModel model, Property property)
        {
            var builder = new StringBuilder();
            var reportDate = model.ReportDate?.ToString("MMMM d, yyyy") ?? "Date TBD";
            var dayOfWeek = model.ReportDate?.ToString("dddd");

            builder.AppendLine($"# Daily Recap \u2013 {reportDate}");
            builder.AppendLine();
            builder.AppendLine($"**Property:** {FormatValue(property.Name, $"Property #{property.Id}")}");
            if (!string.IsNullOrWhiteSpace(property.Code))
            {
                builder.AppendLine($"**Property Code:** {property.Code}");
            }
            if (!string.IsNullOrWhiteSpace(dayOfWeek))
            {
                builder.AppendLine($"**Day of Week:** {dayOfWeek}");
            }
            builder.AppendLine($"**Manager/Supervisor:** {FormatValue(model.ManagerName)}");
            builder.AppendLine();

            AppendStartOfDay(model, builder);
            AppendEndOfDay(model, builder);
            AppendStaffing(model, builder);
            AppendOutOfOrder(model, builder);
            AppendRoomExceptions(model, builder);
            AppendMaintenanceIssues(model, builder);
            AppendInspectionIssues(model, builder);
            AppendPublicAreas(model, builder);
            AppendPerformanceNotes(model, builder);
            AppendAdditionalNotes(model, builder);

            return builder.ToString().Trim();
        }

        private static void AppendStartOfDay(DailyRecapViewModel model, StringBuilder builder)
        {
            if (!HasAnyValue(model.OccupancyPercent, model.CheckOuts, model.Stayovers, model.RoomsOutOfOrderStart))
            {
                return;
            }

            builder.AppendLine("## Start of Day");
            builder.AppendLine($"- **Occupancy %:** {FormatValue(model.OccupancyPercent)}");
            builder.AppendLine($"- **Check-outs:** {FormatValue(model.CheckOuts)}");
            builder.AppendLine($"- **Stayovers:** {FormatValue(model.Stayovers)}");
            builder.AppendLine($"- **Rooms OOO:** {FormatValue(model.RoomsOutOfOrderStart)}");
            builder.AppendLine();
        }

        private static void AppendEndOfDay(DailyRecapViewModel model, StringBuilder builder)
        {
            if (!HasAnyValue(model.VacantClean, model.VacantDirty, model.DeepCleansCompleted, model.RoomsOutOfOrderEnd))
            {
                return;
            }

            builder.AppendLine("## End of Day");
            builder.AppendLine($"- **Vacant Clean:** {FormatValue(model.VacantClean)}");
            builder.AppendLine($"- **Vacant Dirty:** {FormatValue(model.VacantDirty)}");
            builder.AppendLine($"- **Deep Cleans Completed:** {FormatValue(model.DeepCleansCompleted)}");
            builder.AppendLine($"- **Rooms OOO:** {FormatValue(model.RoomsOutOfOrderEnd)}");
            builder.AppendLine();
        }

        private static void AppendStaffing(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.Staffing
                .Where(row => row != null && HasAnyValue(row.Area, row.Scheduled, row.CallOffs, row.Tardies, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 1. Staffing Overview");
            builder.AppendLine("| Area | Scheduled | Call-Offs | Tardies | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Scheduled)} | {EscapeTableValue(row.CallOffs)} | {EscapeTableValue(row.Tardies)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendOutOfOrder(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.OutOfOrderRooms
                .Where(row => row != null && HasAnyValue(row.RoomNumber, row.Status, row.Issue, row.CleanStatus, row.ReasonLeftDirty))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 2. Out of Order / Out of Service Rooms");
            builder.AppendLine("| Room Number | OOO / OOS | Issue | Clean or Dirty | If dirty, why was it left dirty? |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.RoomNumber)} | {EscapeTableValue(row.Status)} | {EscapeTableValue(row.Issue)} | {EscapeTableValue(row.CleanStatus)} | {EscapeTableValue(row.ReasonLeftDirty)} |");
            }

            builder.AppendLine();
        }

        private static void AppendRoomExceptions(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.RoomsNotCleaned
                .Where(row => row != null && HasAnyValue(row.RoomNumber, row.Status, row.Reason, row.AssignedTo, row.ActionPlan))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 3. Rooms Not Cleaned / Rolled Over / DND");
            builder.AppendLine("| Room Number | Room Status | Reason Not Cleaned | Assigned To | Action Plan / Next Step |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.RoomNumber)} | {EscapeTableValue(row.Status)} | {EscapeTableValue(row.Reason)} | {EscapeTableValue(row.AssignedTo)} | {EscapeTableValue(row.ActionPlan)} |");
            }

            builder.AppendLine();
        }

        private static void AppendMaintenanceIssues(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.MaintenanceIssues
                .Where(row => row != null && HasAnyValue(row.Area, row.Issue, row.WorkOrderSubmitted, row.RoomStatus, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 4. Maintenance Issues Found by Housekeeping");
            builder.AppendLine("| Room / Area | Issue Found | Work Order Submitted? | Room Status | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Issue)} | {EscapeTableValue(row.WorkOrderSubmitted)} | {EscapeTableValue(row.RoomStatus)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendInspectionIssues(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.InspectionFailures
                .Where(row => row != null && HasAnyValue(row.Area, row.Issue, row.ResponsibleAssociate, row.CoachingGiven, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 5. Cleanliness / Inspection Failures");
            builder.AppendLine("| Room / Area | Issue | Associate Responsible | Coaching Given? | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Issue)} | {EscapeTableValue(row.ResponsibleAssociate)} | {EscapeTableValue(row.CoachingGiven)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendPublicAreas(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.PublicAreas
                .Where(row => row != null && HasAnyValue(row.Area, row.Completed, row.Issues, row.ItemsToOrder, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 6. Public Areas / Laundry / Supplies");
            builder.AppendLine("_Attach completed & signed Houseman/Public Area Attendant checklist when applicable._");
            builder.AppendLine();
            builder.AppendLine("| Area / Item | Completed? | Issue / Shortage | Items Need Order | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Completed)} | {EscapeTableValue(row.Issues)} | {EscapeTableValue(row.ItemsToOrder)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendPerformanceNotes(DailyRecapViewModel model, StringBuilder builder)
        {
            if (!HasAnyValue(model.PerformanceHighlights, model.PerformanceCoaching, model.OperationalChallenges, model.TomorrowPlan))
            {
                return;
            }

            builder.AppendLine("## 8. Associate Performance Notes & End-of-Day Summary");
            if (!string.IsNullOrWhiteSpace(model.PerformanceHighlights))
            {
                builder.AppendLine($"- **Top performers / recognition:** {FormatValue(model.PerformanceHighlights)}");
            }

            if (!string.IsNullOrWhiteSpace(model.PerformanceCoaching))
            {
                builder.AppendLine($"- **Coaching needed:** {FormatValue(model.PerformanceCoaching)}");
            }

            if (!string.IsNullOrWhiteSpace(model.OperationalChallenges))
            {
                builder.AppendLine($"- **Main operational challenges:** {FormatValue(model.OperationalChallenges)}");
            }

            if (!string.IsNullOrWhiteSpace(model.TomorrowPlan))
            {
                builder.AppendLine($"- **Plan for tomorrow:** {FormatValue(model.TomorrowPlan)}");
            }

            builder.AppendLine();
        }

        private static void AppendAdditionalNotes(DailyRecapViewModel model, StringBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(model.AdditionalNotes))
            {
                return;
            }

            builder.AppendLine("## Additional Notes");
            builder.AppendLine(FormatValue(model.AdditionalNotes));
            builder.AppendLine();
        }

        private static bool HasAnyValue(params string?[] values)
        {
            return values?.Any(value => !string.IsNullOrWhiteSpace(value)) ?? false;
        }

        private static string FormatValue(string? value, string placeholder = "—")
        {
            return string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();
        }

        private static string EscapeTableValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();

            return normalized.Replace("|", "\\|", StringComparison.Ordinal);
        }
    }
}

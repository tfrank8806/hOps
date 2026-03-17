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
        public IActionResult MprTracker()
        {
            var model = new MprTrackerViewModel
            {
                CanEditStandards = UserCanEditMprStandards()
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult MprTracker(MprTrackerViewModel model)
        {
            var canEditStandards = UserCanEditMprStandards();
            model.CanEditStandards = canEditStandards;

            if (!canEditStandards)
            {
                ModelState.Remove(nameof(model.DepartureStandardMinutes));
                ModelState.Remove(nameof(model.LinenChangeStandardMinutes));
                ModelState.Remove(nameof(model.StayoverStandardMinutes));
                model.ResetStandardsToDefaults();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Calculate();
            return View(model);
        }

        private bool UserCanEditMprStandards()
        {
            return User.IsInRole("Manager") || User.IsInRole("Admin");
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

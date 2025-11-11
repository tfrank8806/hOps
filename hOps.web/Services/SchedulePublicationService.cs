using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services
{
    public class SchedulePublicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IExtendedEmailSender _emailSender;
        private readonly SchedulePdfRenderer _pdfRenderer;
        private readonly ILogger<SchedulePublicationService> _logger;

        public SchedulePublicationService(
            ApplicationDbContext context,
            IExtendedEmailSender emailSender,
            SchedulePdfRenderer pdfRenderer,
            ILogger<SchedulePublicationService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _pdfRenderer = pdfRenderer;
            _logger = logger;
        }

        public async Task<SchedulePublicationResult> PublishAsync(int scheduleId, string postedByUserId, string? scheduleUrl)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Property)
                .Include(s => s.Assignments)
                    .ThenInclude(a => a.Employee)
                        .ThenInclude(e => e.ApplicationUser)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null)
            {
                return SchedulePublicationResult.Failure("Schedule could not be found.");
            }

            if (!schedule.Assignments.Any())
            {
                return SchedulePublicationResult.Failure("Add at least one shift before posting the schedule.");
            }

            var dayColumns = Enumerable.Range(0, 7)
                .Select(i => schedule.WeekStartDate.Date.AddDays(i))
                .ToList();

            var approvedTimeOff = await _context.ScheduleTimeOffRequests
                .Where(r => r.PropertyId == schedule.PropertyId &&
                            r.Status == TimeOffRequestStatus.Approved &&
                            r.StartDate <= schedule.WeekEndDate &&
                            r.EndDate >= schedule.WeekStartDate)
                .ToListAsync();

            var gridRows = ScheduleGridBuilder.BuildRows(dayColumns, schedule.Assignments, approvedTimeOff);
            if (gridRows.Count == 0)
            {
                return SchedulePublicationResult.Failure("Unable to build the schedule grid. Please add shifts before posting.");
            }

            if (schedule.WeekEndDate == default)
            {
                schedule.WeekEndDate = schedule.WeekStartDate.AddDays(6);
            }

            var now = DateTime.UtcNow;
            schedule.Status = ScheduleStatus.Posted;
            schedule.PostedAtUtc = now;
            schedule.PostedById = postedByUserId;
            schedule.UpdatedAtUtc = now;
            schedule.UpdatedById = postedByUserId;

            var userRecipients = schedule.Assignments
                .Where(a => a.Employee.ApplicationUser != null && !string.IsNullOrWhiteSpace(a.Employee.ApplicationUser.Email))
                .Select(a => a.Employee.ApplicationUser!)
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();

            var manualRecipients = schedule.Assignments
                .Where(a => a.Employee.ApplicationUser == null &&
                            a.Employee.EmailAlertsEnabled &&
                            !string.IsNullOrWhiteSpace(a.Employee.Email))
                .Select(a => new { a.Employee.DisplayName, a.Employee.Email })
                .GroupBy(e => e.Email!.Trim().ToLowerInvariant())
                .Select(g => new
                {
                    DisplayName = g.First().DisplayName,
                    Email = g.First().Email!
                })
                .ToList();

            var notifications = userRecipients
                .Select(u => new UserNotification
                {
                    UserId = u.Id,
                    Type = "schedule",
                    Title = "Schedule posted",
                    Content = $"Week of {schedule.WeekStartDate:MMM d} - {schedule.WeekEndDate:MMM d} has been posted for {schedule.Property.Name}.",
                    LinkUrl = scheduleUrl,
                    CreatedAt = now,
                    IsRead = false
                })
                .ToList();

            if (notifications.Any())
            {
                _context.UserNotifications.AddRange(notifications);
            }

            await _context.SaveChangesAsync();

            var pdfRows = gridRows
                .Select(r => new SchedulePdfRow
                {
                    EmployeeName = r.EmployeeName,
                    CellLines = r.CellLines
                })
                .ToList();

            var pdfBytes = _pdfRenderer.Render(
                schedule.Property.Name,
                $"Week of {schedule.WeekStartDate:MMM d} - {schedule.WeekEndDate:MMM d}",
                dayColumns,
                pdfRows);

            var fileSafeName = schedule.Property.Name.Replace(' ', '_');
            var pdfFileName = $"Schedule_{fileSafeName}_{schedule.WeekStartDate:yyyyMMdd}.pdf";
            var attachment = new EmailAttachment(pdfFileName, pdfBytes, "application/pdf");

            var htmlTable = BuildHtmlTable(dayColumns, gridRows);
            var subject = $"Schedule posted: {schedule.Property.Name} ({schedule.WeekStartDate:MMM d} - {schedule.WeekEndDate:MMM d})";

            foreach (var user in userRecipients)
            {
                if (!user.EmailOnSchedulePosted || string.IsNullOrWhiteSpace(user.Email))
                {
                    continue;
                }

                try
                {
                    var body = BuildEmailBody(schedule.Property.Name, schedule.WeekStartDate, schedule.WeekEndDate, htmlTable, scheduleUrl, user);
                    await _emailSender.SendEmailAsync(user.Email!, subject, body, new[] { attachment });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send schedule email to user {UserId}", user.Id);
                }
            }

            foreach (var manual in manualRecipients)
            {
                try
                {
                    var body = BuildEmailBody(schedule.Property.Name, schedule.WeekStartDate, schedule.WeekEndDate, htmlTable, scheduleUrl, null, manual.DisplayName);
                    await _emailSender.SendEmailAsync(manual.Email, subject, body, new[] { attachment });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send schedule email to {Email}", manual.Email);
                }
            }

            return SchedulePublicationResult.Ok();
        }

        private static string BuildHtmlTable(IReadOnlyList<DateTime> dayColumns, IReadOnlyList<ScheduleGridRow> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<table style=\"width:100%;border-collapse:collapse;font-family:Arial,Helvetica,sans-serif;font-size:13px;\">");
            sb.Append("<thead><tr>");
            sb.Append("<th style=\"border:1px solid #ddd;padding:6px;text-align:left;background-color:#f5f5f5;\">Employee</th>");
            foreach (var day in dayColumns)
            {
                sb.Append($"<th style=\"border:1px solid #ddd;padding:6px;text-align:left;background-color:#f5f5f5;\">{day:ddd}<br />{day:MMM d}</th>");
            }
            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                sb.Append($"<td style=\"border:1px solid #ddd;padding:6px;vertical-align:top;\">{System.Net.WebUtility.HtmlEncode(row.EmployeeName)}</td>");
                foreach (var cell in row.CellLines)
                {
                    if (cell.Count == 0)
                    {
                        sb.Append("<td style=\"border:1px solid #ddd;padding:6px;min-height:40px;\">&mdash;</td>");
                        continue;
                    }

                    var cellBuilder = new StringBuilder();
                    foreach (var line in cell)
                    {
                        cellBuilder.Append("<div>").Append(System.Net.WebUtility.HtmlEncode(line)).Append("</div>");
                    }

                    sb.Append("<td style=\"border:1px solid #ddd;padding:6px;vertical-align:top;\">");
                    sb.Append(cellBuilder);
                    sb.Append("</td>");
                }

                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private static string BuildEmailBody(
            string propertyName,
            DateTime weekStart,
            DateTime weekEnd,
            string htmlTable,
            string? scheduleUrl,
            ApplicationUser? recipient,
            string? manualDisplayName = null)
        {
            var sb = new StringBuilder();
            var greetingName = recipient != null
                ? $"{recipient.FirstName} {recipient.LastName}".Trim()
                : manualDisplayName;
            if (!string.IsNullOrWhiteSpace(greetingName))
            {
                sb.Append($"<p>Hi {System.Net.WebUtility.HtmlEncode(greetingName)},</p>");
            }

            sb.Append($"<p>The schedule for <strong>{System.Net.WebUtility.HtmlEncode(propertyName)}</strong> covering {weekStart:MMM d} - {weekEnd:MMM d} has been posted.</p>");
            sb.Append(htmlTable);

            if (!string.IsNullOrWhiteSpace(scheduleUrl))
            {
                sb.Append($"<p><a href=\"{scheduleUrl}\">View this schedule in HotelOps</a></p>");
            }

            sb.Append("<p>Thanks,<br/>HotelOps</p>");
            return sb.ToString();
        }

    }
}

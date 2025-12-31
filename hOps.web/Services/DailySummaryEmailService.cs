using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services
{
    public class DailySummaryEmailService : BackgroundService
    {
        private const int PreviewCharacterLimit = 350;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailySummaryEmailService> _logger;
        private readonly string? _appBaseUrl;
        private static readonly IReadOnlyDictionary<string, string> SalesInquiryLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "group_rooms", "Group Rooms" },
                { "corporate_rate", "Corporate Rate" },
                { "meeting_room", "Meeting Room" },
                { "other", "Other" }
            };

        public DailySummaryEmailService(
            IServiceProvider serviceProvider,
            ILogger<DailySummaryEmailService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _appBaseUrl = (configuration["App:BaseUrl"] ?? configuration["AppBaseUrl"])?.TrimEnd('/');
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var nextRun = GetNextRun(now);
                var delay = nextRun - now;

                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await SendSummariesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to complete daily summary email run.");
                }
            }
        }

        private async Task SendSummariesAsync(CancellationToken cancellationToken)
        {
            var summaryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var dayStartUtc = summaryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEndUtc = dayStartUtc.AddDays(1);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var users = await context.Users
                .Where(u => u.EmailDailySummary && !string.IsNullOrWhiteSpace(u.Email))
                .Include(u => u.UserPropertyAccesses)
                .Include(u => u.EmailPropertySubscriptions)
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (user.DailySummaryLastSentUtc.HasValue)
                {
                    var lastSentDate = DateOnly.FromDateTime(DateTime.SpecifyKind(user.DailySummaryLastSentUtc.Value, DateTimeKind.Utc));
                    if (lastSentDate >= summaryDate)
                    {
                        continue;
                    }
                }

                var propertyIds = user.EmailPropertySubscriptions?
                    .Where(p => p.IncludeInDailySummary)
                    .Select(p => p.PropertyId)
                    .Distinct()
                    .ToList() ?? new List<int>();

                if (!propertyIds.Any() && user.UserPropertyAccesses != null)
                {
                    propertyIds = user.UserPropertyAccesses
                        .Select(upa => upa.PropertyId)
                        .Distinct()
                        .ToList();
                }

                if (!propertyIds.Any())
                {
                    user.DailySummaryLastSentUtc = dayStartUtc;
                    continue;
                }

                var logs = await context.PassOnLogs
                    .AsNoTracking()
                    .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                    .Include(l => l.CreatedBy)
                    .Where(l =>
                        (l.CreatedAt >= dayStartUtc && l.CreatedAt < dayEndUtc) ||
                        (l.UpdatedAt.HasValue && l.UpdatedAt.Value >= dayStartUtc && l.UpdatedAt.Value < dayEndUtc))
                    .Where(l => l.Properties.Any(lp => propertyIds.Contains(lp.PropertyId)))
                    .ToListAsync(cancellationToken);

                var posts = await context.BulletinPosts
                    .AsNoTracking()
                    .Include(p => p.Property)
                    .Include(p => p.CreatedBy)
                    .Where(p => propertyIds.Contains(p.PropertyId))
                    .Where(p =>
                        (p.CreatedAt >= dayStartUtc && p.CreatedAt < dayEndUtc) ||
                        (p.UpdatedAt.HasValue && p.UpdatedAt.Value >= dayStartUtc && p.UpdatedAt.Value < dayEndUtc))
                    .ToListAsync(cancellationToken);

                var salesLeads = await context.SalesLeadSubmissions
                    .AsNoTracking()
                    .Include(sl => sl.Property)
                    .Include(sl => sl.SalesContact)
                    .Where(sl => propertyIds.Contains(sl.PropertyId))
                    .Where(sl => sl.CreatedAtUtc >= dayStartUtc && sl.CreatedAtUtc < dayEndUtc)
                    .ToListAsync(cancellationToken);

                var openWorkOrders = await LoadOpenWorkOrdersAsync(context, propertyIds, cancellationToken);
                var announcements = await LoadAnnouncementsAsync(context, propertyIds, cancellationToken);
                var upcomingEvents = await LoadUpcomingEventsAsync(context, propertyIds, cancellationToken);
                var packageEntries = await LoadOpenPackagesAsync(context, propertyIds, cancellationToken);
                var lostFoundEntries = await LoadOpenLostFoundEntriesAsync(context, propertyIds, cancellationToken);
                var scheduleSummaries = await LoadScheduleSummariesAsync(context, propertyIds, cancellationToken);

                var body = BuildSummaryBody(
                    user,
                    summaryDate,
                    logs,
                    posts,
                    salesLeads,
                    openWorkOrders,
                    packageEntries,
                    lostFoundEntries,
                    upcomingEvents,
                    announcements,
                    scheduleSummaries,
                    _appBaseUrl);
                var subject = $"Daily summary for {summaryDate:MMM d, yyyy}";

                try
                {
                    await emailSender.SendEmailAsync(user.Email!, subject, body);
                    user.DailySummaryLastSentUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send daily summary email to user {UserId}", user.Id);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static string BuildSummaryBody(
            ApplicationUser user,
            DateOnly summaryDate,
            List<PassOnLog> logs,
            List<BulletinPost> posts,
            List<SalesLeadSubmission> salesLeads,
            IReadOnlyList<DailySummaryWorkOrder> workOrders,
            IReadOnlyList<DailySummaryPackageLog> packageEntries,
            IReadOnlyList<DailySummaryLostFound> lostFoundEntries,
            IReadOnlyList<DailySummaryEvent> upcomingEvents,
            IReadOnlyList<DailySummaryAnnouncement> announcements,
            IReadOnlyList<DailySummarySchedule> schedules,
            string? baseUrl)
        {
            var builder = new StringBuilder();
            var userName = BuildUserDisplayName(user);
            var safeName = WebUtility.HtmlEncode(userName);
            var userTimeZone = ResolveUserTimeZone(user);

            builder.AppendLine($@"<p>Hello {safeName},</p>");
            builder.AppendLine($@"<p>Here is your activity summary for {summaryDate:MMMM d, yyyy}.</p>");

            AppendWorkOrders();
            AppendPassOnLogs();
            AppendUpcomingEvents();
            AppendAnnouncements();
            AppendBulletins();
            AppendPackages();
            AppendLostFound();
            AppendSchedules();
            AppendSalesLeads();

            builder.AppendLine(@"<p style=""margin-top:1.5rem;"">You are receiving this email because daily summaries are enabled in your profile preferences.</p>");

            return builder.ToString();

            void AppendSectionHeader(string title)
            {
                builder.AppendLine($@"<h3 style=""margin-top:1.5rem;"">{title}</h3>");
            }

            void AppendWorkOrders()
            {
                AppendSectionHeader("Open Work Orders");
                if (workOrders == null || workOrders.Count == 0)
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No open work orders for your selected properties.</p>");
                    return;
                }

                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                foreach (var order in workOrders.OrderBy(o => o.DueDate))
                {
                    var issue = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(order.Issue) ? "Work Order" : order.Issue);
                    var statusLabel = WebUtility.HtmlEncode(WorkOrderStatusOptions.GetLabel(order.Status));
                    var properties = order.PropertyNames.Any()
                        ? string.Join(", ", order.PropertyNames.Select(WebUtility.HtmlEncode))
                        : "Property";
                    var location = string.IsNullOrWhiteSpace(order.Location) ? null : WebUtility.HtmlEncode(order.Location);
                    var department = string.IsNullOrWhiteSpace(order.DepartmentName) ? null : WebUtility.HtmlEncode(order.DepartmentName);
                    var openedAt = WebUtility.HtmlEncode(FormatUserLocal(order.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var dueAt = WebUtility.HtmlEncode(FormatUserLocal(order.DueDate, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var orderLink = BuildAbsoluteUrl(order.DetailPath, baseUrl);

                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{issue}</strong><br/><span style=""color:#555;"">{properties}</span>");
                    builder.Append($@"<div style=""margin:0.4rem 0;""><strong>Status:</strong> {statusLabel}");
                    if (location != null)
                    {
                        builder.Append($@"<br/><strong>Location:</strong> {location}");
                    }
                    if (department != null)
                    {
                        builder.Append($@"<br/><strong>Department:</strong> {department}");
                    }
                    builder.Append($@"<br/><strong>Opened:</strong> {openedAt}");
                    builder.Append($@"<br/><strong>Due:</strong> {dueAt}</div>");
                    builder.AppendLine($@"<a href=""{orderLink}"">View work order</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            void AppendPassOnLogs()
            {
                AppendSectionHeader("Pass On Logs (Last 24 Hours)");
                if (!logs.Any())
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No pass on logs were posted in the last 24 hours.</p>");
                    return;
                }

                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                foreach (var log in logs.OrderBy(l => l.CreatedAt))
                {
                    var logTitle = WebUtility.HtmlEncode(log.Title);
                    var createdAtLocal = FormatUserLocal(log.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt");
                    var safeCreated = WebUtility.HtmlEncode(createdAtLocal);
                    var properties = log.Properties
                        .Select(lp => lp.Property?.Name ?? $"Property #{lp.PropertyId}")
                        .Distinct()
                        .Select(WebUtility.HtmlEncode)
                        .ToList();
                    var propertiesText = properties.Any()
                        ? $"<span style=\"color:#555;\">{string.Join(", ", properties)}</span><br/>"
                        : string.Empty;
                    var previewHtml = BuildRichTextPreview(log.Body, PreviewCharacterLimit);
                    var link = BuildAbsoluteUrl($"/PassOnLogs/Details/{log.Id}", baseUrl);
                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{logTitle}</strong><br/>{propertiesText}<span style=""color:#555;"">{safeCreated}</span>");
                    if (!string.IsNullOrEmpty(previewHtml))
                    {
                        builder.Append($@"<div style=""margin:0.5rem 0;"">{previewHtml}</div>");
                    }
                    else
                    {
                        builder.Append("<br/>");
                    }
                    builder.AppendLine($@"<a href=""{link}"">View log</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            void AppendUpcomingEvents()
            {
                AppendSectionHeader("Upcoming Events");
                if (upcomingEvents == null || upcomingEvents.Count == 0)
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No upcoming events found for your properties.</p>");
                    return;
                }

                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                foreach (var calendarEvent in upcomingEvents.OrderBy(e => e.StartDate))
                {
                    var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(calendarEvent.Title) ? "Event" : calendarEvent.Title);
                    var props = calendarEvent.PropertyNames.Any()
                        ? string.Join(", ", calendarEvent.PropertyNames.Select(WebUtility.HtmlEncode))
                        : "Property";
                    var dateLabel = WebUtility.HtmlEncode(BuildEventDateLabel(calendarEvent));
                    var timeLabel = BuildEventTimeLabel(calendarEvent);
                    var link = BuildAbsoluteUrl(calendarEvent.DetailPath, baseUrl);

                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{title}</strong><br/><span style=""color:#555;"">{props}</span><br/>{dateLabel}");
                    if (!string.IsNullOrEmpty(timeLabel))
                    {
                        builder.Append($@"<br/>{WebUtility.HtmlEncode(timeLabel)}");
                    }
                    builder.AppendLine($@"<br/><a href=""{link}"">View calendar</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            void AppendAnnouncements()
            {
                AppendSectionHeader("Manager Notes &amp; Announcements");
                if (announcements == null || announcements.Count == 0)
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No manager announcements are currently posted.</p>");
                    return;
                }

                foreach (var announcement in announcements.OrderBy(a => a.PropertyName))
                {
                    var propertyName = WebUtility.HtmlEncode(announcement.PropertyName);
                    builder.Append($@"<div style=""margin-bottom:1rem;""><strong>{propertyName}</strong>");

                    if (!string.IsNullOrWhiteSpace(announcement.Content))
                    {
                        builder.Append($@"<div style=""margin:0.4rem 0;"">{RichTextRenderer.ToHtml(announcement.Content)}</div>");
                    }
                    else
                    {
                        builder.Append(@"<p style=""color:#6c757d;margin:0.3rem 0;"">No announcement content.</p>");
                    }

                    if (announcement.UpdatedAt.HasValue)
                    {
                        var updated = WebUtility.HtmlEncode(FormatUserLocal(announcement.UpdatedAt.Value, userTimeZone, "MMM d, yyyy h:mm tt"));
                        var updatedBy = string.IsNullOrWhiteSpace(announcement.UpdatedByName)
                            ? string.Empty
                            : $" by {WebUtility.HtmlEncode(announcement.UpdatedByName)}";
                        builder.Append($@"<p style=""color:#555;font-size:0.9rem;margin:0;margin-bottom:0.4rem;"">Updated {updated}{updatedBy}</p>");
                    }

                    if (announcement.Attachments.Any())
                    {
                        builder.Append(@"<ul style=""padding-left:1.25rem;margin:0 0 0.5rem 0;list-style-type:circle;"">");
                        foreach (var attachment in announcement.Attachments)
                        {
                            var fileName = WebUtility.HtmlEncode(attachment.FileName);
                            var link = BuildAbsoluteUrl(attachment.DownloadPath, baseUrl);
                            builder.Append($@"<li><a href=""{link}"">{fileName}</a></li>");
                        }
                        builder.Append("</ul>");
                    }

                    builder.AppendLine("</div>");
                }
            }

            void AppendBulletins()
            {
                AppendSectionHeader("Bulletin Board");
                if (!posts.Any())
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No bulletin posts were added or updated in the last 24 hours.</p>");
                    return;
                }

                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                foreach (var post in posts.OrderBy(p => p.CreatedAt))
                {
                    var propertyName = post.Property?.Name ?? "Property";
                    var safeProperty = WebUtility.HtmlEncode(propertyName);
                    var createdAtLocal = FormatUserLocal(post.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt");
                    var safeCreated = WebUtility.HtmlEncode(createdAtLocal);
                    var contentHtml = BuildRichTextPreview(post.Content, PreviewCharacterLimit);
                    var link = BuildAbsoluteUrl("/Home", baseUrl);
                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{safeProperty}</strong><br/><span style=""color:#555;"">{safeCreated}</span>");
                    if (!string.IsNullOrEmpty(contentHtml))
                    {
                        builder.Append($@"<div style=""margin:0.5rem 0;"">{contentHtml}</div>");
                    }
                    else
                    {
                        builder.Append("<br/>");
                    }
                    builder.AppendLine($@"<a href=""{link}"">View post</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            void AppendPackages()
            {
                AppendSectionHeader("Open Package Log Entries");
                if (packageEntries == null || packageEntries.Count == 0)
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No open package log entries.</p>");
                    return;
                }

                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                foreach (var entry in packageEntries.OrderByDescending(p => p.LoggedAt))
                {
                    var recipient = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(entry.RecipientName) ? "Package" : entry.RecipientName);
                    var property = WebUtility.HtmlEncode(entry.PropertyName);
                    var loggedAt = WebUtility.HtmlEncode(FormatUserLocal(entry.LoggedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var room = string.IsNullOrWhiteSpace(entry.RoomNumber) ? null : WebUtility.HtmlEncode(entry.RoomNumber);
                    var carrier = string.IsNullOrWhiteSpace(entry.Carrier) ? null : WebUtility.HtmlEncode(entry.Carrier);
                    var tracking = string.IsNullOrWhiteSpace(entry.TrackingNumber) ? null : WebUtility.HtmlEncode(entry.TrackingNumber);
                    var storage = string.IsNullOrWhiteSpace(entry.StorageLocation) ? null : WebUtility.HtmlEncode(entry.StorageLocation);
                    var link = BuildAbsoluteUrl(entry.DetailPath, baseUrl);

                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{recipient}</strong><br/><span style=""color:#555;"">{property}</span>");
                    builder.Append($@"<div style=""margin:0.4rem 0;""><strong>Logged:</strong> {loggedAt}");
                    if (room != null)
                    {
                        builder.Append($@"<br/><strong>Room:</strong> {room}");
                    }
                    if (carrier != null)
                    {
                        builder.Append($@"<br/><strong>Carrier:</strong> {carrier}");
                    }
                    if (tracking != null)
                    {
                        builder.Append($@"<br/><strong>Tracking:</strong> {tracking}");
                    }
                    if (storage != null)
                    {
                        builder.Append($@"<br/><strong>Storage:</strong> {storage}");
                    }
                    builder.Append("</div>");
                    builder.AppendLine($@"<a href=""{link}"">View package entry</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            void AppendLostFound()
            {
                AppendSectionHeader("Open Lost &amp; Found Logs");
                if (lostFoundEntries == null || lostFoundEntries.Count == 0)
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No open lost &amp; found entries.</p>");
                    return;
                }

                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                foreach (var entry in lostFoundEntries.OrderByDescending(l => l.CreatedAt))
                {
                    var title = WebUtility.HtmlEncode(entry.Title);
                    var property = WebUtility.HtmlEncode(entry.PropertyName);
                    var status = WebUtility.HtmlEncode(entry.Status);
                    var type = string.IsNullOrWhiteSpace(entry.Type) ? null : WebUtility.HtmlEncode(entry.Type);
                    var createdAt = WebUtility.HtmlEncode(FormatUserLocal(entry.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var link = BuildAbsoluteUrl(entry.DetailPath, baseUrl);

                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{title}</strong><br/><span style=""color:#555;"">{property}</span>");
                    builder.Append($@"<div style=""margin:0.4rem 0;""><strong>Status:</strong> {status}");
                    if (type != null)
                    {
                        builder.Append($@"<br/><strong>Type:</strong> {type}");
                    }
                    builder.Append($@"<br/><strong>Logged:</strong> {createdAt}</div>");
                    builder.AppendLine($@"<a href=""{link}"">View entry</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            void AppendSchedules()
            {
                var currentSchedules = schedules.Where(s => !s.IsUpcoming).OrderBy(s => s.PropertyName).ThenBy(s => s.WeekStart).ToList();
                var nextSchedules = schedules.Where(s => s.IsUpcoming).OrderBy(s => s.PropertyName).ThenBy(s => s.WeekStart).ToList();

                AppendSectionHeader("Current Week Schedule");
                if (!currentSchedules.Any())
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No posted schedule for the current week.</p>");
                }
                else
                {
                    RenderScheduleList(currentSchedules);
                }

                AppendSectionHeader("Upcoming Week Schedule");
                if (!nextSchedules.Any())
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No posted schedule for the upcoming week.</p>");
                }
                else
                {
                    RenderScheduleList(nextSchedules);
                }
            }

            void RenderScheduleList(IEnumerable<DailySummarySchedule> scheduleList)
            {
                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                foreach (var schedule in scheduleList)
                {
                    var property = WebUtility.HtmlEncode(schedule.PropertyName);
                    var title = WebUtility.HtmlEncode(schedule.Title);
                    var weekRange = $"{FormatUserLocal(schedule.WeekStart, userTimeZone, "MMM d, yyyy")} - {FormatUserLocal(schedule.WeekEnd, userTimeZone, "MMM d, yyyy")}";
                    var link = BuildAbsoluteUrl(schedule.DetailPath, baseUrl);
                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{property}</strong><br/>{title}<div style=""margin:0.35rem 0;"">{WebUtility.HtmlEncode(weekRange)}</div><a href=""{link}"">View schedule</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            void AppendSalesLeads()
            {
                AppendSectionHeader("Sales Leads (Past 24 Hours)");
                if (!salesLeads.Any())
                {
                    builder.AppendLine(@"<p style=""color:#6c757d;margin:0;"">No new sales leads were submitted in the last 24 hours.</p>");
                    return;
                }

                builder.AppendLine(@"<ul style=""padding-left:1.25rem;margin:0;list-style-type:disc;"">");
                var salesLink = BuildAbsoluteUrl("/Sales", baseUrl);
                foreach (var lead in salesLeads.OrderBy(l => l.CreatedAtUtc))
                {
                    var propertyName = lead.Property?.Name ?? "Property";
                    var safeProperty = WebUtility.HtmlEncode(propertyName);
                    var submittedAt = FormatUserLocal(lead.CreatedAtUtc, userTimeZone, "MMM d, yyyy h:mm tt");
                    var safeSubmitted = WebUtility.HtmlEncode(submittedAt);
                    var submittedBy = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.SubmittedByName) ? "Team Member" : lead.SubmittedByName);
                    var groupName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.GroupName) ? "N/A" : lead.GroupName);
                    var contactName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.ContactName) ? "Not provided" : lead.ContactName);
                    var contactEmail = WebUtility.HtmlEncode(lead.ContactEmail);
                    var contactPhone = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.ContactPhone) ? "Not provided" : lead.ContactPhone!);
                    var inquiryHtml = BuildInquiryListHtml(lead);
                    var datesText = BuildDateRangeDescription(lead.EventStartDate, lead.EventEndDate, userTimeZone);
                    var budgetText = BuildBudgetDescription(lead.BudgetMinimum, lead.BudgetMaximum);
                    var detailsHtml = BuildPlainTextHtml(lead.AdditionalDetails);

                    builder.Append($@"<li style=""margin-bottom:1rem;""><strong>{safeProperty}</strong><br/><span style=""color:#555;"">Submitted {safeSubmitted} by {submittedBy}</span>");
                    builder.Append($@"<div style=""margin:0.4rem 0;""><strong>Group:</strong> {groupName}<br/><strong>Contact:</strong> {contactName} &lt;<a href=""mailto:{contactEmail}"">{contactEmail}</a>&gt;<br/><strong>Phone:</strong> {contactPhone}</div>");

                    if (!string.IsNullOrEmpty(inquiryHtml))
                    {
                        builder.Append($@"<div style=""margin-bottom:0.3rem;""><strong>Inquiry:</strong><br/>{inquiryHtml}</div>");
                    }

                    builder.Append($@"<div style=""margin-bottom:0.3rem;""><strong>Dates:</strong> {datesText}<br/><strong>Budget:</strong> {WebUtility.HtmlEncode(budgetText)}</div>");

                    if (!string.IsNullOrEmpty(detailsHtml))
                    {
                        builder.Append($@"<div style=""margin-bottom:0.3rem;""><strong>Additional details:</strong><br/>{detailsHtml}</div>");
                    }

                    builder.AppendLine($@"<a href=""{salesLink}"">View sales tool</a></li>");
                }
                builder.AppendLine("</ul>");
            }

            static string BuildEventDateLabel(DailySummaryEvent calendarEvent)
            {
                return calendarEvent.StartDate.Date == calendarEvent.EndDate.Date
                    ? calendarEvent.StartDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
                    : $"{calendarEvent.StartDate:MMM d, yyyy} - {calendarEvent.EndDate:MMM d, yyyy}";
            }

            static string? BuildEventTimeLabel(DailySummaryEvent calendarEvent)
            {
                if (!calendarEvent.StartTime.HasValue && !calendarEvent.EndTime.HasValue)
                {
                    return null;
                }

                string Format(TimeSpan value) => DateTime.Today.Add(value).ToString("t", CultureInfo.CurrentCulture);

                if (calendarEvent.StartTime.HasValue && calendarEvent.EndTime.HasValue)
                {
                    return $"{Format(calendarEvent.StartTime.Value)} - {Format(calendarEvent.EndTime.Value)}";
                }

                if (calendarEvent.StartTime.HasValue)
                {
                    return Format(calendarEvent.StartTime.Value);
                }

                return $"Until {Format(calendarEvent.EndTime!.Value)}";
            }
        }

        private static async Task<List<DailySummaryWorkOrder>> LoadOpenWorkOrdersAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryWorkOrder>();
            }

            var openStatuses = new[] { "New", "In Progress", "Escalated", "On Hold" };

            var workOrders = await context.WorkOrders
                .AsNoTracking()
                .Include(wo => wo.Properties)
                    .ThenInclude(wp => wp.Property)
                .Include(wo => wo.Department)
                .Where(wo => openStatuses.Contains(wo.Status))
                .Where(wo => wo.Properties.Any(p => propertyIds.Contains(p.PropertyId)))
                .OrderBy(wo => wo.DueDate)
                .ToListAsync(cancellationToken);

            return workOrders.Select(wo => new DailySummaryWorkOrder
            {
                Id = wo.Id,
                Issue = wo.Issue ?? "Work Order",
                Status = wo.Status ?? string.Empty,
                Location = wo.Location,
                DepartmentName = wo.Department?.Name,
                PropertyNames = wo.Properties
                    .Where(p => propertyIds.Contains(p.PropertyId))
                    .Select(p => string.IsNullOrWhiteSpace(p.Property?.Name) ? $"Property #{p.PropertyId}" : p.Property!.Name!)
                    .Distinct()
                    .ToList(),
                CreatedAt = wo.CreatedAt,
                DueDate = wo.DueDate,
                DetailPath = $"/WorkOrders/Edit/{wo.Id}"
            }).ToList();
        }

        private static async Task<List<DailySummaryAnnouncement>> LoadAnnouncementsAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryAnnouncement>();
            }

            var announcements = await context.ManagerAnnouncements
                .AsNoTracking()
                .Include(a => a.Property)
                .Include(a => a.Attachments)
                .Include(a => a.UpdatedBy)
                .Where(a => propertyIds.Contains(a.PropertyId))
                .ToListAsync(cancellationToken);

            return announcements
                .GroupBy(a => a.PropertyId)
                .Select(group => group
                    .OrderByDescending(a => a.UpdatedAt)
                    .First())
                .Select(announcement => new DailySummaryAnnouncement
                {
                    PropertyName = string.IsNullOrWhiteSpace(announcement.Property?.Name)
                        ? $"Property #{announcement.PropertyId}"
                        : announcement.Property!.Name!,
                    Content = announcement.Content,
                    UpdatedAt = announcement.UpdatedAt,
                    UpdatedByName = BuildUserDisplayName(announcement.UpdatedBy),
                    Attachments = announcement.Attachments
                        .OrderBy(a => string.IsNullOrWhiteSpace(a.OriginalFileName) ? a.FilePath : a.OriginalFileName, StringComparer.OrdinalIgnoreCase)
                        .Select(a => new DailySummaryAttachment
                        {
                            FileName = string.IsNullOrWhiteSpace(a.OriginalFileName) ? a.FilePath ?? "Attachment" : a.OriginalFileName!,
                            DownloadPath = a.FilePath ?? string.Empty
                        })
                        .ToList()
                })
                .ToList();
        }

        private static async Task<List<DailySummaryEvent>> LoadUpcomingEventsAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryEvent>();
            }

            var today = DateTime.UtcNow.Date;
            var events = await context.CalendarEvents
                .AsNoTracking()
                .Include(e => e.Category)
                .Include(e => e.EventProperties)
                    .ThenInclude(ep => ep.Property)
                .Where(e => e.EventProperties.Any(ep => propertyIds.Contains(ep.PropertyId)))
                .Where(e => e.EndDate >= today)
                .OrderBy(e => e.StartDate)
                .Take(5)
                .ToListAsync(cancellationToken);

            return events.Select(e => new DailySummaryEvent
            {
                Title = e.Title,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                CategoryName = e.Category?.Name ?? "Event",
                PropertyNames = e.EventProperties
                    .Where(ep => propertyIds.Contains(ep.PropertyId))
                    .Select(ep => string.IsNullOrWhiteSpace(ep.Property?.Name) ? $"Property #{ep.PropertyId}" : ep.Property!.Name!)
                    .Distinct()
                    .ToList(),
                DetailPath = "/Calendar"
            }).ToList();
        }

        private static async Task<List<DailySummaryPackageLog>> LoadOpenPackagesAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryPackageLog>();
            }

            var packages = await context.PackageLogEntries
                .AsNoTracking()
                .Include(p => p.Property)
                .Where(p => propertyIds.Contains(p.PropertyId) && !p.Delivered)
                .OrderByDescending(p => p.LoggedAt)
                .ToListAsync(cancellationToken);

            return packages.Select(p => new DailySummaryPackageLog
            {
                Id = p.Id,
                PropertyName = string.IsNullOrWhiteSpace(p.Property?.Name) ? $"Property #{p.PropertyId}" : p.Property!.Name!,
                RecipientName = p.RecipientName,
                RoomNumber = p.RoomNumber,
                Carrier = p.Carrier,
                TrackingNumber = p.TrackingNumber,
                StorageLocation = p.StorageLocation,
                LoggedAt = p.LoggedAt,
                DetailPath = $"/MailLog/Details/{p.Id}"
            }).ToList();
        }

        private static async Task<List<DailySummaryLostFound>> LoadOpenLostFoundEntriesAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryLostFound>();
            }

            var lostFoundEntries = await context.LostFoundEntries
                .AsNoTracking()
                .Include(lf => lf.Property)
                .Where(lf =>
                    propertyIds.Contains(lf.PropertyId) &&
                    lf.Status != LostFoundStatus.ReturnedToGuest &&
                    lf.Status != LostFoundStatus.DisposedOf)
                .OrderByDescending(lf => lf.CreatedAt)
                .ToListAsync(cancellationToken);

            return lostFoundEntries.Select(lf => new DailySummaryLostFound
            {
                Id = lf.Id,
                Title = !string.IsNullOrWhiteSpace(lf.ItemFound)
                    ? lf.ItemFound!
                    : (!string.IsNullOrWhiteSpace(lf.ItemLost) ? lf.ItemLost! : "Lost & Found Entry"),
                PropertyName = string.IsNullOrWhiteSpace(lf.Property?.Name) ? $"Property #{lf.PropertyId}" : lf.Property!.Name!,
                Status = lf.Status.ToString(),
                Type = lf.Type.ToString(),
                CreatedAt = lf.CreatedAt,
                DetailPath = $"/LostAndFound/Details/{lf.Id}"
            }).ToList();
        }

        private static async Task<List<DailySummarySchedule>> LoadScheduleSummariesAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            var summaries = new List<DailySummarySchedule>();
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return summaries;
            }

            var settings = await context.ScheduleSettings
                .AsNoTracking()
                .Where(s => propertyIds.Contains(s.PropertyId))
                .ToDictionaryAsync(s => s.PropertyId, s => s.StartDayOfWeek, cancellationToken);

            var today = DateTime.UtcNow.Date;
            var targetWeeks = new Dictionary<(int PropertyId, DateTime WeekStart), bool>();

            foreach (var propertyId in propertyIds)
            {
                var startDay = settings.TryGetValue(propertyId, out var configuredStart) ? configuredStart : DayOfWeek.Monday;
                var currentStart = AlignToWeekStart(today, startDay);
                targetWeeks[(propertyId, currentStart)] = false;
                targetWeeks[(propertyId, currentStart.AddDays(7))] = true;
            }

            var weekValues = targetWeeks.Keys.Select(k => k.WeekStart).Distinct().ToList();

            var schedules = await context.Schedules
                .AsNoTracking()
                .Include(s => s.Property)
                .Where(s => propertyIds.Contains(s.PropertyId) &&
                            s.Status == ScheduleStatus.Posted &&
                            weekValues.Contains(s.WeekStartDate))
                .ToListAsync(cancellationToken);

            foreach (var schedule in schedules)
            {
                var key = (schedule.PropertyId, schedule.WeekStartDate);
                var isUpcoming = targetWeeks.TryGetValue(key, out var upcoming) ? upcoming : schedule.WeekStartDate > today;

                summaries.Add(new DailySummarySchedule
                {
                    PropertyName = string.IsNullOrWhiteSpace(schedule.Property?.Name) ? $"Property #{schedule.PropertyId}" : schedule.Property!.Name!,
                    Title = string.IsNullOrWhiteSpace(schedule.Title) ? "Weekly Schedule" : schedule.Title!,
                    WeekStart = schedule.WeekStartDate,
                    WeekEnd = schedule.WeekEndDate,
                    DetailPath = $"/Schedules?weekStart={schedule.WeekStartDate:yyyy-MM-dd}",
                    IsUpcoming = isUpcoming
                });
            }

            return summaries;
        }

        private static DateTime AlignToWeekStart(DateTime date, DayOfWeek startDay)
        {
            while (date.DayOfWeek != startDay)
            {
                date = date.AddDays(-1);
            }

            return date.Date;
        }

        private sealed class DailySummaryWorkOrder
        {
            public int Id { get; set; }
            public string Issue { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Location { get; set; }
            public string? DepartmentName { get; set; }
            public List<string> PropertyNames { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime DueDate { get; set; }
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryAnnouncement
        {
            public string PropertyName { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime? UpdatedAt { get; set; }
            public string? UpdatedByName { get; set; }
            public List<DailySummaryAttachment> Attachments { get; set; } = new();
        }

        private sealed class DailySummaryAttachment
        {
            public string FileName { get; set; } = string.Empty;
            public string DownloadPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryEvent
        {
            public string? Title { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public TimeSpan? StartTime { get; set; }
            public TimeSpan? EndTime { get; set; }
            public string CategoryName { get; set; } = string.Empty;
            public List<string> PropertyNames { get; set; } = new();
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryPackageLog
        {
            public int Id { get; set; }
            public string PropertyName { get; set; } = string.Empty;
            public string? RecipientName { get; set; }
            public string? RoomNumber { get; set; }
            public string? Carrier { get; set; }
            public string? TrackingNumber { get; set; }
            public string? StorageLocation { get; set; }
            public DateTime LoggedAt { get; set; }
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryLostFound
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string PropertyName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Type { get; set; }
            public DateTime CreatedAt { get; set; }
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummarySchedule
        {
            public string PropertyName { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public DateTime WeekStart { get; set; }
            public DateTime WeekEnd { get; set; }
            public string DetailPath { get; set; } = string.Empty;
            public bool IsUpcoming { get; set; }
        }

        private static string BuildUserDisplayName(ApplicationUser? user)
        {
            if (user == null)
            {
                return "there";
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

            return string.IsNullOrWhiteSpace(user.Email) ? "there" : user.Email!;
        }

        private static string BuildInquiryListHtml(SalesLeadSubmission lead)
        {
            var labels = (lead.InquiryTypes ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(key => key.Trim())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(GetInquiryLabel)
                .Select(WebUtility.HtmlEncode)
                .ToList();

            if (labels.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(string.Join("<br/>", labels));

            if (!string.IsNullOrWhiteSpace(lead.InquiryOtherDetails))
            {
                var encoded = WebUtility.HtmlEncode(lead.InquiryOtherDetails);
                builder.Append("<br/><em>Other:</em> ").Append(encoded);
            }

            return builder.ToString();
        }

        private static string GetInquiryLabel(string key)
        {
            if (SalesInquiryLabels.TryGetValue(key, out var label))
            {
                return label;
            }

            return key;
        }

        private static string BuildDateRangeDescription(DateTime? start, DateTime? end, TimeZoneInfo timeZone)
        {
            if (!start.HasValue && !end.HasValue)
            {
                return "Not provided";
            }

            if (start.HasValue && end.HasValue)
            {
                var startText = FormatUserLocal(start.Value, timeZone, "MMM d");
                var endText = FormatUserLocal(end.Value, timeZone, "MMM d");
                return $"{startText} - {endText}";
            }

            if (start.HasValue)
            {
                var startText = FormatUserLocal(start.Value, timeZone, "MMM d");
                return $"Starting {startText}";
            }

            var endTextOnly = FormatUserLocal(end!.Value, timeZone, "MMM d");
            return $"Ending {endTextOnly}";
        }

        private static string BuildBudgetDescription(decimal? min, decimal? max)
        {
            if (!min.HasValue && !max.HasValue)
            {
                return "Not provided";
            }

            var culture = CultureInfo.CurrentCulture;
            if (min.HasValue && max.HasValue)
            {
                var minText = min.Value.ToString("C0", culture);
                var maxText = max.Value.ToString("C0", culture);
                return $"{minText} - {maxText}";
            }

            if (min.HasValue)
            {
                var minText = min.Value.ToString("C0", culture);
                return $"{minText}+";
            }

            var maxOnly = max!.Value.ToString("C0", culture);
            return $"Up to {maxOnly}";
        }

        private static string BuildPlainTextHtml(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var encoded = WebUtility.HtmlEncode(value);
            return encoded
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal)
                .Replace("\r", "<br />", StringComparison.Ordinal);
        }

        private static DateTimeOffset GetNextRun(DateTimeOffset from)
        {
            var localNow = from.ToLocalTime();
            var target = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 6, 0, 0, localNow.Offset);
            if (localNow >= target)
            {
                target = target.AddDays(1);
            }

            return target.ToUniversalTime();
        }

        private static string BuildRichTextPreview(string? content, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var plainText = RichTextRenderer.ToPlainText(content);
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return string.Empty;
            }

            if (maxCharacters <= 0 || plainText.Length <= maxCharacters)
            {
                return RichTextRenderer.ToHtml(content);
            }

            var displayWithBreaks = RichTextRenderer.ToPlainTextWithLineBreaks(content);
            if (string.IsNullOrWhiteSpace(displayWithBreaks))
            {
                displayWithBreaks = plainText;
            }

            var truncated = displayWithBreaks[..Math.Min(maxCharacters, displayWithBreaks.Length)].TrimEnd();
            if (truncated.Length < displayWithBreaks.Length)
            {
                truncated = $"{truncated}...";
            }

            return BuildPlainTextHtml(truncated);
        }

        private static TimeZoneInfo ResolveUserTimeZone(ApplicationUser user)
        {
            var normalized = DefaultTimeZoneProvider.NormalizeForStorage(user.TimeZoneId);
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(normalized);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static string FormatUserLocal(DateTime utcDateTime, TimeZoneInfo timeZone, string format)
        {
            var utc = utcDateTime.Kind switch
            {
                DateTimeKind.Utc => utcDateTime,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
                DateTimeKind.Local => utcDateTime.ToUniversalTime(),
                _ => utcDateTime
            };

            var localized = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
            return localized.ToString(format, CultureInfo.CurrentCulture);
        }

        private static string BuildAbsoluteUrl(string relativeUrl, string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl))
            {
                return string.IsNullOrWhiteSpace(baseUrl) ? "/" : baseUrl!;
            }

            if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return relativeUrl;
            }

            return $"{baseUrl!.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
        }
    }
}

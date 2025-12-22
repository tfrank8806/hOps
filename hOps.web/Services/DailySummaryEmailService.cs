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

                if (!logs.Any() && !posts.Any() && !salesLeads.Any())
                {
                    user.DailySummaryLastSentUtc = dayStartUtc;
                    continue;
                }

                var body = BuildSummaryBody(user, summaryDate, logs, posts, salesLeads, _appBaseUrl);
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
            string? baseUrl)
        {
            var builder = new StringBuilder();
            var userName = BuildUserDisplayName(user);
            var safeName = WebUtility.HtmlEncode(userName);
            var userTimeZone = ResolveUserTimeZone(user);

            builder.AppendLine($@"<p>Hello {safeName},</p>");
            builder.AppendLine($@"<p>Here is your activity summary for {summaryDate:MMMM d, yyyy}.</p>");

            if (logs.Any())
            {
                builder.AppendLine(@"<h3 style=""margin-top:1.5rem;"">Pass On Logs</h3>");
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

            if (posts.Any())
            {
                builder.AppendLine(@"<h3 style=""margin-top:1.5rem;"">Bulletin Board</h3>");
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

            if (salesLeads.Any())
            {
                builder.AppendLine(@"<h3 style=""margin-top:1.5rem;"">Sales Leads</h3>");
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

            builder.AppendLine(@"<p style=""margin-top:1.5rem;"">You are receiving this email because daily summaries are enabled in your profile preferences.</p>");

            return builder.ToString();
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

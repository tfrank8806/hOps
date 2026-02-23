using System;
using System.Collections.Generic;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailySummaryEmailService> _logger;
        private readonly IConfiguration _configuration;

        private static readonly TimeSpan ExecutionInterval = TimeSpan.FromHours(1);
        private const int MinimumHourToSendLocal = 5;

        public DailySummaryEmailService(
            IServiceProvider serviceProvider,
            ILogger<DailySummaryEmailService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendSummariesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process daily summary emails.");
                }

                try
                {
                    await Task.Delay(ExecutionInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // shutting down
                    break;
                }
            }
        }

        private async Task SendSummariesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var now = DateTime.UtcNow;
            var throttleCutoff = now.AddHours(-22);
            var baseUrl = (_configuration["App:BaseUrl"] ?? _configuration["AppBaseUrl"])?.TrimEnd('/');

            var subscribers = await dbContext.Users
                .Include(u => u.EmailPropertySubscriptions)
                .Include(u => u.UserPropertyAccesses)
                .Where(u => u.EmailDailySummary && !string.IsNullOrWhiteSpace(u.Email))
                .ToListAsync(cancellationToken);

            var updatesMade = false;

            foreach (var user in subscribers)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (user.DailySummaryLastSentUtc.HasValue && user.DailySummaryLastSentUtc.Value > throttleCutoff)
                {
                    continue;
                }

                var userTimeZone = ResolveTimeZone(user.TimeZoneId);
                var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, userTimeZone);

                if (localNow.Hour < MinimumHourToSendLocal)
                {
                    continue;
                }

                var summaryDateLocal = localNow.Date.AddDays(-1);
                var summaryStartLocal = summaryDateLocal;
                var summaryEndLocal = summaryDateLocal.AddDays(1);

                var summaryStartUtc = SafeConvertToUtc(summaryStartLocal, userTimeZone);
                var summaryEndUtc = SafeConvertToUtc(summaryEndLocal, userTimeZone);

                var allowedPropertyIds = GetAllowedPropertyIds(user);
                if (allowedPropertyIds.Count == 0)
                {
                    continue;
                }

                var logs = await dbContext.PassOnLogs
                    .AsNoTracking()
                    .Include(l => l.CreatedBy)
                    .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                    .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                    .Where(l => l.CreatedAt >= summaryStartUtc && l.CreatedAt < summaryEndUtc)
                    .Where(l => l.Properties.Any(p => allowedPropertyIds.Contains(p.PropertyId)))
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync(cancellationToken);

                var subject = $"Daily recap for {summaryDateLocal:MMM d}";
                var htmlBody = BuildEmailBody(user, logs, summaryDateLocal, userTimeZone, baseUrl);

                try
                {
                    await emailSender.SendEmailAsync(user.Email!, subject, htmlBody);
                    user.DailySummaryLastSentUtc = now;
                    updatesMade = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send daily summary to user {UserId}", user.Id);
                }
            }

            if (updatesMade)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        private static HashSet<int> GetAllowedPropertyIds(ApplicationUser user)
        {
            var allowed = (user.EmailPropertySubscriptions ?? Array.Empty<UserPropertyEmailSubscription>())
                .Where(s => s.IncludeInDailySummary)
                .Select(s => s.PropertyId)
                .ToHashSet();

            if (allowed.Count > 0)
            {
                return allowed;
            }

            return (user.UserPropertyAccesses ?? Array.Empty<UserPropertyAccess>())
                .Select(upa => upa.PropertyId)
                .ToHashSet();
        }

        private static TimeZoneInfo ResolveTimeZone(string? userTimeZoneId)
        {
            var normalized = DefaultTimeZoneProvider.NormalizeForStorage(userTimeZoneId);
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

        private static DateTime SafeConvertToUtc(DateTime localTime, TimeZoneInfo timeZone)
        {
            try
            {
                return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
            }
            catch (ArgumentException)
            {
                return DateTime.SpecifyKind(localTime, DateTimeKind.Utc);
            }
        }

        private static string BuildEmailBody(
            ApplicationUser user,
            IReadOnlyCollection<PassOnLog> logs,
            DateTime summaryDateLocal,
            TimeZoneInfo userTimeZone,
            string? baseUrl)
        {
            var builder = new StringBuilder();
            var friendlyDate = summaryDateLocal.ToString("dddd, MMMM d");
            var friendlyCount = logs.Count == 1 ? "1 log" : $"{logs.Count} logs";
            var greetingName = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : WebUtility.HtmlEncode(user.FirstName.Trim());
            var settingsUrl = string.IsNullOrWhiteSpace(baseUrl) ? "/Profile" : $"{baseUrl}/Profile";

            builder.AppendLine(@"<div style=""font-family:'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;background:#f3f4f6;padding:32px 16px;"">");
            builder.AppendLine(@"<div style=""max-width:640px;margin:0 auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 20px 45px rgba(15,23,42,0.15);"">");
            builder.AppendLine($@"
    <div style=""background:linear-gradient(135deg,#0f62fe,#4f46e5);color:#fff;padding:28px 32px 36px;"">
        <p style=""margin:0 0 8px;font-size:15px;letter-spacing:0.08em;text-transform:uppercase;opacity:0.9;"">Daily recap</p>
        <h1 style=""margin:0 0 6px;font-size:28px;font-weight:600;"">{friendlyDate}</h1>
        <p style=""margin:0;font-size:16px;opacity:0.85;"">{friendlyCount} from your properties</p>
    </div>");

            builder.AppendLine(@"    <div style=""padding:28px 32px 32px;"">");
            builder.AppendLine($@"<p style=""margin:0 0 16px;font-size:16px;color:#111827;"">Hi {greetingName}, here&apos;s what your teams shared yesterday.</p>");

            if (!logs.Any())
            {
                builder.AppendLine(@"<div style=""border:1px dashed #c7d2fe;border-radius:16px;padding:24px;text-align:center;background:#eef2ff;color:#3730a3;font-size:15px;"">
    <strong>No updates to report.</strong>
    <p style=""margin:12px 0 0;color:#4c1d95;font-size:14px;"">Once new pass on logs are posted, we&apos;ll highlight them here.</p>
</div>");
            }
            else
            {
                builder.AppendLine($@"<p style=""margin:0 0 12px;font-size:14px;color:#6b7280;"">{logs.Count} pass on {(logs.Count == 1 ? "log was" : "logs were")} posted for the properties you follow.</p>");

                foreach (var log in logs)
                {
                    var authorName = PassOnLogEmailHelper.FormatUserName(log.CreatedBy);
                    var author = WebUtility.HtmlEncode(authorName);
                    var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(log.CreatedAt, userTimeZone);
                    var localTimestamp = createdLocal.ToString("MMM d, h:mm tt");
                    var safeTitle = WebUtility.HtmlEncode(log.Title);
                    var link = BuildLogLink(baseUrl, log.Id);
                    var propertyNames = log.Properties
                        .Select(lp => lp.Property?.Name ?? $"Property #{lp.PropertyId}")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var propertyBadges = propertyNames.Any()
                        ? string.Join(string.Empty, propertyNames.Select(name => $@"<span style=""display:inline-block;margin:0 8px 8px 0;padding:4px 10px;border-radius:999px;background:#eef2ff;color:#4338ca;font-size:12px;font-weight:600;"">{WebUtility.HtmlEncode(name)}</span>"))
                        : string.Empty;
                    var detailHtml = PassOnLogEmailHelper.BuildLogEmailBody(log, link, propertyNames, log.Comments, userTimeZone);

                    builder.AppendLine($@"
<div style=""border:1px solid #e5e7eb;border-radius:16px;padding:20px 24px;margin-bottom:20px;background:#fff;"">
    <div style=""display:flex;justify-content:space-between;flex-wrap:wrap;gap:8px;margin-bottom:12px;font-size:13px;color:#6b7280;"">
        <span>Posted by <strong style=""color:#111827;"">{author}</strong></span>
        <span>{localTimestamp}</span>
    </div>
    {(string.IsNullOrEmpty(propertyBadges) ? string.Empty : $@"<div style=""margin-bottom:12px;"">{propertyBadges}</div>")}
    <h2 style=""margin:0 0 10px;font-size:18px;color:#111827;"">{safeTitle}</h2>
    <div style=""font-size:14px;line-height:1.6;color:#374151;"">{detailHtml}</div>
    <div style=""margin-top:16px;"">
        <a href=""{link}"" style=""display:inline-block;padding:10px 18px;background:#111827;color:#fff;text-decoration:none;border-radius:999px;font-size:14px;"">Open log</a>
    </div>
</div>");
                }
            }

            builder.AppendLine($@"<p style=""margin:24px 0 0;font-size:13px;color:#9ca3af;"">
You are receiving this email because you opted into daily summaries.
You can <a href=""{settingsUrl}"" style=""color:#6366f1;text-decoration:none;"">update your preferences</a> anytime.
</p>");
            builder.AppendLine("    </div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");

            return builder.ToString();
        }

        private static string BuildLogLink(string? baseUrl, int logId)
        {
            var relativePath = $"/PassOnLogs/Details/{logId}";
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return relativePath;
            }

            return $"{baseUrl}{relativePath}";
        }
    }
}

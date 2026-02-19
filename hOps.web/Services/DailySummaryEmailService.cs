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
            var friendlyDate = summaryDateLocal.ToString("MMMM d");
            var greetingName = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName.Trim();

            builder.AppendLine($@"<p style=""font-size:16px;margin-bottom:8px;"">Hi {WebUtility.HtmlEncode(greetingName)},</p>");
            builder.AppendLine($@"<p style=""margin-top:0;color:#374151;"">Here&apos;s your pass on log recap for <strong>{friendlyDate}</strong>.</p>");

            if (!logs.Any())
            {
                builder.AppendLine(@"<p style=""color:#6b7280;"">No pass on logs were posted yesterday for the properties you follow.</p>");
                return builder.ToString();
            }

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

                builder.AppendLine($@"
<div style=""border:1px solid #e5e7eb;border-radius:12px;padding:16px;margin-bottom:16px;"">
    <h4 style=""margin:0 0 8px;font-size:16px;"">{safeTitle}</h4>
    <p style=""margin:0 0 12px;font-size:13px;color:#6b7280;"">Posted by {author} on {localTimestamp}.</p>
    {PassOnLogEmailHelper.BuildLogEmailBody(log, link, propertyNames, log.Comments)}
</div>");
            }

            builder.AppendLine("<p style=\"font-size:13px;color:#9ca3af;margin-top:24px;\">You are receiving this email because you opted into daily summaries. Update your preferences in your profile settings.</p>");
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

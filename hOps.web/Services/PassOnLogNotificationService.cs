using System.Net;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services
{
    public interface IPassOnLogNotificationService
    {
        Task<List<ApplicationUser>> GetLogEntryAlertRecipientsAsync(PassOnLog log, ApplicationUser actor);
        Task NotifyLogSubscribersAsync(PassOnLog log, ApplicationUser actor, string linkUrl, List<ApplicationUser> recipients);
        Task SendLogEntryEmailsAsync(PassOnLog log, ApplicationUser actor, string linkUrl, List<ApplicationUser> recipients);
        Task SendLogCommentEmailsAsync(PassOnLog log, PassOnLogComment comment, ApplicationUser actor, string linkUrl, List<ApplicationUser> recipients);
    }

    public class PassOnLogNotificationService : IPassOnLogNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IRealtimeNotificationService _realtimeNotifications;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<PassOnLogNotificationService> _logger;

        public PassOnLogNotificationService(
            ApplicationDbContext context,
            IRealtimeNotificationService realtimeNotifications,
            IEmailSender emailSender,
            ILogger<PassOnLogNotificationService> logger)
        {
            _context = context;
            _realtimeNotifications = realtimeNotifications;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<List<ApplicationUser>> GetLogEntryAlertRecipientsAsync(PassOnLog log, ApplicationUser actor)
        {
            await _context.Entry(log)
                .Collection(l => l.Properties)
                .Query()
                .Include(lp => lp.Property)
                .LoadAsync();

            var propertyIds = log.Properties
                .Select(lp => lp.PropertyId)
                .Distinct()
                .ToList();

            var candidateUsers = await _context.Users
                .Where(u => !string.Equals(u.Id, actor.Id))
                .Select(u => new
                {
                    User = u,
                    PropertyPreferences = u.EmailPropertySubscriptions.Select(s => new { s.PropertyId, s.IncludeInLogAlerts }),
                    AccessIds = u.UserPropertyAccesses!.Select(upa => upa.PropertyId)
                })
                .ToListAsync();

            return candidateUsers
                .Where(candidate =>
                {
                    var allowedProperties = candidate.PropertyPreferences
                        .Where(p => p.IncludeInLogAlerts)
                        .Select(p => p.PropertyId)
                        .ToHashSet();

                    if (!allowedProperties.Any())
                    {
                        allowedProperties = candidate.AccessIds.ToHashSet();
                    }

                    if (!allowedProperties.Any())
                    {
                        return false;
                    }

                    if (!propertyIds.Any())
                    {
                        return true;
                    }

                    return propertyIds.Any(pid => allowedProperties.Contains(pid));
                })
                .Select(candidate => candidate.User)
                .ToList();
        }

        public async Task NotifyLogSubscribersAsync(
            PassOnLog log,
            ApplicationUser actor,
            string linkUrl,
            List<ApplicationUser> recipients)
        {
            if (!recipients.Any())
            {
                return;
            }

            var actorName = PassOnLogEmailHelper.FormatUserName(actor.FirstName, actor.LastName, actor.Email ?? string.Empty);
            var now = DateTime.UtcNow;

            foreach (var recipient in recipients)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = recipient.Id,
                    Type = "passon-log",
                    Title = "New pass-on log",
                    Content = $"{actorName} posted \"{log.Title}\"",
                    LinkUrl = linkUrl,
                    PassOnLogId = log.Id,
                    CreatedAt = now,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            var payload = new RealtimeNotificationPayload(
                "New pass-on log",
                $"{actorName} posted \"{log.Title}\"",
                linkUrl,
                "log");

            await _realtimeNotifications.NotifyUsersAsync(
                recipients.Select(r => r.Id),
                payload);
        }

        public async Task SendLogEntryEmailsAsync(
            PassOnLog log,
            ApplicationUser actor,
            string linkUrl,
            List<ApplicationUser> recipients)
        {
            var emailRecipients = recipients
                .Where(r => r.EmailOnLogEntry && !string.IsNullOrWhiteSpace(r.Email))
                .ToList();

            if (!emailRecipients.Any())
            {
                return;
            }

            var propertyNames = log.Properties
                .Select(lp => lp.Property?.Name ?? $"Property #{lp.PropertyId}")
                .Distinct()
                .ToList();

            var actorName = PassOnLogEmailHelper.FormatUserName(actor.FirstName, actor.LastName, actor.Email ?? string.Empty);
            var subject = $"New log: {log.Title}";
            var introHtml = $@"<p>{WebUtility.HtmlEncode(actorName)} posted a new log titled <strong>{WebUtility.HtmlEncode(log.Title)}</strong>.</p>";

            foreach (var recipient in emailRecipients)
            {
                try
                {
                    var recipientTimeZone = ResolveUserTimeZone(recipient);
                    var htmlBody = introHtml + PassOnLogEmailHelper.BuildLogEmailBody(
                        log,
                        linkUrl,
                        propertyNames,
                        log.Comments,
                        recipientTimeZone);

                    await _emailSender.SendEmailAsync(recipient.Email!, subject, htmlBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send log email notification to user {UserId}", recipient.Id);
                }
            }
        }

        public async Task SendLogCommentEmailsAsync(
            PassOnLog log,
            PassOnLogComment comment,
            ApplicationUser actor,
            string linkUrl,
            List<ApplicationUser> recipients)
        {
            var emailRecipients = recipients
                .Where(r => r.EmailOnLogEntry && !string.IsNullOrWhiteSpace(r.Email))
                .ToList();

            if (!emailRecipients.Any())
            {
                return;
            }

            var propertyNames = log.Properties
                .Select(lp => lp.Property?.Name ?? $"Property #{lp.PropertyId}")
                .Distinct()
                .ToList();

            var actorName = PassOnLogEmailHelper.FormatUserName(actor.FirstName, actor.LastName, actor.Email ?? string.Empty);
            var subject = $"New comment on: {log.Title}";
            var introHtml = $@"<p>{WebUtility.HtmlEncode(actorName)} added a new comment on <strong>{WebUtility.HtmlEncode(log.Title)}</strong>.</p>";

            foreach (var recipient in emailRecipients)
            {
                try
                {
                    var recipientTimeZone = ResolveUserTimeZone(recipient);
                    var htmlBody = introHtml + PassOnLogEmailHelper.BuildLogEmailBody(
                        log,
                        linkUrl,
                        propertyNames,
                        log.Comments,
                        recipientTimeZone);

                    await _emailSender.SendEmailAsync(recipient.Email!, subject, htmlBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send log comment email notification to user {UserId}", recipient.Id);
                }
            }
        }

        private static TimeZoneInfo ResolveUserTimeZone(ApplicationUser? user)
        {
            var normalized = DefaultTimeZoneProvider.NormalizeForStorage(user?.TimeZoneId);
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
    }
}

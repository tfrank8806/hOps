using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services
{
    public class MentionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<MentionService> _logger;

        public MentionService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<MentionService> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<List<MentionSuggestion>> SearchUsersAsync(string term, int maxResults = 8)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<MentionSuggestion>();
            }

            term = term.Trim();

            return await _userManager.Users
                .Where(u =>
                    (u.FirstName != null && EF.Functions.Like(u.FirstName, $"%{term}%")) ||
                    (u.LastName != null && EF.Functions.Like(u.LastName, $"%{term}%")) ||
                    (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%")) ||
                    (u.UserName != null && EF.Functions.Like(u.UserName, $"%{term}%")))
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Take(maxResults)
                .Select(u => new MentionSuggestion(
                    u.Id,
                    BuildDisplayName(u),
                    string.Empty))
                .ToListAsync();
        }

        public IEnumerable<MentionMarkupFormatter.MentionReference> ExtractMentions(string? content)
        {
            return MentionMarkupFormatter.ExtractMentions(content);
        }

        public async Task CreateMentionNotificationsAsync(
            string? content,
            ApplicationUser actor,
            string contextTitle,
            string linkUrl,
            string? excerpt = null)
        {
            var mentions = ExtractMentions(content).ToList();
            if (!mentions.Any())
            {
                return;
            }

            var userIds = mentions
                .Select(m => m.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, actor.Id, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!userIds.Any())
            {
                return;
            }

            var mentionedUsers = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            if (!mentionedUsers.Any())
            {
                return;
            }

            var now = DateTime.UtcNow;
            var preview = string.IsNullOrWhiteSpace(excerpt)
                ? MentionMarkupFormatter.ToDisplayText(content)
                : excerpt!;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 240)
            {
                preview = $"{preview[..240]}…";
            }

            var actorName = BuildDisplayName(actor);

            foreach (var user in mentionedUsers)
            {
                try
                {
                    _context.UserNotifications.Add(new UserNotification
                    {
                        UserId = user.Id,
                        Type = "mention",
                        Title = $"{actorName} mentioned you",
                        Content = string.IsNullOrWhiteSpace(preview) ? contextTitle : $"{contextTitle}: {preview}",
                        LinkUrl = linkUrl,
                        CreatedAt = now,
                        IsRead = false
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to create mention notification for user {UserId}", user.Id);
                }
            }

            await _context.SaveChangesAsync();

            foreach (var user in mentionedUsers)
            {
                await SendMentionEmailAsync(user, actor, contextTitle, linkUrl, preview);
            }
        }

        private async Task SendMentionEmailAsync(ApplicationUser recipient, ApplicationUser actor, string contextTitle, string linkUrl, string? preview)
        {
            if (!recipient.EmailOnMention || string.IsNullOrWhiteSpace(recipient.Email))
            {
                return;
            }

            var actorName = BuildDisplayName(actor);
            var safeActor = WebUtility.HtmlEncode(actorName);
            var safeContext = WebUtility.HtmlEncode(contextTitle);

            var trimmedPreview = preview;
            if (!string.IsNullOrWhiteSpace(trimmedPreview) && trimmedPreview.Length > 240)
            {
                trimmedPreview = $"{trimmedPreview[..240]}…";
            }

            var safePreview = string.IsNullOrWhiteSpace(trimmedPreview)
                ? null
                : WebUtility.HtmlEncode(trimmedPreview);

            var htmlBody = safePreview == null
                ? $"""
                    <p>{safeActor} mentioned you in <strong>{safeContext}</strong>.</p>
                    <p><a href="{linkUrl}">Open HotelOps to see the conversation.</a></p>
                    """
                : $"""
                    <p>{safeActor} mentioned you in <strong>{safeContext}</strong>.</p>
                    <p style="margin:0 0 1rem 0;">{safePreview}</p>
                    <p><a href="{linkUrl}">Open HotelOps to see the conversation.</a></p>
                    """;

            try
            {
                await _emailSender.SendEmailAsync(recipient.Email!, $"{actorName} mentioned you", htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to send mention email notification to user {UserId}", recipient.Id);
            }
        }

        private static string BuildDisplayName(ApplicationUser user)
        {
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

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }

            return user.UserName ?? "User";
        }
    }

    public readonly record struct MentionSuggestion(string Id, string DisplayName, string Email);
}

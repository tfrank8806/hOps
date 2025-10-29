using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services
{
    public class MentionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MentionService> _logger;

        public MentionService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<MentionService> logger)
        {
            _context = context;
            _userManager = userManager;
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

            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, Name = BuildDisplayName(u) })
                .ToListAsync();

            if (!users.Any())
            {
                return;
            }

            var now = DateTime.UtcNow;
            var preview = string.IsNullOrWhiteSpace(excerpt)
                ? MentionMarkupFormatter.ToDisplayText(content)
                : excerpt!;

            foreach (var user in users)
            {
                try
                {
                    _context.UserNotifications.Add(new UserNotification
                    {
                        UserId = user.Id,
                        Type = "mention",
                        Title = $"{BuildDisplayName(actor)} mentioned you",
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

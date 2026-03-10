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

        public async Task<List<MentionSuggestion>> SearchEntitiesAsync(ApplicationUser? actor, string term, int maxResults = 8)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<MentionSuggestion>();
            }

            var normalizedTerm = term.Trim();
            if (normalizedTerm.Length == 0)
            {
                return new List<MentionSuggestion>();
            }

            normalizedTerm = normalizedTerm.ToLowerInvariant();
            var likePattern = $"%{normalizedTerm}%";

            var suggestions = new List<MentionSuggestion>();

            var users = await _userManager.Users
                .Where(u =>
                    (!string.IsNullOrEmpty(u.FirstName) && EF.Functions.Like(u.FirstName.ToLower(), likePattern)) ||
                    (!string.IsNullOrEmpty(u.LastName) && EF.Functions.Like(u.LastName.ToLower(), likePattern)) ||
                    (!string.IsNullOrEmpty(u.Email) && EF.Functions.Like(u.Email.ToLower(), likePattern)) ||
                    (!string.IsNullOrEmpty(u.UserName) && EF.Functions.Like(u.UserName.ToLower(), likePattern)))
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Take(maxResults)
                .Select(u => new MentionSuggestion(
                    u.Id,
                    BuildDisplayName(u),
                    string.IsNullOrWhiteSpace(u.Email) ? (u.UserName ?? string.Empty) : u.Email!,
                    "user"))
                .ToListAsync();

            if (actor != null)
            {
                users = users
                    .Where(s => !string.Equals(s.Id, actor.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            suggestions.AddRange(users);

            var accessiblePropertyIds = actor == null
                ? new List<int>()
                : await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == actor.Id)
                    .Select(upa => upa.PropertyId)
                    .Distinct()
                    .ToListAsync();

            var departmentQuery = _context.Departments.AsQueryable();
            if (accessiblePropertyIds.Any())
            {
                departmentQuery = departmentQuery.Where(d => !d.PropertyId.HasValue || accessiblePropertyIds.Contains(d.PropertyId.Value));
            }
            else
            {
                departmentQuery = departmentQuery.Where(d => !d.PropertyId.HasValue);
            }

            departmentQuery = departmentQuery
                .Where(d => d.Name != null && EF.Functions.Like(d.Name!.ToLower(), likePattern))
                .OrderBy(d => d.Name)
                .Take(maxResults);

            var departmentResults = await departmentQuery
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    PropertyName = d.Property != null
                        ? (string.IsNullOrWhiteSpace(d.Property.Code)
                            ? d.Property.Name
                            : $"{d.Property.Name} ({d.Property.Code})")
                        : null
                })
                .ToListAsync();

            foreach (var department in departmentResults)
            {
                var displayName = string.IsNullOrWhiteSpace(department.Name)
                    ? $"Department #{department.Id}"
                    : department.Name!;
                suggestions.Add(new MentionSuggestion(
                    $"department:{department.Id}",
                    displayName,
                    department.PropertyName ?? "All properties",
                    "department"));
            }

            return suggestions;
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

            var directUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var departmentIds = new HashSet<int>();

            foreach (var mention in mentions)
            {
                if (string.IsNullOrWhiteSpace(mention.UserId))
                {
                    continue;
                }

                if (TryParseDepartmentIdentifier(mention.UserId, out var deptId))
                {
                    departmentIds.Add(deptId);
                    continue;
                }

                if (!string.Equals(mention.UserId, actor.Id, StringComparison.OrdinalIgnoreCase))
                {
                    directUserIds.Add(mention.UserId);
                }
            }

            var mentionedUsers = directUserIds.Any()
                ? await _userManager.Users
                    .Where(u => directUserIds.Contains(u.Id))
                    .ToListAsync()
                : new List<ApplicationUser>();

            var departmentUsers = new List<ApplicationUser>();
            if (departmentIds.Any())
            {
                var departmentUserIds = await _context.UserDepartmentSubscriptions
                    .Where(s => departmentIds.Contains(s.DepartmentId))
                    .Select(s => s.UserId)
                    .Where(userId => !string.Equals(userId, actor.Id, StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToListAsync();

                if (departmentUserIds.Any())
                {
                    departmentUsers = await _userManager.Users
                        .Where(u => departmentUserIds.Contains(u.Id))
                        .ToListAsync();
                }
            }

            var allRecipients = mentionedUsers
                .Concat(departmentUsers)
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();

            if (!allRecipients.Any())
            {
                return;
            }

            var now = DateTime.UtcNow;
            var preview = string.IsNullOrWhiteSpace(excerpt)
                ? RichTextRenderer.ToPlainText(content)
                : excerpt!;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 240)
            {
                preview = $"{preview[..240]}…";
            }

            var actorName = BuildDisplayName(actor);

            foreach (var user in allRecipients)
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

            foreach (var user in allRecipients)
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

        private static bool TryParseDepartmentIdentifier(string identifier, out int departmentId)
        {
            departmentId = 0;
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            const string prefix = "department:";
            if (!identifier.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var numericPart = identifier.Substring(prefix.Length);
            return int.TryParse(numericPart, out departmentId);
        }
    }

    public readonly record struct MentionSuggestion(string Id, string DisplayName, string? Description, string Type);
}

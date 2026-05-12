using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

public class BaseController : Controller
{
    protected readonly ApplicationDbContext _context;
    protected readonly UserManager<ApplicationUser> _userManager;

    public BaseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        InitializeViewBagDefaults();

        var httpContext = context.HttpContext;

        try
        {
            var languagePreferenceService = httpContext.RequestServices.GetRequiredService<hOps.web.Services.Localization.ILanguagePreferenceService>();
            var translationService = httpContext.RequestServices.GetRequiredService<hOps.web.Services.Localization.ITranslationService>();
            var cancellationToken = httpContext.RequestAborted;
            var preferredLanguage = await languagePreferenceService.GetPreferredLanguageAsync(User, cancellationToken);
            httpContext.Items["ActiveLanguage"] = preferredLanguage;

            if (string.IsNullOrWhiteSpace(languagePreferenceService.GetPreferredLanguageFromCookie()))
            {
                languagePreferenceService.SetPreferredLanguageCookie(preferredLanguage);
            }
            ViewBag.ActiveLanguage = preferredLanguage;
            ViewBag.DefaultLanguage = translationService.DefaultLanguage;
            ViewBag.SupportedLanguages = translationService.SupportedLanguages;
            ViewBag.ActiveLanguageDisplayName = translationService.SupportedLanguages
                .FirstOrDefault(l => string.Equals(l.Code, preferredLanguage, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? "English";
        }
        catch (Exception ex)
        {
            LogLayoutError(httpContext, ex, "Failed to determine preferred language for {Path}", httpContext?.Request.Path.Value ?? "(unknown)");
            ViewBag.ActiveLanguage = hOps.web.Localization.LanguageConstants.English;
            ViewBag.DefaultLanguage = hOps.web.Localization.LanguageConstants.English;
            ViewBag.SupportedLanguages = hOps.web.Localization.LanguageConstants.SupportedLanguages;
            ViewBag.ActiveLanguageDisplayName = "English";
            httpContext.Items["ActiveLanguage"] = hOps.web.Localization.LanguageConstants.English;
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var props = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == user.Id)
                    .Include(upa => upa.Property)
                    .Select(upa => upa.Property)
                    .ToListAsync();

                var userProperties = props
                    .Where(p => p != null)
                    .Select(p => p!)
                    .ToList();

                ViewBag.UserProperties = userProperties;
                ViewBag.CurrentUserId = user.Id;
                var displayName = $"{user.FirstName} {user.LastName}".Trim();
                httpContext.Session.SetString("CurrentUserId", user.Id);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = user.Email ?? user.UserName ?? "My Profile";
                }
                ViewBag.CurrentUserAvatar = UserAvatarHelper.BuildFromUser(user, displayName);

                int? currentPropertyId = httpContext.Session.GetInt32("CurrentPropertyId");
                Property? currentProperty = currentPropertyId.HasValue
                    ? userProperties.FirstOrDefault(p => p.Id == currentPropertyId.Value)
                    : null;

                if (currentProperty == null && user.DefaultPropertyId.HasValue)
                {
                    currentProperty = userProperties.FirstOrDefault(p => p.Id == user.DefaultPropertyId.Value);
                    if (currentProperty != null)
                    {
                        httpContext.Session.SetInt32("CurrentPropertyId", currentProperty.Id);
                    }
                }

                if (currentProperty == null && userProperties.Any())
                {
                    currentProperty = userProperties.First();
                    httpContext.Session.SetInt32("CurrentPropertyId", currentProperty.Id);
                }
                else if (currentProperty == null)
                {
                    httpContext.Session.Remove("CurrentPropertyId");
                }

                ViewBag.CurrentProperty = currentProperty;

                var normalizedTimeZoneId = DefaultTimeZoneProvider.NormalizeForStorage(user.TimeZoneId);
                httpContext.Items["UserTimeZoneId"] = normalizedTimeZoneId;
                httpContext.Session.SetString("UserTimeZoneId", normalizedTimeZoneId);

                var shouldLoadSidebarChrome = ShouldLoadSidebarChrome(httpContext);

                if (shouldLoadSidebarChrome)
                {
                    await PopulateDirectMessageBadgeSafelyAsync(user, httpContext);
                    await BuildToDoSidebarSafelyAsync(user, userProperties, httpContext);
                }
            }
        }
        catch (Exception ex)
        {
            LogLayoutError(httpContext, ex, "Failed to prepare shared layout state for {Path}", httpContext?.Request.Path.Value ?? "(unknown)");
        }

        await next();
    }

    private async Task PopulateDirectMessageBadgeSafelyAsync(ApplicationUser user, HttpContext httpContext)
    {
        try
        {
            await PopulateDirectMessageBadgeAsync(user);
        }
        catch (Exception ex)
        {
            LogLayoutError(httpContext, ex, "Unable to populate direct message badge for user {UserId}", user.Id);
            InitializeDirectMessageDefaults();
        }
    }

    private async Task BuildToDoSidebarSafelyAsync(ApplicationUser user, List<Property> userProperties, HttpContext httpContext)
    {
        try
        {
            ViewBag.ToDoSidebarData = await BuildToDoSidebarAsync(user, userProperties);
        }
        catch (Exception ex)
        {
            LogLayoutError(httpContext, ex, "Unable to build to-do sidebar for user {UserId}", user.Id);
            ViewBag.ToDoSidebarData = new ToDoSidebarViewModel();
        }
    }

        protected async Task PopulateDirectMessageBadgeAsync(ApplicationUser user)
        {
            var access = await GetMessagingAccessContextAsync(user);
            var allowedUserIds = access.AllowedUserIds.ToList();
            var restrictToAllowedUsers = !access.IsAdmin;

            ViewBag.UnreadDirectMessageCount = 0;
            ViewBag.UnreadAlertCount = 0;
            ViewBag.UnreadMessageCenterCount = 0;
            ViewBag.LatestDirectMessageParticipant = null;
            ViewBag.LatestDirectMessageBody = null;
            ViewBag.LatestDirectMessageSentAt = null;
            ViewBag.LatestDirectMessageConversationId = null;

            var counts = await GetMessageCenterCountsAsync(user, access);
            ViewBag.UnreadDirectMessageCount = counts.UnreadConversations;
            ViewBag.UnreadAlertCount = counts.UnreadAlerts;
            ViewBag.UnreadMessageCenterCount = counts.UnreadConversations + counts.UnreadAlerts;

            if (restrictToAllowedUsers && allowedUserIds.Count == 0)
            {
                return;
            }

            var latestMessageQuery = _context.DirectMessages
                .Where(m => m.RecipientId == user.Id || m.SenderId == user.Id);

            if (restrictToAllowedUsers)
            {
                latestMessageQuery = latestMessageQuery.Where(m =>
                    (m.SenderId == user.Id && allowedUserIds.Contains(m.RecipientId)) ||
                    (m.RecipientId == user.Id && allowedUserIds.Contains(m.SenderId)));
            }

            var latestMessage = await latestMessageQuery
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.SentAt,
                    m.Body,
                    m.SenderId,
                    m.RecipientId,
                    m.ConversationId
                })
                .FirstOrDefaultAsync();

            if (latestMessage == null)
            {
                return;
            }

            var otherUserId = string.Equals(latestMessage.SenderId ?? user.Id, user.Id, StringComparison.OrdinalIgnoreCase)
                ? (latestMessage.RecipientId ?? user.Id)
                : (latestMessage.SenderId ?? user.Id);

            var otherUser = await _userManager.Users
                .Where(u => u.Id == otherUserId)
                .Select(u => new { u.FirstName, u.LastName, u.Email, u.UserName })
                .FirstOrDefaultAsync();

            string? participantName = null;
            if (otherUser != null)
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(otherUser.FirstName))
                {
                    parts.Add(otherUser.FirstName);
                }
                if (!string.IsNullOrWhiteSpace(otherUser.LastName))
                {
                    parts.Add(otherUser.LastName);
                }

                participantName = parts.Count > 0
                    ? string.Join(" ", parts)
                    : (otherUser.Email ?? otherUser.UserName ?? "Teammate");
            }

            ViewBag.LatestDirectMessageParticipant = participantName;
            ViewBag.LatestDirectMessageBody = latestMessage.Body ?? string.Empty;
            ViewBag.LatestDirectMessageSentAt = latestMessage.SentAt;
            ViewBag.LatestDirectMessageConversationId = latestMessage.ConversationId;
        }

    private async Task<ToDoSidebarViewModel?> BuildToDoSidebarAsync(ApplicationUser? user, List<Property>? accessibleProperties)
    {
        if (user == null)
        {
            return null;
        }

        var propertyIds = accessibleProperties?
            .Select(p => p.Id)
            .Where(id => id > 0)
            .ToList() ?? new List<int>();

        var departmentIds = await _context.UserDepartmentSubscriptions
            .Where(s => s.UserId == user.Id)
            .Select(s => s.DepartmentId)
            .ToListAsync();

        var model = new ToDoSidebarViewModel
        {
            HasDepartmentAssignments = departmentIds.Any()
        };

        if (departmentIds.Any())
        {
            var departmentWorkOrders = await _context.WorkOrders
                .Where(w => w.DepartmentId.HasValue && departmentIds.Contains(w.DepartmentId.Value))
                .Where(w => w.Status != "Completed" && w.Status != "Cancelled")
                .Where(w => w.Properties.Any(p => propertyIds.Contains(p.PropertyId)))
                .Include(w => w.Department)
                .Include(w => w.Properties).ThenInclude(p => p.Property)
                .OrderBy(w => w.DueDate == default ? DateTime.MaxValue : w.DueDate)
                .ThenByDescending(w => w.CreatedAt)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            model.DepartmentTasks = departmentWorkOrders.Select(w =>
            {
                var propertyLabel = w.Properties
                    .Select(p => p.Property != null
                        ? (string.IsNullOrWhiteSpace(p.Property.Code)
                            ? p.Property.Name
                            : $"{p.Property.Name} ({p.Property.Code})")
                        : $"Property #{p.PropertyId}")
                    .FirstOrDefault();

                return new DepartmentWorkOrderTaskViewModel
                {
                    WorkOrderId = w.Id,
                    Issue = string.IsNullOrWhiteSpace(w.Issue) ? $"Work Order #{w.Id}" : w.Issue,
                    DepartmentName = w.Department?.Name ?? "Department",
                    PropertyName = propertyLabel,
                    Status = string.IsNullOrWhiteSpace(w.Status) ? "New" : w.Status,
                    DueDate = w.DueDate,
                    HasDueDate = w.DueDate != default,
                    Location = string.IsNullOrWhiteSpace(w.Location) ? null : w.Location
                };
            }).ToList();
        }

        var personalToDos = await _context.UserToDoItems
            .Where(t => t.UserId == user.Id)
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.CreatedAtUtc)
            .AsNoTracking()
            .Select(t => new UserToDoItemViewModel
            {
                Id = t.Id,
                Title = t.Title,
                IsCompleted = t.IsCompleted,
                CreatedAtUtc = t.CreatedAtUtc,
                CompletedAtUtc = t.CompletedAtUtc
            })
            .ToListAsync();

        model.ActivePersonalToDos = personalToDos
            .Where(t => !t.IsCompleted)
            .Take(25)
            .ToList();

        model.CompletedPersonalToDos = personalToDos
            .Where(t => t.IsCompleted)
            .Take(25)
            .ToList();

        return model;
    }

    private void InitializeViewBagDefaults()
    {
        InitializeDirectMessageDefaults();
        ViewBag.ToDoSidebarData = new ToDoSidebarViewModel();
    }

    private void InitializeDirectMessageDefaults()
    {
        ViewBag.UnreadDirectMessageCount = 0;
        ViewBag.UnreadAlertCount = 0;
        ViewBag.UnreadMessageCenterCount = 0;
        ViewBag.LatestDirectMessageParticipant = null;
        ViewBag.LatestDirectMessageBody = null;
        ViewBag.LatestDirectMessageSentAt = null;
        ViewBag.LatestDirectMessageConversationId = null;
    }

    private static bool ShouldLoadSidebarChrome(HttpContext? httpContext)
    {
        if (httpContext == null)
        {
            return false;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
            requestedWith.Any(value => value.Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var accept = httpContext.Request.Headers["Accept"];
        if (!StringValues.IsNullOrEmpty(accept))
        {
            var acceptsHtml = accept.Any(value =>
                value != null &&
                value.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!acceptsHtml)
            {
                return false;
            }
        }

        return true;
    }

    private void LogLayoutError(HttpContext? httpContext, Exception exception, string message, params object[] args)
    {
        if (httpContext == null)
        {
            return;
        }

        var logger = httpContext.RequestServices.GetService<ILogger<BaseController>>();
        logger?.LogError(exception, message, args);
    }

    protected async Task<MessagingAccessContext> GetMessagingAccessContextAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains("Admin");

        var propertyIds = (await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .ToListAsync())
            .ToHashSet();

        var allowedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (propertyIds.Count > 0)
        {
            var propertyUserIds = await _context.UserPropertyAccesses
                .Where(upa => propertyIds.Contains(upa.PropertyId) && upa.ApplicationUserId != user.Id)
                .Select(upa => upa.ApplicationUserId)
                .Distinct()
                .ToListAsync();

            foreach (var id in propertyUserIds)
            {
                allowedUserIds.Add(id);
            }
        }

        var adminRoleId = await _context.Roles
            .Where(r => r.NormalizedName == "ADMIN")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        if (adminRoleId != null)
        {
            var adminUserIds = await _context.UserRoles
                .Where(ur => ur.RoleId == adminRoleId && ur.UserId != user.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();

            foreach (var id in adminUserIds)
            {
                allowedUserIds.Add(id);
            }
        }

        return new MessagingAccessContext(isAdmin, propertyIds, allowedUserIds);
    }

    protected async Task<MessageCenterCounts> GetMessageCenterCountsAsync(ApplicationUser user, MessagingAccessContext? accessContext = null)
    {
        var access = accessContext ?? await GetMessagingAccessContextAsync(user);
        var allowedUserIds = access.AllowedUserIds.ToList();
        var restrictToAllowedUsers = !access.IsAdmin;

        var unreadQuery = _context.DirectMessages
            .Where(m => m.RecipientId == user.Id && !m.IsRead);

        if (restrictToAllowedUsers)
        {
            unreadQuery = unreadQuery.Where(m => allowedUserIds.Contains(m.SenderId));
        }

        var unreadMessages = await unreadQuery.CountAsync();
        var unreadAlerts = await _context.UserNotifications
            .CountAsync(n => n.UserId == user.Id && !n.IsRead && n.Type != "message");

        return new MessageCenterCounts(unreadMessages, unreadAlerts);
    }

    protected sealed record MessagingAccessContext(bool IsAdmin, HashSet<int> PropertyIds, HashSet<string> AllowedUserIds);
    protected sealed record MessageCenterCounts(int UnreadConversations, int UnreadAlerts);
}


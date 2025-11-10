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
        ViewBag.UnreadDirectMessageCount = 0;
        ViewBag.LatestDirectMessageParticipant = null;
        ViewBag.LatestDirectMessageBody = null;
        ViewBag.LatestDirectMessageSentAt = null;
        ViewBag.LatestDirectMessageConversationId = null;

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

            int? currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            Property? currentProperty = currentPropertyId.HasValue
                ? userProperties.FirstOrDefault(p => p.Id == currentPropertyId.Value)
                : null;

            if (currentProperty == null && user.DefaultPropertyId.HasValue)
            {
                currentProperty = userProperties.FirstOrDefault(p => p.Id == user.DefaultPropertyId.Value);
                if (currentProperty != null)
                {
                    HttpContext.Session.SetInt32("CurrentPropertyId", currentProperty.Id);
                }
            }

            if (currentProperty == null && userProperties.Any())
            {
                currentProperty = userProperties.First();
                HttpContext.Session.SetInt32("CurrentPropertyId", currentProperty.Id);
            }
            else if (currentProperty == null)
            {
                HttpContext.Session.Remove("CurrentPropertyId");
            }

            ViewBag.CurrentProperty = currentProperty;

            var normalizedTimeZoneId = DefaultTimeZoneProvider.NormalizeForStorage(user.TimeZoneId);
            HttpContext.Items["UserTimeZoneId"] = normalizedTimeZoneId;
            HttpContext.Session.SetString("UserTimeZoneId", normalizedTimeZoneId);

            var unreadMessageCount = await _context.DirectMessages
                .Where(m => m.RecipientId == user.Id && !m.IsRead)
                .CountAsync();
            ViewBag.UnreadDirectMessageCount = unreadMessageCount;

            var latestMessage = await _context.DirectMessages
                .Where(m => m.RecipientId == user.Id || m.SenderId == user.Id)
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

            if (latestMessage != null)
            {
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

            ViewBag.ToDoSidebarData = await BuildToDoSidebarAsync(user, userProperties);
        }

        await next();
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

        model.PersonalToDos = await _context.UserToDoItems
            .Where(t => t.UserId == user.Id)
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Take(25)
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

        return model;
    }
}


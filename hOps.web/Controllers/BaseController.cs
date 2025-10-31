using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
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

            ViewBag.UserProperties = props;
            ViewBag.CurrentUserId = user.Id;

            int? currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            Property? currentProperty = currentPropertyId.HasValue
                ? props.FirstOrDefault(p => p.Id == currentPropertyId.Value)
                : null;

            if (currentProperty == null && user.DefaultPropertyId.HasValue)
            {
                currentProperty = props.FirstOrDefault(p => p.Id == user.DefaultPropertyId.Value);
                if (currentProperty != null)
                {
                    HttpContext.Session.SetInt32("CurrentPropertyId", currentProperty.Id);
                }
            }

            if (currentProperty == null && props.Any())
            {
                currentProperty = props.First();
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
        }

        await next();
    }
}


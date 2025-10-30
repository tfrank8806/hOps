using hOps.web.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    public class DirectMessagesController : BaseController
    {
        private readonly DirectMessageService _messageService;
        private readonly IUserTimeZoneService _timeZoneService;

        public DirectMessagesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            DirectMessageService messageService,
            IUserTimeZoneService timeZoneService)
            : base(context, userManager)
        {
            _messageService = messageService;
            _timeZoneService = timeZoneService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? conversationId, string? userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (!conversationId.HasValue && !string.IsNullOrWhiteSpace(userId) && !string.Equals(userId, currentUser.Id, StringComparison.OrdinalIgnoreCase))
            {
                var conversation = await _messageService.GetOrCreateConversationAsync(currentUser.Id, userId);
                conversationId = conversation.Id;
            }

            var summaries = await _messageService.GetConversationSummariesAsync(currentUser.Id);
            var otherUserIds = summaries.Select(s => s.OtherUserId).Distinct().ToList();

            var otherUsers = await _userManager.Users
                .Where(u => otherUserIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    Name = BuildDisplayName(u)
                })
                .ToListAsync();

            var conversationItems = summaries
                .Select(summary =>
                {
                    var other = otherUsers.FirstOrDefault(u => u.Id == summary.OtherUserId);
                    return new ConversationListItemViewModel
                    {
                        ConversationId = summary.ConversationId,
                        ParticipantId = summary.OtherUserId,
                        ParticipantName = other?.Name ?? "Unknown user",
                        LastMessagePreview = string.IsNullOrWhiteSpace(summary.LastMessagePreview)
                            ? null
                            : MentionMarkupFormatter.ToDisplayText(summary.LastMessagePreview),
                        LastMessageAt = summary.LastMessageAt,
                        HasUnread = summary.UnreadCount > 0,
                        UnreadCount = summary.UnreadCount
                    };
                })
                .ToList();

            ConversationDetailViewModel? activeConversation = null;
            if (conversationId.HasValue)
            {
                var detail = await _messageService.GetConversationDetailAsync(conversationId.Value, currentUser.Id);
                if (detail != null)
                {
                    activeConversation = new ConversationDetailViewModel
                    {
                        ConversationId = detail.ConversationId,
                        ParticipantId = detail.OtherUserId,
                        ParticipantName = detail.OtherUserName,
                        Messages = detail.Messages
                            .Select(m => new DirectMessageBubbleViewModel
                            {
                                MessageId = m.MessageId,
                                SenderId = m.SenderId,
                                SenderName = currentUser.Id == m.SenderId
                                    ? "You"
                                    : conversationItems.FirstOrDefault(c => c.ParticipantId == m.SenderId)?.ParticipantName ?? "User",
                                Body = m.Body,
                                SentAt = _timeZoneService.ConvertToUserTime(m.SentAt),
                                IsOwnMessage = m.IsOwnMessage,
                                IsRead = m.IsRead,
                                ReadAt = m.ReadAt.HasValue ? _timeZoneService.ConvertToUserTime(m.ReadAt.Value) : (DateTime?)null
                            })
                            .ToList()
                    };
                }
            }

            var recipientCandidates = await _userManager.Users
                .Where(u => u.Id != currentUser.Id)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();

            var availableRecipients = recipientCandidates
                .Select(u => new MessageRecipientOptionViewModel
                {
                    UserId = u.Id,
                    DisplayName = BuildDisplayName(u),
                    IsActive = conversationItems.Any(c => c.ParticipantId == u.Id)
                })
                .ToList();

            var viewModel = new DirectMessagePageViewModel
            {
                Conversations = conversationItems,
                ActiveConversation = activeConversation,
                Form = new DirectMessageForm
                {
                    ConversationId = activeConversation?.ConversationId ?? 0
                },
                CurrentUserId = currentUser.Id,
                AvailableRecipients = availableRecipients
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(DirectMessageForm form)
        {
            if (!ModelState.IsValid)
            {
                TempData["DirectMessageError"] = "Please enter a message before sending.";
                return RedirectToAction(nameof(Index), new { conversationId = form.ConversationId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var conversation = await _context.DirectMessageConversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == form.ConversationId && (c.ParticipantAId == currentUser.Id || c.ParticipantBId == currentUser.Id));

            if (conversation == null)
            {
                TempData["DirectMessageError"] = "Conversation not found or you do not have access.";
                return RedirectToAction(nameof(Index));
            }

            var recipientId = conversation.ParticipantAId == currentUser.Id
                ? conversation.ParticipantBId
                : conversation.ParticipantAId;

            await _messageService.SendMessageAsync(form.ConversationId, currentUser.Id, recipientId, form.Body);
            TempData["DirectMessageMessage"] = "Message sent.";
            return RedirectToAction(nameof(Index), new { conversationId = form.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(NewConversationForm form)
        {
            if (!ModelState.IsValid)
            {
                TempData["DirectMessageError"] = "Select a recipient and enter a message.";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            try
            {
                var message = await _messageService.StartConversationAsync(currentUser.Id, form.RecipientUserId, form.Body);
                TempData["DirectMessageMessage"] = "Conversation started.";
                return RedirectToAction(nameof(Index), new { conversationId = message.ConversationId });
            }
            catch (InvalidOperationException ex)
            {
                TempData["DirectMessageError"] = ex.Message;
                return RedirectToAction(nameof(Index));
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

            return string.IsNullOrWhiteSpace(user.Email)
                ? user.UserName ?? "User"
                : user.Email!;
        }
    }
}


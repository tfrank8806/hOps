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
        public async Task<IActionResult> Index(int? conversationId, string? userId, bool startNew = false)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var viewModel = await BuildViewModelAsync(currentUser, conversationId, userId, startNew);
            await PopulateDirectMessageBadgeAsync(currentUser);
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

            var access = await GetMessagingAccessContextAsync(currentUser);

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

            if (!access.IsAdmin && !access.AllowedUserIds.Contains(recipientId))
            {
                TempData["DirectMessageError"] = "You can only message teammates assigned to your properties.";
                return RedirectToAction(nameof(Index));
            }

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

            var access = await GetMessagingAccessContextAsync(currentUser);
            if (!access.IsAdmin && !access.AllowedUserIds.Contains(form.RecipientUserId))
            {
                TempData["DirectMessageError"] = "You can only message teammates assigned to your properties.";
                return RedirectToAction(nameof(Index));
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int conversationId)
        {
            if (conversationId <= 0)
            {
                TempData["DirectMessageError"] = "Invalid conversation.";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var conversation = await _context.DirectMessageConversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c =>
                    c.Id == conversationId &&
                    (c.ParticipantAId == currentUser.Id || c.ParticipantBId == currentUser.Id));

            if (conversation == null)
            {
                TempData["DirectMessageError"] = "Conversation not found or you don't have access.";
                return RedirectToAction(nameof(Index));
            }

            _context.DirectMessageConversations.Remove(conversation);
            await _context.SaveChangesAsync();

            TempData["DirectMessageMessage"] = "Conversation deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<DirectMessagePageViewModel> BuildViewModelAsync(ApplicationUser currentUser, int? conversationId, string? userId, bool startNew)
        {
            var access = await GetMessagingAccessContextAsync(currentUser);

            var resolvedConversationId = conversationId;
            if (!resolvedConversationId.HasValue && !string.IsNullOrWhiteSpace(userId) && !string.Equals(userId, currentUser.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (access.IsAdmin || access.AllowedUserIds.Contains(userId))
                {
                    var conversation = await _messageService.GetOrCreateConversationAsync(currentUser.Id, userId);
                    resolvedConversationId = conversation.Id;
                }
            }

            var summaries = await _messageService.GetConversationSummariesAsync(currentUser.Id);
            var allowedRecipientIds = access.IsAdmin
                ? null
                : access.AllowedUserIds.ToList();

            var filteredSummaries = access.IsAdmin
                ? summaries
                : summaries.Where(summary => allowedRecipientIds!.Contains(summary.OtherUserId)).ToList();

            if (!resolvedConversationId.HasValue)
            {
                var fallbackSummary = filteredSummaries
                    .OrderByDescending(s => s.LastMessageAt ?? DateTime.MinValue)
                    .FirstOrDefault();

                if (fallbackSummary == null && access.IsAdmin)
                {
                    fallbackSummary = summaries
                        .OrderByDescending(s => s.LastMessageAt ?? DateTime.MinValue)
                        .FirstOrDefault();
                }

                if (fallbackSummary != null)
                {
                    resolvedConversationId = fallbackSummary.ConversationId;
                }
            }

            var participantIds = filteredSummaries.Select(s => s.OtherUserId).Distinct().ToList();
            var participantInfo = await _userManager.Users
                .Where(u => participantIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    Name = BuildDisplayName(u),
                    u.Email
                })
                .ToListAsync();
            var nameLookup = participantInfo.ToDictionary(u => u.Id, u => u.Name);
            var emailLookup = participantInfo.ToDictionary(u => u.Id, u => u.Email);

            var conversationItems = filteredSummaries
                .Select(summary =>
                {
                    var participantName = nameLookup.TryGetValue(summary.OtherUserId, out var value)
                        ? value
                        : null;

                    if (string.IsNullOrWhiteSpace(participantName))
                    {
                        participantName = emailLookup.TryGetValue(summary.OtherUserId, out var fallbackEmail) && !string.IsNullOrWhiteSpace(fallbackEmail)
                            ? fallbackEmail!
                            : "Teammate";
                    }

                    return new ConversationListItemViewModel
                    {
                        ConversationId = summary.ConversationId,
                        ParticipantId = summary.OtherUserId,
                        ParticipantName = participantName,
                        ParticipantEmail = emailLookup.TryGetValue(summary.OtherUserId, out var participantEmail)
                            ? participantEmail
                            : null,
                        LastMessagePreview = string.IsNullOrWhiteSpace(summary.LastMessagePreview)
                            ? null
                            : MentionMarkupFormatter.ToDisplayText(summary.LastMessagePreview),
                        LastMessageAt = summary.LastMessageAt,
                        HasUnread = summary.UnreadCount > 0,
                        UnreadCount = summary.UnreadCount
                    };
                })
                .ToList();

            var conversationLookup = conversationItems
                .GroupBy(c => c.ParticipantId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().ConversationId, StringComparer.OrdinalIgnoreCase);

            ConversationDetailViewModel? activeConversation = null;
            if (resolvedConversationId.HasValue)
            {
                var detail = await _messageService.GetConversationDetailAsync(resolvedConversationId.Value, currentUser.Id);
                if (detail != null && (access.IsAdmin || access.AllowedUserIds.Contains(detail.OtherUserId)))
                {
                    activeConversation = new ConversationDetailViewModel
                    {
                        ConversationId = detail.ConversationId,
                        ParticipantId = detail.OtherUserId,
                        ParticipantName = detail.OtherUserName,
                        ParticipantEmail = detail.OtherUserEmail,
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
            else if (!filteredSummaries.Any() && summaries.Any())
            {
                var latest = summaries
                    .OrderByDescending(s => s.LastMessageAt ?? DateTime.MinValue)
                    .FirstOrDefault();
                if (latest != null && (access.IsAdmin || access.AllowedUserIds.Contains(latest.OtherUserId)))
                {
                    var detail = await _messageService.GetConversationDetailAsync(latest.ConversationId, currentUser.Id);
                    if (detail != null)
                    {
                        activeConversation = new ConversationDetailViewModel
                        {
                            ConversationId = detail.ConversationId,
                            ParticipantId = detail.OtherUserId,
                            ParticipantName = detail.OtherUserName,
                            ParticipantEmail = detail.OtherUserEmail,
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
            }

            var recipientQuery = _userManager.Users.Where(u => u.Id != currentUser.Id);
            if (!access.IsAdmin)
            {
                if (allowedRecipientIds == null || allowedRecipientIds.Count == 0)
                {
                    recipientQuery = recipientQuery.Where(u => false);
                }
                else
                {
                    recipientQuery = recipientQuery.Where(u => allowedRecipientIds.Contains(u.Id));
                }
            }

            var recipientCandidates = await recipientQuery
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new
                {
                    u.Id,
                    Name = BuildDisplayName(u)
                })
                .ToListAsync();

            var activeParticipantIds = conversationItems
                .Select(c => c.ParticipantId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var availableRecipients = recipientCandidates
                .Select(u => new MessageRecipientOptionViewModel
                {
                    UserId = u.Id,
                    DisplayName = u.Name,
                    IsActive = activeParticipantIds.Contains(u.Id),
                    ConversationId = conversationLookup.TryGetValue(u.Id, out var conversationId)
                        ? conversationId
                        : (int?)null
                })
                .ToList();

            var showNewConversation = startNew || (!conversationItems.Any() && availableRecipients.Any());
            var newConversation = new NewConversationForm
            {
                RecipientUserId = availableRecipients.Any(option =>
                        string.Equals(option.UserId, userId, StringComparison.OrdinalIgnoreCase))
                    ? userId!
                    : string.Empty
            };

            return new DirectMessagePageViewModel
            {
                Conversations = conversationItems,
                ActiveConversation = activeConversation,
                Form = new DirectMessageForm
                {
                    ConversationId = activeConversation?.ConversationId ?? 0
                },
                CurrentUserId = currentUser.Id,
                AvailableRecipients = availableRecipients,
                NewConversation = newConversation,
                ShowNewConversation = showNewConversation
            };
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


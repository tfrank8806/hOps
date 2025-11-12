using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace hOps.web.Services
{
    public class DirectMessageService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IRealtimeNotificationService _realtimeNotifications;
        private readonly ILogger<DirectMessageService> _logger;

        public DirectMessageService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<DirectMessageService> logger,
            IRealtimeNotificationService realtimeNotifications)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
            _realtimeNotifications = realtimeNotifications;
        }

        public async Task<DirectMessageConversation> GetOrCreateConversationAsync(string userId, string otherUserId)
        {
            var (participantA, participantB) = NormalizeParticipants(userId, otherUserId);

            var conversation = await _context.DirectMessageConversations
                .FirstOrDefaultAsync(c => c.ParticipantAId == participantA && c.ParticipantBId == participantB);

            if (conversation != null)
            {
                return conversation;
            }

            conversation = new DirectMessageConversation
            {
                ParticipantAId = participantA,
                ParticipantBId = participantB,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.DirectMessageConversations.Add(conversation);
            await _context.SaveChangesAsync();
            return conversation;
        }

        public async Task<DirectMessage> SendMessageAsync(int conversationId, string senderId, string recipientId, string body)
        {
            var conversation = await _context.DirectMessageConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.ParticipantAId == senderId || c.ParticipantBId == senderId));

            if (conversation == null)
            {
                throw new InvalidOperationException("Conversation not found.");
            }

            if (conversation.ParticipantAId != recipientId && conversation.ParticipantBId != recipientId)
            {
                throw new InvalidOperationException("Recipient does not belong to this conversation.");
            }

            var message = new DirectMessage
            {
                ConversationId = conversationId,
                SenderId = senderId,
                RecipientId = recipientId,
                Body = body.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.DirectMessages.Add(message);
            conversation.UpdatedAt = message.SentAt;

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = recipientId,
                Type = "message",
                Title = "New message",
                Content = body.Length > 140 ? $"{body.Substring(0, 140)}…" : body,
                LinkUrl = $"/DirectMessages?conversationId={conversationId}",
                CreatedAt = message.SentAt,
                DirectMessage = message,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await SendRealtimeNotificationAsync(message, senderId, recipientId);
            await SendMessageEmailAsync(message, senderId, recipientId);
            return message;
        }

        public async Task<DirectMessage> StartConversationAsync(string senderId, string recipientId, string body)
        {
            if (senderId == recipientId)
            {
                throw new InvalidOperationException("Cannot start a conversation with yourself.");
            }

            var conversation = await GetOrCreateConversationAsync(senderId, recipientId);
            return await SendMessageAsync(conversation.Id, senderId, recipientId, body);
        }

        public async Task<List<ConversationSummary>> GetConversationSummariesAsync(string userId)
        {
            return await _context.DirectMessageConversations
                .Where(c => c.ParticipantAId == userId || c.ParticipantBId == userId)
                .Select(c => new ConversationSummary
                {
                    ConversationId = c.Id,
                    OtherUserId = c.ParticipantAId == userId ? c.ParticipantBId : c.ParticipantAId,
                    LastMessageAt = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => (DateTime?)m.SentAt)
                        .FirstOrDefault(),
                    LastMessagePreview = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.Body)
                        .FirstOrDefault(),
                    UnreadCount = c.Messages.Count(m => m.RecipientId == userId && !m.IsRead)
                })
                .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<ConversationDetail?> GetConversationDetailAsync(int conversationId, string userId)
        {
            var conversation = await _context.DirectMessageConversations
                .Include(c => c.Messages.OrderBy(m => m.SentAt))
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.ParticipantAId == userId || c.ParticipantBId == userId));

            if (conversation == null)
            {
                return null;
            }

            var otherUserId = conversation.ParticipantAId == userId ? conversation.ParticipantBId : conversation.ParticipantAId;
            var otherUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == otherUserId);

            var detail = new ConversationDetail
            {
                ConversationId = conversation.Id,
                OtherUserId = otherUserId ?? string.Empty,
                OtherUserName = otherUser != null ? BuildDisplayName(otherUser) : "Unknown user",
                OtherUserEmail = otherUser?.Email,
                Messages = conversation.Messages
                    .Select(m => new MessageDetail
                    {
                        MessageId = m.Id,
                        SenderId = m.SenderId,
                        Body = m.Body,
                        SentAt = m.SentAt,
                        IsOwnMessage = m.SenderId == userId,
                        IsRead = m.IsRead,
                        ReadAt = m.ReadAt
                    })
                    .ToList()
            };

            var unreadMessages = conversation.Messages
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .ToList();

            if (unreadMessages.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.ReadAt = now;
                }

                var notifications = await _context.UserNotifications
                    .Where(n => n.DirectMessageId != null
                                && unreadMessages.Select(m => m.Id).Contains(n.DirectMessageId.Value)
                                && n.UserId == userId
                                && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = now;
                }

                await _context.SaveChangesAsync();
            }

            return detail;
        }

        public async Task<List<UserNotification>> GetRecentNotificationsAsync(string userId, int take = 10)
        {
            return await _context.UserNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            return await _context.UserNotifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkNotificationAsReadAsync(int notificationId, string userId)
        {
            var notification = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return;
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllNotificationsReadAsync(string userId)
        {
            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (notifications.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
            }

            await _context.SaveChangesAsync();
        }

        private async Task SendRealtimeNotificationAsync(DirectMessage message, string senderId, string recipientId)
        {
            var sender = await _userManager.FindByIdAsync(senderId);
            var senderName = sender != null ? BuildDisplayName(sender) : "Teammate";

            var preview = message.Body;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 160)
            {
                preview = $"{preview[..160]}…";
            }

            var payload = new RealtimeNotificationPayload(
                "New message",
                $"{senderName}: {preview}",
                $"/DirectMessages?conversationId={message.ConversationId}",
                "message");

            await _realtimeNotifications.NotifyUserAsync(recipientId, payload);
        }

        private async Task SendMessageEmailAsync(DirectMessage message, string senderId, string recipientId)
        {
            var recipient = await _userManager.FindByIdAsync(recipientId);
            if (recipient == null || !recipient.EmailOnMessage || string.IsNullOrWhiteSpace(recipient.Email))
            {
                return;
            }

            var sender = await _userManager.FindByIdAsync(senderId);
            var senderName = sender != null ? BuildDisplayName(sender) : "A colleague";
            var preview = message.Body.Length > 200 ? $"{message.Body[..200]}…" : message.Body;
            var safePreview = WebUtility.HtmlEncode(preview);
            var safeSender = WebUtility.HtmlEncode(senderName);
            var link = $"/DirectMessages?conversationId={message.ConversationId}";
            var htmlBody = $"""
                <p>{safeSender} sent you a new message.</p>
                <p style="margin:0 0 1rem 0;"><em>{safePreview}</em></p>
                <p><a href="{link}">Open the conversation</a></p>
                """;

            try
            {
                await _emailSender.SendEmailAsync(recipient.Email!, "New message in HotelOps", htmlBody);
            }
            catch (Exception ex)
            {
                // Sanitize recipientId to prevent log forging
                var sanitizedRecipientId = recipientId.Replace("\n", "").Replace("\r", "");
                _logger.LogError(ex, "Unable to send direct message email notification to user {UserId}", sanitizedRecipientId);
            }
        }

        private static (string First, string Second) NormalizeParticipants(string first, string second)
        {
            return string.CompareOrdinal(first, second) <= 0
                ? (first, second)
                : (second, first);
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

            return string.IsNullOrWhiteSpace(user.Email) ? user.UserName ?? "Unknown user" : user.Email!;
        }
    }

    public class ConversationSummary
    {
        public int ConversationId { get; set; }
        public string OtherUserId { get; set; } = string.Empty;
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }

    public class ConversationDetail
    {
        public int ConversationId { get; set; }
        public string OtherUserId { get; set; } = string.Empty;
        public string OtherUserName { get; set; } = string.Empty;
        public string? OtherUserEmail { get; set; }
        public List<MessageDetail> Messages { get; set; } = new();
    }

    public class MessageDetail
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsOwnMessage { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}

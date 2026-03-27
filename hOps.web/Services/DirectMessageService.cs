using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using hOps.web.Utilities;

namespace hOps.web.Services
{
    public class DirectMessageService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IRealtimeNotificationService _realtimeNotifications;
        private readonly ILogger<DirectMessageService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public DirectMessageService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<DirectMessageService> logger,
            IRealtimeNotificationService realtimeNotifications,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
            _realtimeNotifications = realtimeNotifications;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
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
            conversation.ParticipantAArchived = false;
            conversation.ParticipantBArchived = false;

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = recipientId,
                Type = "message",
                Title = "New message",
                Content = body.Length > 140 ? $"{body.Substring(0, 140)}..." : body,
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
                .Where(c =>
                    (c.ParticipantAId == userId && !c.ParticipantAArchived) ||
                    (c.ParticipantBId == userId && !c.ParticipantBArchived))
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

            var unarchived = false;
            if (conversation.ParticipantAId == userId && conversation.ParticipantAArchived)
            {
                conversation.ParticipantAArchived = false;
                unarchived = true;
            }

            if (conversation.ParticipantBId == userId && conversation.ParticipantBArchived)
            {
                conversation.ParticipantBArchived = false;
                unarchived = true;
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

            var hadUnreadChanges = unreadMessages.Count > 0;
            if (hadUnreadChanges)
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

            }

            if (hadUnreadChanges || unarchived)
            {
                await _context.SaveChangesAsync();
            }

            return detail;
        }

        public async Task<List<UserNotification>> GetRecentAlertsAsync(string userId, int take = 10)
        {
            return await _context.UserNotifications
                .Where(n => n.UserId == userId && n.Type != "message")
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> GetUnreadAlertCountAsync(string userId)
        {
            return await _context.UserNotifications
                .CountAsync(n => n.UserId == userId && !n.IsRead && n.Type != "message");
        }

        public async Task<UserNotification?> GetAlertAsync(int notificationId, string userId)
        {
            return await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId && n.Type != "message");
        }

        public async Task DeleteAlertAsync(int notificationId, string userId)
        {
            var alert = await GetAlertAsync(notificationId, userId);
            if (alert == null)
            {
                return;
            }

            _context.UserNotifications.Remove(alert);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllAlertsAsync(string userId)
        {
            var alerts = await _context.UserNotifications
                .Where(n => n.UserId == userId && n.Type != "message")
                .ToListAsync();

            if (alerts.Count == 0)
            {
                return;
            }

            _context.UserNotifications.RemoveRange(alerts);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAlertsByCategoryAsync(string userId, string categoryKey)
        {
            var normalizedKey = AlertCategoryHelper.NormalizeKey(categoryKey);
            var alerts = await BuildAlertCategoryQuery(userId, normalizedKey)
                .ToListAsync();

            if (alerts.Count == 0)
            {
                return;
            }

            _context.UserNotifications.RemoveRange(alerts);
            await _context.SaveChangesAsync();
        }

        public async Task ArchiveConversationForUserAsync(int conversationId, string userId)
        {
            var conversation = await _context.DirectMessageConversations
                .FirstOrDefaultAsync(c =>
                    c.Id == conversationId &&
                    (c.ParticipantAId == userId || c.ParticipantBId == userId));

            if (conversation == null)
            {
                return;
            }

            if (conversation.ParticipantAId == userId)
            {
                conversation.ParticipantAArchived = true;
            }
            else
            {
                conversation.ParticipantBArchived = true;
            }

            conversation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task MarkNotificationAsReadAsync(int notificationId, string userId)
        {
            var notification = await GetAlertAsync(notificationId, userId);
            if (notification == null)
            {
                return;
            }

            _context.UserNotifications.Remove(notification);
            await _context.SaveChangesAsync();
        }

        public async Task MarkAllAlertsReadAsync(string userId)
        {
            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == userId && !n.IsRead && n.Type != "message")
                .ToListAsync();

            if (notifications.Count == 0)
            {
                return;
            }

            _context.UserNotifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();
        }

        private IQueryable<UserNotification> BuildAlertCategoryQuery(string userId, string normalizedCategoryKey)
        {
            var query = _context.UserNotifications
                .Where(n => n.UserId == userId && n.Type != "message");

            if (normalizedCategoryKey.Equals(AlertCategoryHelper.OtherKey, StringComparison.OrdinalIgnoreCase))
            {
                var knownKeys = AlertCategoryHelper.KnownNonOtherKeys;
                if (knownKeys.Count > 0)
                {
                    query = query.Where(n =>
                        n.Type == null ||
                        !knownKeys.Contains(n.Type!.ToLower()));
                }
                else
                {
                    query = query.Where(n => n.Type == null);
                }
            }
            else
            {
                query = query.Where(n => n.Type != null && n.Type.ToLower() == normalizedCategoryKey);
            }

            return query;
        }

        public async Task MarkAlertsReadByCategoryAsync(string userId, string categoryKey)
        {
            var normalizedKey = AlertCategoryHelper.NormalizeKey(categoryKey);
            var notifications = await BuildAlertCategoryQuery(userId, normalizedKey)
                .Where(n => !n.IsRead)
                .ToListAsync();

            if (notifications.Count == 0)
            {
                return;
            }

            _context.UserNotifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();
        }

        private async Task SendRealtimeNotificationAsync(DirectMessage message, string senderId, string recipientId)
        {
            var sender = await _userManager.FindByIdAsync(senderId);
            var senderName = sender != null ? BuildDisplayName(sender) : "Teammate";

            var preview = message.Body;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 160)
            {
                preview = $"{preview[..160]}...";
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
            var preview = message.Body.Length > 200 ? $"{message.Body[..200]}..." : message.Body;
            var safePreview = WebUtility.HtmlEncode(preview);
            var safeSender = WebUtility.HtmlEncode(senderName);
            var link = BuildConversationUrl(message.ConversationId);
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

        private string BuildConversationUrl(int conversationId)
        {
            var relative = $"/DirectMessages?conversationId={conversationId}";
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request != null)
            {
                return $"{request.Scheme}://{request.Host}{relative}";
            }

            var baseUrl = _configuration["App:BaseUrl"] ?? _configuration["AppBaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var trimmed = baseUrl!.TrimEnd('/');
                return $"{trimmed}{relative}";
            }

            return relative;
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

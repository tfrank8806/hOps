using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Messages
{
    public class ConversationListItemViewModel
    {
        public int ConversationId { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantAvatarUrl { get; set; }
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public bool HasUnread { get; set; }
        public int UnreadCount { get; set; }
    }

    public class DirectMessageBubbleViewModel
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsOwnMessage { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class ConversationDetailViewModel
    {
        public int ConversationId { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantAvatarUrl { get; set; }
        public List<DirectMessageBubbleViewModel> Messages { get; set; } = new();
    }

    public class DirectMessagePageViewModel
    {
        public List<ConversationListItemViewModel> Conversations { get; set; } = new();
        public ConversationDetailViewModel? ActiveConversation { get; set; }
        public DirectMessageForm Form { get; set; } = new();
        public string? CurrentUserId { get; set; }
        public NewConversationForm NewConversation { get; set; } = new();
        public List<MessageRecipientOptionViewModel> AvailableRecipients { get; set; } = new();
    }

    public class DirectMessageForm
    {
        [Required]
        public int ConversationId { get; set; }

        [Required]
        [StringLength(2000)]
        public string Body { get; set; } = string.Empty;
    }

    public class MessageRecipientOptionViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? ConversationId { get; set; }
    }

    public class NewConversationForm
    {
        [Required]
        public string RecipientUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Body { get; set; } = string.Empty;
    }

    public class NotificationListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? LinkUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}

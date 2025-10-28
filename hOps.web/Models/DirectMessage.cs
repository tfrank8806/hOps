using System;

namespace hOps.web.Models
{
    public class DirectMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public DirectMessageConversation? Conversation { get; set; }

        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser? Sender { get; set; }

        public string RecipientId { get; set; } = string.Empty;
        public ApplicationUser? Recipient { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}

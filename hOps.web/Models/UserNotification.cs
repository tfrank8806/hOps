using System;

namespace hOps.web.Models
{
    public class UserNotification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Type { get; set; } = "message";
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? LinkUrl { get; set; }

        public int? DirectMessageId { get; set; }
        public DirectMessage? DirectMessage { get; set; }

        public int? PassOnLogId { get; set; }
        public PassOnLog? PassOnLog { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }
}

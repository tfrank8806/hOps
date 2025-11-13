using System;
using System.Collections.Generic;

namespace hOps.web.Models
{
    public class DirectMessageConversation
    {
        public int Id { get; set; }

        public string ParticipantAId { get; set; } = string.Empty;
        public ApplicationUser? ParticipantA { get; set; }

        public string ParticipantBId { get; set; } = string.Empty;
        public ApplicationUser? ParticipantB { get; set; }

        public bool ParticipantAArchived { get; set; }
        public bool ParticipantBArchived { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
    }
}

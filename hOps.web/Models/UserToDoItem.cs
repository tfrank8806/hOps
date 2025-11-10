using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class UserToDoItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAtUtc { get; set; }

        public int? WorkOrderId { get; set; }

        public WorkOrder? WorkOrder { get; set; }
    }
}

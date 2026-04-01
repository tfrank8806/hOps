#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class MaintenanceLogCycleCompletion
    {
        public int Id { get; set; }

        [Required]
        public int TemplateId { get; set; }
        public MaintenanceLogTemplate Template { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string CycleWindowKey { get; set; } = string.Empty;

        public MaintenanceLogScheduleType ScheduleType { get; set; } = MaintenanceLogScheduleType.None;

        public DateTime CycleStartLocal { get; set; }
        public DateTime CycleEndLocal { get; set; }
        public DateTime CycleDueLocal { get; set; }

        public MaintenanceLogCompletionResult Result { get; set; } = MaintenanceLogCompletionResult.Passed;

        public DateTime? CompletedAtUtc { get; set; }

        [MaxLength(450)]
        public string? CompletedByUserId { get; set; }
        public ApplicationUser? CompletedByUser { get; set; }

        public int? DurationMinutes { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<MaintenanceLogCompletionAttachment> Attachments { get; set; } = new List<MaintenanceLogCompletionAttachment>();
    }
}


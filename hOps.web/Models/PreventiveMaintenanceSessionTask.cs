#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PreventiveMaintenanceSessionTask
    {
        public int Id { get; set; }

        public int SessionId { get; set; }
        public PreventiveMaintenanceSession Session { get; set; } = null!;

        public int? TemplateTaskId { get; set; }
        public PreventiveMaintenanceTask? TemplateTask { get; set; }

        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? TaskDescription { get; set; }

        public int SortOrder { get; set; }

        public PreventiveMaintenanceTaskStatus Status { get; set; } = PreventiveMaintenanceTaskStatus.NotStarted;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public DateTime? CompletedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}


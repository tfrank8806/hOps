#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PreventiveMaintenanceSession
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        public int? RoomId { get; set; }
        public Room? Room { get; set; }

        [MaxLength(32)]
        public string RoomNumber { get; set; } = string.Empty;

        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? PausedAtUtc { get; set; }
        public DateTime? LastResumedAtUtc { get; set; }

        public double TotalDurationSeconds { get; set; }

        public PreventiveMaintenanceSessionStatus Status { get; set; } = PreventiveMaintenanceSessionStatus.Draft;

        public string CreatedById { get; set; } = string.Empty;
        public ApplicationUser? CreatedBy { get; set; }

        public string? CompletedById { get; set; }
        public ApplicationUser? CompletedBy { get; set; }

        public DateTime LastSavedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<PreventiveMaintenanceSessionTask> Tasks { get; set; } = new List<PreventiveMaintenanceSessionTask>();
    }
}


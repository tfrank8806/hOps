#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PreventiveMaintenanceChecklist
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        public PreventiveMaintenanceChecklistType ChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;

        public string? AreaOptionsJson { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        public string? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }

        public string? UpdatedById { get; set; }
        public ApplicationUser? UpdatedBy { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<PreventiveMaintenanceTask> Tasks { get; set; } = new List<PreventiveMaintenanceTask>();
        public ICollection<PreventiveMaintenanceSession> Sessions { get; set; } = new List<PreventiveMaintenanceSession>();
    }
}

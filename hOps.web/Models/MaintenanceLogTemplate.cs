#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class MaintenanceLogTemplate
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        public string ColumnsJson { get; set; } = "[]";

        public MaintenanceLogScheduleType ScheduleType { get; set; } = MaintenanceLogScheduleType.None;

        public int WeeklyDaysBitmask { get; set; }
        public int? DayOfMonth { get; set; }
        public TimeSpan? DueTimeLocal { get; set; }

        [MaxLength(260)]
        public string? ChecklistFilePath { get; set; }

        [MaxLength(160)]
        public string? ChecklistFileName { get; set; }

        public long? ChecklistFileSizeBytes { get; set; }

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<MaintenanceLogEntry> Entries { get; set; } = new List<MaintenanceLogEntry>();
        public ICollection<MaintenanceLogCycleCompletion> CycleCompletions { get; set; } = new List<MaintenanceLogCycleCompletion>();
    }
}

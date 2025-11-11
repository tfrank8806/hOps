#nullable enable

using System;
using System.Collections.Generic;

namespace hOps.web.Models
{
    public class Schedule
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;

        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }

        public string? Title { get; set; }
        public string? Notes { get; set; }

        public ScheduleStatus Status { get; set; } = ScheduleStatus.Draft;

        public string? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedById { get; set; }
        public ApplicationUser? UpdatedBy { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        public string? PostedById { get; set; }
        public ApplicationUser? PostedBy { get; set; }
        public DateTime? PostedAtUtc { get; set; }

        public ICollection<ScheduleAssignment> Assignments { get; set; } = new List<ScheduleAssignment>();
    }
}

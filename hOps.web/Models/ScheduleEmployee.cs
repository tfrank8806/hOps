#nullable enable

using System;
using System.Collections.Generic;

namespace hOps.web.Models
{
    public class ScheduleEmployee
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;

        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }

        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public bool IsActive { get; set; } = true;
        public bool EmailAlertsEnabled { get; set; } = true;

        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        public ICollection<ScheduleAssignment> Assignments { get; set; } = new List<ScheduleAssignment>();
        public ICollection<ScheduleTimeOffRequest> TimeOffRequests { get; set; } = new List<ScheduleTimeOffRequest>();
    }
}

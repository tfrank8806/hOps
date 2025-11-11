#nullable enable

using System;

namespace hOps.web.Models
{
    public class ScheduleTimeOffRequest
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;

        public int ScheduleEmployeeId { get; set; }
        public ScheduleEmployee Employee { get; set; } = default!;

        public string SubmittedByUserId { get; set; } = string.Empty;
        public ApplicationUser SubmittedByUser { get; set; } = default!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string Reason { get; set; } = string.Empty;

        public TimeOffRequestStatus Status { get; set; } = TimeOffRequestStatus.Pending;

        public string? DecisionByUserId { get; set; }
        public ApplicationUser? DecisionByUser { get; set; }

        public DateTime? DecisionAtUtc { get; set; }
        public string? DecisionNotes { get; set; }

        public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

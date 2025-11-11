#nullable enable

using System;

namespace hOps.web.Models
{
    public class ScheduleAssignment
    {
        public int Id { get; set; }

        public int ScheduleId { get; set; }
        public Schedule Schedule { get; set; } = default!;

        public int ScheduleEmployeeId { get; set; }
        public ScheduleEmployee Employee { get; set; } = default!;

        public DateTime ShiftDate { get; set; }

        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan? ShiftStartTime { get; set; }
        public TimeSpan? ShiftEndTime { get; set; }
        public string? Notes { get; set; }
        public string? ColorHex { get; set; }
    }
}

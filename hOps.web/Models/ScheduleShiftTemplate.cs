#nullable enable

using System;

namespace hOps.web.Models
{
    public class ScheduleShiftTemplate
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;

        public string Name { get; set; } = string.Empty;
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(9);
        public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(17);
        public int SortOrder { get; set; } = 0;
        public string ColorHex { get; set; } = "#3b82f6";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}

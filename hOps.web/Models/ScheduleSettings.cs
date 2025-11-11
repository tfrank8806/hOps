#nullable enable

using System;

namespace hOps.web.Models
{
    public class ScheduleSettings
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;

        public DayOfWeek StartDayOfWeek { get; set; } = DayOfWeek.Monday;

        public string? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedByUser { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

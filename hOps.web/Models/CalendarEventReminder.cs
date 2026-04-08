using System;

namespace hOps.web.Models
{
    public class CalendarEventReminder
    {
        public int Id { get; set; }

        public int CalendarEventId { get; set; }
        public CalendarEvent CalendarEvent { get; set; } = default!;

        public CalendarEventReminderOffset ReminderType { get; set; }

        public DateTime OccurrenceStartUtc { get; set; }

        public DateTime ScheduledSendUtc { get; set; }

        public bool IsSent { get; set; }

        public DateTime? SentAtUtc { get; set; }
    }

    public enum CalendarEventReminderOffset
    {
        DayOfEvent = 0,
        OneDayBefore = 1,
        TwoDaysBefore = 2,
        OneWeekBefore = 3
    }
}

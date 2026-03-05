using System;

namespace hOps.web.Models
{
    public class CalendarEventException
    {
        public int Id { get; set; }

        public int CalendarEventId { get; set; }

        public CalendarEvent CalendarEvent { get; set; } = default!;

        public DateTime OccurrenceDate { get; set; }

        public CalendarEventExceptionType Type { get; set; } = CalendarEventExceptionType.DeletedOccurrence;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum CalendarEventExceptionType
    {
        DeletedOccurrence = 0
    }
}

using System;

namespace hOps.web.Utilities
{
    public static class CalendarEventTimeHelper
    {
        public static DateTime CombineDateAndTime(DateTime dateUtc, TimeSpan? time)
        {
            var normalized = DateTime.SpecifyKind(dateUtc.Date, DateTimeKind.Utc);
            if (time.HasValue)
            {
                normalized = normalized.Add(time.Value);
            }

            return DateTime.SpecifyKind(normalized, DateTimeKind.Utc);
        }

        public static string BuildDateTimeLabel(DateTime dateUtc, TimeSpan? time)
        {
            var dateLabel = dateUtc.ToString("MMM d, yyyy");
            if (time.HasValue)
            {
                var timeLabel = DateTime.Today.Add(time.Value).ToString("t");
                return $"{dateLabel} at {timeLabel}";
            }

            return dateLabel;
        }
    }
}

using System;
using System.Collections.Generic;
using hOps.web.Models;
using hOps.web.ViewModels;

namespace hOps.web.Utilities
{
    public static class CalendarRecurrenceHelper
    {
        public static IEnumerable<CalendarEventDisplayViewModel> ExpandOccurrences(
            IEnumerable<CalendarEventDisplayViewModel> events,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            foreach (var calendarEvent in events)
            {
                foreach (var occurrence in EnumerateOccurrences(calendarEvent, rangeStart, rangeEnd))
                {
                    yield return occurrence;
                }
            }
        }

        private static IEnumerable<CalendarEventDisplayViewModel> EnumerateOccurrences(
            CalendarEventDisplayViewModel calendarEvent,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            var duration = calendarEvent.EndDate.Date - calendarEvent.StartDate.Date;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            var occurrenceStart = AlignOccurrenceStart(calendarEvent, rangeStart, duration);
            var safetyCounter = 0;
            var deletedDates = calendarEvent.DeletedOccurrenceDates ?? new HashSet<DateTime>();

            while (occurrenceStart <= rangeEnd && safetyCounter < 1000)
            {
                var occurrenceEnd = occurrenceStart.Add(duration);
                var isDeletedOccurrence = deletedDates.Contains(occurrenceStart.Date);

                if (!isDeletedOccurrence && occurrenceEnd >= rangeStart && occurrenceStart <= rangeEnd)
                {
                    yield return calendarEvent.CloneWithDates(occurrenceStart, occurrenceEnd);
                }

                if (calendarEvent.Recurrence == CalendarRecurrenceType.None)
                {
                    yield break;
                }

                occurrenceStart = GetNextOccurrenceStart(occurrenceStart, calendarEvent.Recurrence);
                if (occurrenceStart == DateTime.MinValue)
                {
                    yield break;
                }

                safetyCounter++;
            }
        }

        private static DateTime AlignOccurrenceStart(
            CalendarEventDisplayViewModel calendarEvent,
            DateTime rangeStart,
            TimeSpan duration)
        {
            var start = calendarEvent.StartDate.Date;
            if (calendarEvent.Recurrence == CalendarRecurrenceType.None || start >= rangeStart)
            {
                return start;
            }

            switch (calendarEvent.Recurrence)
            {
                case CalendarRecurrenceType.Daily:
                case CalendarRecurrenceType.Weekly:
                case CalendarRecurrenceType.BiWeekly:
                    var stepDays = calendarEvent.Recurrence == CalendarRecurrenceType.Daily
                        ? 1
                        : calendarEvent.Recurrence == CalendarRecurrenceType.Weekly
                            ? 7
                            : 14;
                    var diff = (int)((rangeStart.Date - start).TotalDays / stepDays);
                    if (diff > 0)
                    {
                        start = start.AddDays(diff * stepDays);
                    }
                    while (start.Add(duration) < rangeStart.Date)
                    {
                        start = start.AddDays(stepDays);
                    }

                    return start;

                case CalendarRecurrenceType.Monthly:
                    while (start.Add(duration) < rangeStart.Date)
                    {
                        start = start.AddMonths(1);
                    }

                    return start;

                case CalendarRecurrenceType.Yearly:
                    while (start.Add(duration) < rangeStart.Date)
                    {
                        start = start.AddYears(1);
                    }

                    return start;

                default:
                    return start;
            }
        }

        private static DateTime GetNextOccurrenceStart(DateTime currentStart, CalendarRecurrenceType recurrence)
        {
            return recurrence switch
            {
                CalendarRecurrenceType.Daily => currentStart.AddDays(1),
                CalendarRecurrenceType.Weekly => currentStart.AddDays(7),
                CalendarRecurrenceType.BiWeekly => currentStart.AddDays(14),
                CalendarRecurrenceType.Monthly => currentStart.AddMonths(1),
                CalendarRecurrenceType.Yearly => currentStart.AddYears(1),
                _ => DateTime.MinValue
            };
        }
    }
}

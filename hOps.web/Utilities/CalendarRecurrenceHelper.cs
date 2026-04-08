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
            var isRecurring = calendarEvent.Recurrence != CalendarRecurrenceType.None;
            var seriesEndDate = calendarEvent.EndDate.Date < calendarEvent.StartDate.Date
                ? calendarEvent.StartDate.Date
                : calendarEvent.EndDate.Date;

            var occurrenceDuration = calendarEvent.EndDate.Date - calendarEvent.StartDate.Date;
            if (occurrenceDuration < TimeSpan.Zero)
            {
                occurrenceDuration = TimeSpan.Zero;
            }

            if (isRecurring)
            {
                occurrenceDuration = TimeSpan.Zero;
            }

            var occurrenceStart = AlignOccurrenceStart(calendarEvent, rangeStart, occurrenceDuration);
            if (occurrenceStart > seriesEndDate)
            {
                yield break;
            }

            var safetyCounter = 0;
            var deletedDates = calendarEvent.DeletedOccurrenceDates ?? new HashSet<DateTime>();

            while (occurrenceStart <= rangeEnd && occurrenceStart <= seriesEndDate && safetyCounter < 1000)
            {
                var occurrenceEnd = occurrenceStart.Add(occurrenceDuration);
                var isDeletedOccurrence = deletedDates.Contains(occurrenceStart.Date);

                if (!isDeletedOccurrence && occurrenceEnd >= rangeStart && occurrenceStart <= rangeEnd)
                {
                    yield return calendarEvent.CloneWithDates(occurrenceStart, occurrenceEnd);
                }

                if (!isRecurring)
                {
                    yield break;
                }

                var nextStart = GetNextOccurrenceStart(occurrenceStart, calendarEvent.Recurrence);
                if (nextStart == DateTime.MinValue || nextStart > seriesEndDate)
                {
                    yield break;
                }

                occurrenceStart = nextStart;
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

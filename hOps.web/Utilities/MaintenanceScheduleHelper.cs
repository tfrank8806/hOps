#nullable enable

using System;

namespace hOps.web.Utilities
{
    public static class MaintenanceScheduleHelper
    {
        private const int DailyCapacity = 5; // One session per weekday, 5 per week

        public static DateTime? CalculateNextDueDate(DateTime? lastCompletedAtUtc, int frequencyPerYear, int roomIndex)
        {
            if (frequencyPerYear <= 0)
            {
                return null;
            }

            var normalizedRoomIndex = Math.Max(0, roomIndex);
            var cycleLengthDays = 365.0 / frequencyPerYear;
            var referenceDate = lastCompletedAtUtc?.Date ?? DateTime.UtcNow.Date.AddDays(-cycleLengthDays);
            var nextCycleStart = GetNextCycleStart(referenceDate, cycleLengthDays);

            var offsetBusinessDays = normalizedRoomIndex / DailyCapacity;
            var dueDate = AddBusinessDays(nextCycleStart, offsetBusinessDays);
            var cycleEnd = nextCycleStart.AddDays(Math.Ceiling(cycleLengthDays) - 1);
            if (dueDate > cycleEnd)
            {
                dueDate = MoveToPreviousBusinessDay(cycleEnd);
            }

            if (dueDate <= referenceDate)
            {
                nextCycleStart = nextCycleStart.AddDays(cycleLengthDays);
                dueDate = AddBusinessDays(nextCycleStart, offsetBusinessDays);
                cycleEnd = nextCycleStart.AddDays(Math.Ceiling(cycleLengthDays) - 1);
                if (dueDate > cycleEnd)
                {
                    dueDate = MoveToPreviousBusinessDay(cycleEnd);
                }
            }

            return dueDate;
        }

        private static DateTime GetNextCycleStart(DateTime referenceDate, double cycleLengthDays)
        {
            var cycleStart = GetCycleStart(referenceDate, cycleLengthDays);
            if (cycleStart <= referenceDate)
            {
                cycleStart = cycleStart.AddDays(cycleLengthDays);
            }

            return EnsureBusinessDay(cycleStart);
        }

        private static DateTime GetCycleStart(DateTime targetDate, double cycleLengthDays)
        {
            var yearStart = new DateTime(targetDate.Year, 1, 1);
            var delta = (targetDate - yearStart).TotalDays;

            while (delta < 0)
            {
                yearStart = yearStart.AddYears(-1);
                delta = (targetDate - yearStart).TotalDays;
            }

            var cycles = Math.Floor(delta / cycleLengthDays);
            return yearStart.AddDays(cycles * cycleLengthDays);
        }

        private static DateTime AddBusinessDays(DateTime start, int businessDays)
        {
            var date = EnsureBusinessDay(start);
            var remaining = businessDays;
            while (remaining > 0)
            {
                date = date.AddDays(1);
                if (IsBusinessDay(date))
                {
                    remaining--;
                }
            }

            return date;
        }

        private static DateTime MoveToPreviousBusinessDay(DateTime date)
        {
            var adjusted = date;
            while (!IsBusinessDay(adjusted))
            {
                adjusted = adjusted.AddDays(-1);
            }

            return adjusted;
        }

        private static DateTime EnsureBusinessDay(DateTime date)
        {
            var adjusted = date;
            while (!IsBusinessDay(adjusted))
            {
                adjusted = adjusted.AddDays(1);
            }

            return adjusted;
        }

        private static bool IsBusinessDay(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        }
    }
}

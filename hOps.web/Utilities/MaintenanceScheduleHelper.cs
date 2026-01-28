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
            var currentYearStart = new DateTime(DateTime.UtcNow.Year, 1, 1);
            var referenceDate = lastCompletedAtUtc?.Date ?? currentYearStart;
            if (referenceDate < currentYearStart)
            {
                referenceDate = currentYearStart;
            }

            var nextCycleStart = GetNextCycleStart(referenceDate, cycleLengthDays, currentYearStart);

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

        public static IReadOnlyList<MaintenanceCycleWindow> BuildCycleWindows(DateTime referenceDate, int frequencyPerYear)
        {
            var cycles = new List<MaintenanceCycleWindow>();
            if (frequencyPerYear <= 0)
            {
                return cycles;
            }

            var yearStart = new DateTime(referenceDate.Year, 1, 1);
            var monthsPerCycle = 12.0 / frequencyPerYear;
            var monthAccumulator = 0.0;
            var cycleStart = yearStart;
            var lastDueMonth = 0;

            for (int i = 0; i < frequencyPerYear; i++)
            {
                monthAccumulator += monthsPerCycle;
                var dueMonth = (int)Math.Ceiling(monthAccumulator);
                if (dueMonth <= lastDueMonth)
                {
                    dueMonth = lastDueMonth + 1;
                }
                if (dueMonth > 12)
                {
                    dueMonth = 12;
                }

                var dueDate = new DateTime(yearStart.Year, dueMonth, DateTime.DaysInMonth(yearStart.Year, dueMonth));
                if (dueDate < cycleStart)
                {
                    dueDate = cycleStart;
                }

                cycles.Add(new MaintenanceCycleWindow
                {
                    Index = i + 1,
                    StartDate = cycleStart,
                    DueDate = dueDate
                });

                if (dueMonth >= 12)
                {
                    break;
                }

                cycleStart = dueDate.AddDays(1);
                lastDueMonth = dueMonth;
            }

            return cycles;
        }

        public sealed class MaintenanceCycleWindow
        {
            public int Index { get; init; }
            public DateTime StartDate { get; init; }
            public DateTime DueDate { get; init; }
        }

        private static DateTime GetNextCycleStart(DateTime referenceDate, double cycleLengthDays, DateTime currentYearStart)
        {
            var cycleStart = GetCycleStart(referenceDate, cycleLengthDays);
            if (cycleStart < currentYearStart)
            {
                cycleStart = currentYearStart;
            }

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

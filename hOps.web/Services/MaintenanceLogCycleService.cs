#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using hOps.web.Models;
using hOps.web.Utilities;

namespace hOps.web.Services
{
    public interface IMaintenanceLogCycleService
    {
        MaintenanceLogCycleSummaryResult BuildCycleSummary(
            MaintenanceLogTemplate template,
            TimeZoneInfo timeZone,
            DateTime referenceUtc,
            IReadOnlyList<MaintenanceLogCycleCompletion>? completions,
            IReadOnlyList<MaintenanceLogEntry>? legacyEntries,
            int additionalPastBlocks = 0);

        IReadOnlyList<MaintenanceLogCycleWindow> GetVisibleWindows(
            MaintenanceLogTemplate template,
            TimeZoneInfo timeZone,
            DateTime referenceUtc,
            int additionalPastBlocks = 0);

        MaintenanceLogCycleWindow BuildWindowForDate(MaintenanceLogTemplate template, DateTime localDateTime);
    }

    public sealed class MaintenanceLogCycleService : IMaintenanceLogCycleService
    {
        private static readonly TimeSpan DefaultDueTime = new(23, 59, 0);

        public MaintenanceLogCycleSummaryResult BuildCycleSummary(
            MaintenanceLogTemplate template,
            TimeZoneInfo timeZone,
            DateTime referenceUtc,
            IReadOnlyList<MaintenanceLogCycleCompletion>? completions,
            IReadOnlyList<MaintenanceLogEntry>? legacyEntries,
            int additionalPastBlocks = 0)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (!SupportsCycleType(template.ScheduleType))
            {
                return MaintenanceLogCycleSummaryResult.Empty;
            }

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(referenceUtc, timeZone);
            var windows = BuildVisibleWindows(template, localNow, additionalPastBlocks);
            if (windows.Count == 0)
            {
                return new MaintenanceLogCycleSummaryResult
                {
                    LocalNow = localNow,
                    Windows = Array.Empty<MaintenanceLogCycleWindow>(),
                    Statuses = Array.Empty<MaintenanceLogCycleStatusResult>()
                };
            }

            var completionSource = completions ?? Array.Empty<MaintenanceLogCycleCompletion>();
            var completionLookup = completionSource
                .GroupBy(c => NormalizeKey(c.CycleWindowKey))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(c => c.CompletedAtUtc ?? c.CreatedAtUtc)
                        .ThenByDescending(c => c.Id)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var statuses = new List<MaintenanceLogCycleStatusResult>();
            var legacySource = legacyEntries ?? Array.Empty<MaintenanceLogEntry>();

            foreach (var window in windows)
            {
                completionLookup.TryGetValue(NormalizeKey(window.WindowKey), out var completionList);
                var completion = completionList?.FirstOrDefault();
                var legacyMatches = LocateLegacyEntries(window, legacySource, timeZone);
                var history = completionList != null
                    ? (IReadOnlyList<MaintenanceLogCycleCompletion>)completionList
                    : Array.Empty<MaintenanceLogCycleCompletion>();
                var status = BuildStatus(window, completion, history, legacyMatches, localNow, timeZone);
                statuses.Add(status);
            }

            return new MaintenanceLogCycleSummaryResult
            {
                LocalNow = localNow,
                Windows = windows,
                Statuses = statuses
            };
        }

        public IReadOnlyList<MaintenanceLogCycleWindow> GetVisibleWindows(
            MaintenanceLogTemplate template,
            TimeZoneInfo timeZone,
            DateTime referenceUtc,
            int additionalPastBlocks = 0)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(referenceUtc, timeZone);
            return BuildVisibleWindows(template, localNow, additionalPastBlocks);
        }

        public MaintenanceLogCycleWindow BuildWindowForDate(MaintenanceLogTemplate template, DateTime localDateTime)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (!SupportsCycleType(template.ScheduleType))
            {
                return MaintenanceLogCycleWindow.Empty;
            }

            return BuildWindow(template, DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified));
        }

        private static bool SupportsCycleType(MaintenanceLogScheduleType scheduleType)
        {
            return scheduleType is MaintenanceLogScheduleType.Daily
                or MaintenanceLogScheduleType.Weekly
                or MaintenanceLogScheduleType.Monthly
                or MaintenanceLogScheduleType.Quarterly
                or MaintenanceLogScheduleType.Yearly;
        }

        private static string NormalizeKey(string? key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }

        private static MaintenanceLogCycleStatusResult BuildStatus(
            MaintenanceLogCycleWindow window,
            MaintenanceLogCycleCompletion? completion,
            IReadOnlyList<MaintenanceLogCycleCompletion> completionHistory,
            IReadOnlyList<MaintenanceLogLegacyEntryBridgeResult> legacyEntries,
            DateTime localNow,
            TimeZoneInfo timeZone)
        {
            var statusKind = ResolveStatus(window, completion, legacyEntries, localNow);
            var isLate = EvaluateLate(window, completion, legacyEntries, timeZone);

            return new MaintenanceLogCycleStatusResult
            {
                Window = window,
                Status = statusKind,
                LatestCompletion = completion,
                Completions = completionHistory,
                LegacyEntries = legacyEntries,
                IsLate = isLate
            };
        }

        private static MaintenanceLogCycleStatusKind ResolveStatus(
            MaintenanceLogCycleWindow window,
            MaintenanceLogCycleCompletion? completion,
            IReadOnlyList<MaintenanceLogLegacyEntryBridgeResult> legacyEntries,
            DateTime localNow)
        {
            if (completion != null)
            {
                return completion.Result == MaintenanceLogCompletionResult.Failed
                    ? MaintenanceLogCycleStatusKind.Failed
                    : MaintenanceLogCycleStatusKind.Passed;
            }

            if (legacyEntries.Count > 0)
            {
                return MaintenanceLogCycleStatusKind.Passed;
            }

            if (localNow < window.StartLocal)
            {
                return MaintenanceLogCycleStatusKind.Upcoming;
            }

            if (localNow <= window.DueLocal)
            {
                return MaintenanceLogCycleStatusKind.Due;
            }

            return MaintenanceLogCycleStatusKind.Overdue;
        }

        private static bool EvaluateLate(
            MaintenanceLogCycleWindow window,
            MaintenanceLogCycleCompletion? completion,
            IReadOnlyList<MaintenanceLogLegacyEntryBridgeResult> legacyEntries,
            TimeZoneInfo timeZone)
        {
            var dueUtc = ConvertToUtc(window.DueLocal, timeZone);

            if (completion?.CompletedAtUtc != null)
            {
                return completion.CompletedAtUtc.Value > dueUtc;
            }

            if (legacyEntries.Count > 0)
            {
                var latest = legacyEntries.Max(e => e.CreatedAtUtc);
                return latest > dueUtc;
            }

            return false;
        }

        private static IReadOnlyList<MaintenanceLogLegacyEntryBridgeResult> LocateLegacyEntries(
            MaintenanceLogCycleWindow window,
            IReadOnlyList<MaintenanceLogEntry>? legacyEntries,
            TimeZoneInfo timeZone)
        {
            if (legacyEntries == null || legacyEntries.Count == 0)
            {
                return Array.Empty<MaintenanceLogLegacyEntryBridgeResult>();
            }

            var startUtc = ConvertToUtc(window.StartLocal, timeZone);
            var endUtc = ConvertToUtc(window.EndLocal, timeZone);

            var matches = legacyEntries
                .Where(entry => entry.CreatedAtUtc >= startUtc && entry.CreatedAtUtc < endUtc)
                .Select(entry => new MaintenanceLogLegacyEntryBridgeResult
                {
                    EntryId = entry.Id,
                    CreatedAtUtc = entry.CreatedAtUtc
                })
                .OrderBy(e => e.CreatedAtUtc)
                .ToList();

            return matches;
        }

        private static List<MaintenanceLogCycleWindow> BuildVisibleWindows(
            MaintenanceLogTemplate template,
            DateTime localReference,
            int additionalPastBlocks)
        {
            var options = GetWindowRequest(template.ScheduleType);
            if (options.VisiblePast < 0 || options.VisibleFuture < 0)
            {
                return new List<MaintenanceLogCycleWindow>();
            }

            var pastCount = CalculatePastCount(options, additionalPastBlocks);
            var current = BuildWindow(template, localReference);
            var windows = new List<MaintenanceLogCycleWindow>();
            for (var offset = -pastCount; offset <= options.VisibleFuture; offset++)
            {
                MaintenanceLogCycleWindow window;
                if (offset == 0)
                {
                    window = current;
                }
                else
                {
                    window = ShiftWindow(template, current, offset);
                }

                windows.Add(window);
            }

            return windows
                .OrderBy(w => w.StartLocal)
                .ToList();
        }

        private static MaintenanceLogCycleWindow BuildWindow(
            MaintenanceLogTemplate template,
            DateTime localReference)
        {
            return template.ScheduleType switch
            {
                MaintenanceLogScheduleType.Daily => BuildDailyWindow(template, localReference.Date),
                MaintenanceLogScheduleType.Weekly => BuildWeeklyWindow(template, localReference.Date),
                MaintenanceLogScheduleType.Monthly => BuildMonthlyWindow(template, localReference),
                MaintenanceLogScheduleType.Quarterly => BuildQuarterlyWindow(template, localReference),
                MaintenanceLogScheduleType.Yearly => BuildYearlyWindow(template, localReference),
                _ => MaintenanceLogCycleWindow.Empty
            };
        }

        private static MaintenanceLogCycleWindow ShiftWindow(
            MaintenanceLogTemplate template,
            MaintenanceLogCycleWindow current,
            int offset)
        {
            return template.ScheduleType switch
            {
                MaintenanceLogScheduleType.Daily => BuildDailyWindow(template, current.StartLocal.AddDays(offset)),
                MaintenanceLogScheduleType.Weekly => BuildWeeklyWindowFromStart(template, current.StartLocal.AddDays(offset * 7)),
                MaintenanceLogScheduleType.Monthly => BuildMonthlyWindowFromStart(template, current.StartLocal.AddMonths(offset)),
                MaintenanceLogScheduleType.Quarterly => BuildQuarterlyWindowFromStart(template, current.StartLocal.AddMonths(offset * 3)),
                MaintenanceLogScheduleType.Yearly => BuildYearlyWindowFromStart(template, current.StartLocal.AddYears(offset)),
                _ => current
            };
        }

        private static MaintenanceLogCycleWindow BuildDailyWindow(
            MaintenanceLogTemplate template,
            DateTime anchorDate)
        {
            var start = anchorDate.Date;
            var end = start.AddDays(1);
            var due = template.DueTimeLocal.HasValue
                ? start.Add(template.DueTimeLocal.Value)
                : end;
            return CreateWindow(template.ScheduleType, start, end, due);
        }

        private static MaintenanceLogCycleWindow BuildWeeklyWindow(
            MaintenanceLogTemplate template,
            DateTime anchorDate)
        {
            var dueDay = ResolveWeeklyDueDay(template);
            var dueDate = MoveToNextOrSame(anchorDate, dueDay);
            var start = dueDate.AddDays(-6).Date;
            return BuildWeeklyWindowFromStart(template, start);
        }

        private static MaintenanceLogCycleWindow BuildWeeklyWindowFromStart(
            MaintenanceLogTemplate template,
            DateTime start)
        {
            var end = start.AddDays(7);
            var dueTime = ResolveDueTime(template);
            var dueDate = start.AddDays(6);
            var due = dueDate.Add(dueTime);
            return CreateWindow(template.ScheduleType, start, end, due);
        }

        private static MaintenanceLogCycleWindow BuildMonthlyWindow(
            MaintenanceLogTemplate template,
            DateTime localReference)
        {
            var anchor = new DateTime(localReference.Year, localReference.Month, 1);
            return BuildMonthlyWindowFromStart(template, anchor);
        }

        private static MaintenanceLogCycleWindow BuildMonthlyWindowFromStart(
            MaintenanceLogTemplate template,
            DateTime monthStart)
        {
            var start = new DateTime(monthStart.Year, monthStart.Month, 1);
            var end = start.AddMonths(1);
            var dayOfMonth = template.DayOfMonth ?? 1;
            var maxDay = DateTime.DaysInMonth(start.Year, start.Month);
            var dueDay = Math.Clamp(dayOfMonth, 1, maxDay);
            var dueTime = ResolveDueTime(template);
            var dueDate = new DateTime(start.Year, start.Month, dueDay);
            var due = dueDate.Add(dueTime);
            if (due > end)
            {
                due = end.AddTicks(-1);
            }

            return CreateWindow(template.ScheduleType, start, end, due);
        }

        private static MaintenanceLogCycleWindow BuildQuarterlyWindow(
            MaintenanceLogTemplate template,
            DateTime localReference)
        {
            var quarterIndex = (localReference.Month - 1) / 3;
            var quarterStartMonth = (quarterIndex * 3) + 1;
            var start = new DateTime(localReference.Year, quarterStartMonth, 1);
            return BuildQuarterlyWindowFromStart(template, start);
        }

        private static MaintenanceLogCycleWindow BuildQuarterlyWindowFromStart(
            MaintenanceLogTemplate template,
            DateTime start)
        {
            var normalizedStart = new DateTime(start.Year, start.Month, 1);
            var end = normalizedStart.AddMonths(3);
            var dueTime = ResolveDueTime(template);
            var lastMonthDate = end.AddDays(-1);
            var dayOfMonth = template.DayOfMonth ?? 1;
            var maxDay = DateTime.DaysInMonth(lastMonthDate.Year, lastMonthDate.Month);
            var dueDay = Math.Clamp(dayOfMonth, 1, maxDay);
            var dueDate = new DateTime(lastMonthDate.Year, lastMonthDate.Month, dueDay);
            var due = dueDate.Add(dueTime);
            if (due > end)
            {
                due = end.AddTicks(-1);
            }

            return CreateWindow(MaintenanceLogScheduleType.Quarterly, normalizedStart, end, due);
        }

        private static MaintenanceLogCycleWindow BuildYearlyWindow(
            MaintenanceLogTemplate template,
            DateTime localReference)
        {
            var start = new DateTime(localReference.Year, 1, 1);
            return BuildYearlyWindowFromStart(template, start);
        }

        private static MaintenanceLogCycleWindow BuildYearlyWindowFromStart(
            MaintenanceLogTemplate template,
            DateTime start)
        {
            var normalizedStart = new DateTime(start.Year, 1, 1);
            var end = normalizedStart.AddYears(1);
            var dueTime = ResolveDueTime(template);
            var lastMonthDate = end.AddDays(-1);
            var dayOfMonth = template.DayOfMonth ?? 1;
            var maxDay = DateTime.DaysInMonth(lastMonthDate.Year, lastMonthDate.Month);
            var dueDay = Math.Clamp(dayOfMonth, 1, maxDay);
            var dueDate = new DateTime(lastMonthDate.Year, lastMonthDate.Month, dueDay);
            var due = dueDate.Add(dueTime);
            if (due > end)
            {
                due = end.AddTicks(-1);
            }

            return CreateWindow(MaintenanceLogScheduleType.Yearly, normalizedStart, end, due);
        }

        private static TimeSpan ResolveDueTime(MaintenanceLogTemplate template)
        {
            return template.DueTimeLocal ?? DefaultDueTime;
        }

        private static DayOfWeek ResolveWeeklyDueDay(MaintenanceLogTemplate template)
        {
            var selected = MaintenanceLogTemplateHelper.ParseWeeklyBitmask(template.WeeklyDaysBitmask);
            return selected.Count > 0 ? selected[0] : DayOfWeek.Monday;
        }

        private static MaintenanceLogCycleWindow CreateWindow(
            MaintenanceLogScheduleType scheduleType,
            DateTime start,
            DateTime end,
            DateTime due)
        {
            var windowKey = BuildWindowKey(scheduleType, start);
            return new MaintenanceLogCycleWindow
            {
                ScheduleType = scheduleType,
                StartLocal = start,
                EndLocal = end,
                DueLocal = due,
                WindowKey = windowKey
            };
        }

        private static string BuildWindowKey(MaintenanceLogScheduleType scheduleType, DateTime startLocal)
        {
            return scheduleType switch
            {
                MaintenanceLogScheduleType.Daily => $"daily:{startLocal:yyyyMMdd}",
                MaintenanceLogScheduleType.Weekly => $"weekly:{startLocal:yyyyMMdd}",
                MaintenanceLogScheduleType.Monthly => $"monthly:{startLocal:yyyyMM}",
                MaintenanceLogScheduleType.Quarterly => $"quarterly:{startLocal:yyyy}-q{((startLocal.Month - 1) / 3) + 1}",
                MaintenanceLogScheduleType.Yearly => $"yearly:{startLocal:yyyy}",
                _ => $"{scheduleType.ToString().ToLowerInvariant()}:{startLocal:O}"
            };
        }

        private static DateTime MoveToNextOrSame(DateTime reference, DayOfWeek target)
        {
            var diff = ((int)target - (int)reference.DayOfWeek + 7) % 7;
            return reference.Date.AddDays(diff);
        }

        private static DateTime ConvertToUtc(DateTime local, TimeZoneInfo timeZone)
        {
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
        }

        private static CycleWindowRequest GetWindowRequest(MaintenanceLogScheduleType scheduleType)
        {
            return scheduleType switch
            {
                MaintenanceLogScheduleType.Daily => new CycleWindowRequest(7, 0, 7),
                MaintenanceLogScheduleType.Weekly => new CycleWindowRequest(3, 0, 3),
                MaintenanceLogScheduleType.Monthly => new CycleWindowRequest(2, 0, 2),
                MaintenanceLogScheduleType.Quarterly => new CycleWindowRequest(2, 0, 2),
                MaintenanceLogScheduleType.Yearly => new CycleWindowRequest(2, 0, 1),
                _ => new CycleWindowRequest(-1, -1, 0)
            };
        }

        private static int CalculatePastCount(CycleWindowRequest request, int additionalPastBlocks)
        {
            if (request.VisiblePast <= 0)
            {
                return 0;
            }

            if (additionalPastBlocks <= 0)
            {
                return request.VisiblePast;
            }

            var blockSize = request.PastBlockSize > 0 ? request.PastBlockSize : request.VisiblePast;
            return request.VisiblePast + (blockSize * additionalPastBlocks);
        }

        private readonly record struct CycleWindowRequest(int VisiblePast, int VisibleFuture, int PastBlockSize);
    }

    public sealed class MaintenanceLogCycleSummaryResult
    {
        public static MaintenanceLogCycleSummaryResult Empty { get; } = new()
        {
            Windows = Array.Empty<MaintenanceLogCycleWindow>(),
            Statuses = Array.Empty<MaintenanceLogCycleStatusResult>(),
            LocalNow = DateTime.UtcNow
        };

        public IReadOnlyList<MaintenanceLogCycleWindow> Windows { get; init; } = Array.Empty<MaintenanceLogCycleWindow>();
        public IReadOnlyList<MaintenanceLogCycleStatusResult> Statuses { get; init; } = Array.Empty<MaintenanceLogCycleStatusResult>();
        public DateTime LocalNow { get; init; }
    }

    public sealed class MaintenanceLogCycleWindow
    {
        public static MaintenanceLogCycleWindow Empty { get; } = new();

        public string WindowKey { get; init; } = string.Empty;
        public MaintenanceLogScheduleType ScheduleType { get; init; } = MaintenanceLogScheduleType.None;
        public DateTime StartLocal { get; init; }
        public DateTime EndLocal { get; init; }
        public DateTime DueLocal { get; init; }
    }

    public sealed class MaintenanceLogCycleStatusResult
    {
        public MaintenanceLogCycleWindow Window { get; init; } = MaintenanceLogCycleWindow.Empty;
        public MaintenanceLogCycleStatusKind Status { get; init; } = MaintenanceLogCycleStatusKind.Upcoming;
        public bool IsLate { get; init; }
        public MaintenanceLogCycleCompletion? LatestCompletion { get; init; }
        public IReadOnlyList<MaintenanceLogCycleCompletion> Completions { get; init; }
            = Array.Empty<MaintenanceLogCycleCompletion>();
        public IReadOnlyList<MaintenanceLogLegacyEntryBridgeResult> LegacyEntries { get; init; }
            = Array.Empty<MaintenanceLogLegacyEntryBridgeResult>();
    }

    public sealed class MaintenanceLogLegacyEntryBridgeResult
    {
        public int EntryId { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}

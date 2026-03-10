using System;

namespace hOps.web.Utilities
{
    public static class WorkOrderSlaHelper
    {
        private const int HighThresholdHours = 12;
        private const int MediumThresholdHours = 48;

        public static WorkOrderSlaInfo Calculate(DateTime dueDate, DateTime utcNow)
        {
            var dueUtc = NormalizeDueDate(dueDate);
            var remaining = dueUtc - utcNow;
            var isOverdue = remaining.TotalMinutes < 0;

            var priority = GetPriority(remaining);
            var slaStatus = GetSlaStatus(remaining);

            return new WorkOrderSlaInfo(
                priority.Label,
                priority.CssClass,
                slaStatus.Label,
                slaStatus.CssClass,
                remaining,
                isOverdue);
        }

        public static string BuildSummaryText(WorkOrderSlaInfo info)
        {
            var duration = info.Remaining.Duration();
            var formatted = FormatDuration(duration);
            return info.IsOverdue
                ? $"Overdue by {formatted}"
                : $"Due in {formatted}";
        }

        private static DateTime NormalizeDueDate(DateTime dueDate)
        {
            DateTime dueUtc = dueDate.Kind switch
            {
                DateTimeKind.Utc => dueDate,
                DateTimeKind.Local => dueDate.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dueDate, DateTimeKind.Utc)
            };

            if (dueUtc.TimeOfDay == TimeSpan.Zero)
            {
                return dueUtc.Date.AddDays(1);
            }

            return dueUtc;
        }

        private static (string Label, string CssClass) GetPriority(TimeSpan remaining)
        {
            if (remaining.TotalMinutes < 0)
            {
                return ("Critical", "badge bg-danger");
            }

            if (remaining.TotalHours <= HighThresholdHours)
            {
                return ("High", "badge bg-warning text-dark");
            }

            if (remaining.TotalHours <= MediumThresholdHours)
            {
                return ("Normal", "badge bg-info text-dark");
            }

            return ("Low", "badge bg-light text-muted border");
        }

        private static (string Label, string CssClass) GetSlaStatus(TimeSpan remaining)
        {
            if (remaining.TotalMinutes < 0)
            {
                return ("Overdue", "sla-indicator sla-indicator--overdue");
            }

            if (remaining.TotalHours <= HighThresholdHours)
            {
                return ("At Risk", "sla-indicator sla-indicator--atrisk");
            }

            return ("On Track", "sla-indicator sla-indicator--ontrack");
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1)
            {
                return "under a minute";
            }

            if (duration.TotalHours < 1)
            {
                return $"{Math.Max(1, duration.Minutes)}m";
            }

            if (duration.TotalDays < 1)
            {
                var hours = Math.Floor(duration.TotalHours);
                var minutes = duration.Minutes;
                return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
            }

            var days = Math.Floor(duration.TotalDays);
            var remainingHours = duration.Hours;
            return remainingHours > 0 ? $"{days}d {remainingHours}h" : $"{days}d";
        }
    }

    public readonly record struct WorkOrderSlaInfo(
        string PriorityLabel,
        string PriorityClass,
        string SlaStatus,
        string SlaStatusClass,
        TimeSpan Remaining,
        bool IsOverdue);
}

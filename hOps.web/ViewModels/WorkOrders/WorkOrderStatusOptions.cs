using System;
using System.Collections.Generic;
using System.Linq;

namespace hOps.web.ViewModels.WorkOrders
{
    public static class WorkOrderStatusOptions
    {
        public const string DefaultStatus = "New";

        public static IReadOnlyList<WorkOrderStatusOption> All { get; } = new List<WorkOrderStatusOption>
        {
            new() { Key = "New", Label = "Open", ColorHex = "#0d6efd" },
            new() { Key = "In Progress", Label = "In Progress", ColorHex = "#ffc107" },
            new() { Key = "Escalated", Label = "Escalated", ColorHex = "#d63384" },
            new() { Key = "On Hold", Label = "On Hold", ColorHex = "#6c757d" },
            new() { Key = "Completed", Label = "Completed", ColorHex = "#198754" },
            new() { Key = "Cancelled", Label = "Cancelled", ColorHex = "#dc3545" }
        };

        public static string GetColor(string status)
        {
            var match = FindMatch(status);
            return match?.ColorHex ?? "#6c757d";
        }

        public static string GetLabel(string status)
        {
            var match = FindMatch(status);
            return match?.Label ?? status;
        }

        private static WorkOrderStatusOption? FindMatch(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            return All.FirstOrDefault(s =>
                s.Key.Equals(status, StringComparison.OrdinalIgnoreCase) ||
                (s.Key.Equals("New", StringComparison.OrdinalIgnoreCase) && status.Equals("Open", StringComparison.OrdinalIgnoreCase)));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace hOps.web.ViewModels.WorkOrders
{
    public static class WorkOrderStatusOptions
    {
        public static IReadOnlyList<WorkOrderStatusOption> All { get; } = new List<WorkOrderStatusOption>
        {
            new() { Key = "New", Label = "New", ColorHex = "#0d6efd" },
            new() { Key = "In Progress", Label = "In Progress", ColorHex = "#ffc107" },
            new() { Key = "Completed", Label = "Completed", ColorHex = "#198754" },
            new() { Key = "On Hold", Label = "On Hold", ColorHex = "#6c757d" },
            new() { Key = "Cancelled", Label = "Cancelled", ColorHex = "#dc3545" },
            new() { Key = "Escalated", Label = "Escalated", ColorHex = "#d63384" }
        };

        public static string GetColor(string status)
        {
            var match = All.FirstOrDefault(s => s.Key.Equals(status, StringComparison.OrdinalIgnoreCase));
            return match?.ColorHex ?? "#6c757d";
        }
    }
}

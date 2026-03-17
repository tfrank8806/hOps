#nullable enable

using System;

namespace hOps.web.ViewModels.Maintenance
{
    public class MaintenanceCycleDefinitionViewModel
    {
        public int Index { get; set; }
        public DateTime DueDate { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class MaintenanceCycleStatusViewModel
    {
        public int Index { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? SessionId { get; set; }
        public string? Notes { get; set; }
    }
}

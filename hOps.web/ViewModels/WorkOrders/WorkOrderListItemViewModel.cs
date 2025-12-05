using System;
using System.Collections.Generic;

namespace hOps.web.ViewModels.WorkOrders
{
    public class WorkOrderListItemViewModel
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#6c757d";
        public string Location { get; set; } = string.Empty;
        public string? WorkOrderType { get; set; }
        public string Issue { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Department { get; set; }
        public string? DepartmentColor { get; set; }
        public string? Creator { get; set; }
        public string PriorityLabel { get; set; } = string.Empty;
        public string PriorityClass { get; set; } = "badge bg-light text-dark border";
        public string SlaStatus { get; set; } = string.Empty;
        public string SlaStatusClass { get; set; } = string.Empty;
        public string SlaSummary { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
        public List<string> Properties { get; set; } = new();
        public List<WorkOrderAttachmentViewModel> Attachments { get; set; } = new();
    }
}

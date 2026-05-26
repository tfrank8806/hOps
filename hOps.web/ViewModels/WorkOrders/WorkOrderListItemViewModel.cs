using System;
using System.Collections.Generic;

namespace hOps.web.ViewModels.WorkOrders
{
    public class WorkOrderListItemViewModel
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#6c757d";
        public string StatusLabel { get; set; } = string.Empty;
        public string TranslatedStatusLabel { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string TranslatedLocation { get; set; } = string.Empty;
        public string? WorkOrderType { get; set; }
        public string? TranslatedWorkOrderType { get; set; }
        public int? WorkOrderTypeId { get; set; }
        public string Issue { get; set; } = string.Empty;
        public string TranslatedIssue { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? TranslatedDetails { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Department { get; set; }
        public string? TranslatedDepartment { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentColor { get; set; }
        public string? AssignedToName { get; set; }
        public string? AssignedToId { get; set; }
        public string TranslatedAssignedTo { get; set; } = string.Empty;
        public string? Creator { get; set; }
        public string PriorityLabel { get; set; } = string.Empty;
        public string PriorityClass { get; set; } = "badge bg-light text-dark border";
        public string SlaStatus { get; set; } = string.Empty;
        public string SlaStatusClass { get; set; } = string.Empty;
        public string SlaSummary { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
        public string? CompletionNotes { get; set; }
        public string? TranslatedCompletionNotes { get; set; }
        public List<WorkOrderPropertyDisplayViewModel> Properties { get; set; } = new();
        public List<string> TranslatedPropertyNames { get; set; } = new();
        public List<string> TranslatedProperties { get; set; } = new();
        public List<WorkOrderAttachmentViewModel> Attachments { get; set; } = new();
    }

    public class WorkOrderPropertyDisplayViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TranslatedName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}

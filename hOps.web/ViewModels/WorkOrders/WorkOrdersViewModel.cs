using System.Collections.Generic;
using hOps.web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.WorkOrders
{
    public class WorkOrdersViewModel
    {
        public List<WorkOrderListItemViewModel> WorkOrders { get; set; } = new();
        public WorkOrderFilterInput Filters { get; set; } = new();
        public WorkOrderFormViewModel Form { get; set; } = new();
        public List<WorkOrderStatusOption> StatusOptions { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<WorkOrderType> WorkOrderTypes { get; set; } = new();
        public List<PropertyOptionViewModel> PropertyFilterOptions { get; set; } = new();
        public List<PropertyOptionViewModel> PropertyOptions { get; set; } = new();
        public List<SelectListItem> CreatorOptions { get; set; } = new();
        public List<string> LocationSuggestions { get; set; } = new();
        public Dictionary<string, string> StatusColorMap { get; set; } = new();
        public int? EditingWorkOrderId { get; set; }
        public bool IsEditing => EditingWorkOrderId.HasValue;
        public bool CanManageWorkOrders { get; set; }
    }
}

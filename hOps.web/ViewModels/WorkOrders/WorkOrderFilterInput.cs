using System;

namespace hOps.web.ViewModels.WorkOrders
{
    public class WorkOrderFilterInput
    {
        public string SortOrder { get; set; } = "newest";
        public string? RoomNumber { get; set; }
        public int? DepartmentId { get; set; }
        public int? WorkOrderTypeId { get; set; }
        public string? Status { get; set; }
        public string? CreatorId { get; set; }
        public string? Search { get; set; }
        public int? PropertyId { get; set; }
    }
}

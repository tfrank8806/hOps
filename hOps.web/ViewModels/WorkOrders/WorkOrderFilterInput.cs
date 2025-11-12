using System;
using System.Collections.Generic;
using System.Linq;

namespace hOps.web.ViewModels.WorkOrders
{
    public class WorkOrderFilterInput
    {
        public string SortOrder { get; set; } = "newest";
        public string? RoomNumber { get; set; }
        public List<int> DepartmentIds { get; set; } = new();
        public List<int> WorkOrderTypeIds { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
        public List<string> CreatorIds { get; set; } = new();
        public string? Search { get; set; }
        public List<int> PropertyIds { get; set; } = new();
        public bool HideCompleted { get; set; } = true;

        public void Normalize()
        {
            SortOrder = string.IsNullOrWhiteSpace(SortOrder) ? "newest" : SortOrder.Trim().ToLowerInvariant();
            RoomNumber = string.IsNullOrWhiteSpace(RoomNumber) ? null : RoomNumber.Trim();
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

            DepartmentIds = NormalizePositiveIds(DepartmentIds);
            WorkOrderTypeIds = NormalizePositiveIds(WorkOrderTypeIds);
            PropertyIds = NormalizePositiveIds(PropertyIds);

            Statuses = NormalizeStringList(Statuses);
            CreatorIds = NormalizeStringList(CreatorIds);
        }

        private static List<int> NormalizePositiveIds(IEnumerable<int> source) =>
            source.Where(id => id > 0).Distinct().ToList();

        private static List<string> NormalizeStringList(IEnumerable<string> source) =>
            source
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}

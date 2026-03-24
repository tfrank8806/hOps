#nullable enable

using System.Collections.Generic;

namespace hOps.web.ViewModels.Housekeeping.LinenInventory
{
    public class SupplyInventoryPageViewModel
    {
        public int PropertyId { get; set; }

        public string PropertyName { get; set; } = string.Empty;

        public decimal DefaultMonthlyBudget { get; set; }

        public List<SupplyInventoryItemViewModel> TemplateItems { get; set; } = new();

        public List<LinenInventoryHistoryEntryViewModel> HistoryEntries { get; set; } = new();
    }

    public class SupplyInventoryItemViewModel
    {
        public string Item { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PartNumber { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal QuantityPerCase { get; set; }
    }
}

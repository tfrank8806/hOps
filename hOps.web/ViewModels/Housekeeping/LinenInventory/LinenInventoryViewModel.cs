#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Housekeeping.LinenInventory
{
    public class LinenInventoryPageViewModel
    {
        public int PropertyId { get; set; }

        public string PropertyName { get; set; } = string.Empty;

        public bool CanEditSetup { get; set; }

        public LinenInventoryEntryForm Entry { get; set; } = new();

        public List<LinenInventoryItemRowViewModel> InventoryRows { get; set; } = new();

        public LinenInventorySetupViewModel Setup { get; set; } = new();

        public DateTime? LastInventoryDate { get; set; }

        public decimal? LastSessionTotalCost { get; set; }

        public decimal? LastSessionProjectedNeed { get; set; }

        public string? FlashMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public List<LinenInventoryHistoryEntryViewModel> HistoryEntries { get; set; } = new();
    }

    public class LinenInventoryItemRowViewModel
    {
        public int ItemId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string? OrderItemNumber { get; set; }

        public decimal OrderCaseCount { get; set; }

        public decimal OrderCasePrice { get; set; }

        public decimal ParLevelTarget { get; set; }

        public decimal InRooms { get; set; }

        public decimal BudgetedPar { get; set; }

        public decimal LastMonthActuals { get; set; }
    }

    public class LinenInventoryEntryForm
    {
        [DataType(DataType.Date)]
        public DateTime InventoryDate { get; set; } = DateTime.UtcNow.Date;

        [MaxLength(200)]
        public string? PerformedBy { get; set; }

        [Range(0, 500000)]
        public decimal MonthlyBudget { get; set; }

        public List<LinenInventoryEntryRowInput> Rows { get; set; } = new();
    }

    public class LinenInventoryEntryRowInput
    {
        [Required]
        public int ItemId { get; set; }

        [Range(0, 100000)]
        public decimal LaundryClean { get; set; }

        [Range(0, 100000)]
        public decimal LaundryDirty { get; set; }

        [Range(0, 100000)]
        public decimal InStorage { get; set; }

        [Range(0, 100000)]
        public decimal OnCarts { get; set; }

        [Range(0, 100000)]
        public decimal LastMonthActuals { get; set; }

        [Range(0, 100000)]
        public decimal CasesPurchased { get; set; }
    }

    public class LinenInventorySetupViewModel
    {
        [MaxLength(200)]
        public string? PropertyLabel { get; set; }

        [Range(0, 500000)]
        public decimal DefaultMonthlyBudget { get; set; }

        public List<LinenInventoryRoomTypeForm> RoomTypes { get; set; } = new();

        public List<LinenInventoryItemForm> Items { get; set; } = new();
    }

    public class LinenInventoryRoomTypeForm
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 5000)]
        public int TotalRooms { get; set; }

        public int SortOrder { get; set; }

        public bool IsDeleted { get; set; }
    }

    public class LinenInventoryItemForm
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? OrderItemNumber { get; set; }

        [Range(0, 10000)]
        public decimal OrderCaseCount { get; set; } = 1m;

        [Range(0, 100000)]
        public decimal OrderCasePrice { get; set; }

        [Range(0, 1000)]
        public decimal ParLevelTarget { get; set; } = 1m;

        public int SortOrder { get; set; }

        public bool IsDeleted { get; set; }

        public List<LinenInventoryItemRequirementForm> Requirements { get; set; } = new();
    }

    public class LinenInventoryItemRequirementForm
    {
        public int RoomTypeId { get; set; }

        [Range(0, 1000)]
        public decimal UnitsPerRoom { get; set; }
    }

    public class LinenInventoryRoomTypeCollectionInput
    {
        [MaxLength(200)]
        public string? PropertyLabel { get; set; }

        [Range(0, 500000)]
        public decimal DefaultMonthlyBudget { get; set; }

        public List<LinenInventoryRoomTypeForm> RoomTypes { get; set; } = new();
    }

    public class LinenInventoryItemCollectionInput
    {
        public List<LinenInventoryItemForm> Items { get; set; } = new();
    }

    public class LinenInventoryHistoryEntryViewModel
    {
        public int SessionId { get; set; }

        public DateTime InventoryDate { get; set; }

        public string DisplayLabel => InventoryDate.ToString("MMMM d, yyyy");

        public string? PerformedBy { get; set; }

        public decimal TotalCost { get; set; }

        public decimal ProjectedNeedCost { get; set; }

        public decimal MonthlyBudget { get; set; }
    }

    public class LinenInventoryHistoryDetailViewModel
    {
        public int SessionId { get; set; }
        public string PropertyName { get; set; } = string.Empty;

        public DateTime InventoryDate { get; set; }

        public string? PerformedBy { get; set; }

        public decimal MonthlyBudget { get; set; }

        public decimal TotalCost { get; set; }

        public decimal ProjectedNeedCost { get; set; }

        public List<LinenInventoryHistoryDetailRow> Items { get; set; } = new();
    }

    public class LinenInventoryHistoryDetailRow
    {
        public string ItemName { get; set; } = string.Empty;
        public string? ItemNumber { get; set; }
        public decimal LaundryClean { get; set; }
        public decimal LaundryDirty { get; set; }
        public decimal InStorage { get; set; }
        public decimal OnCarts { get; set; }
        public decimal TotalOnHand { get; set; }
        public decimal LastMonthActuals { get; set; }
        public decimal BudgetedPar { get; set; }
        public decimal OrderRecommendation { get; set; }
        public decimal CasesToOrder { get; set; }
        public decimal NeedCost { get; set; }
        public decimal CasesPurchased { get; set; }
        public decimal OrderCost { get; set; }
    }
}

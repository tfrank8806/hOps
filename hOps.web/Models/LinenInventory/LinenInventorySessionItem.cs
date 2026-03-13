#nullable enable

using Microsoft.EntityFrameworkCore;

namespace hOps.web.Models
{
    public class LinenInventorySessionItem
    {
        public int Id { get; set; }

        public int SessionId { get; set; }

        public LinenInventorySession Session { get; set; } = null!;

        public int InventoryItemId { get; set; }

        public LinenInventoryItem InventoryItem { get; set; } = null!;

        [Precision(18, 2)]
        public decimal LaundryClean { get; set; }

        [Precision(18, 2)]
        public decimal LaundryDirty { get; set; }

        [Precision(18, 2)]
        public decimal InStorage { get; set; }

        [Precision(18, 2)]
        public decimal OnCarts { get; set; }

        [Precision(18, 2)]
        public decimal TotalOnHand { get; set; }

        [Precision(18, 2)]
        public decimal LastMonthActuals { get; set; }

        [Precision(18, 2)]
        public decimal InRoomsQuantity { get; set; }

        [Precision(18, 2)]
        public decimal BudgetedPar { get; set; }

        [Precision(18, 2)]
        public decimal OrderRecommendation { get; set; }

        [Precision(18, 4)]
        public decimal ActToParRatio { get; set; }

        [Precision(18, 2)]
        public decimal CasesToOrder { get; set; }

        [Precision(18, 2)]
        public decimal NeedCost { get; set; }

        [Precision(18, 2)]
        public decimal CasesPurchased { get; set; }

        [Precision(18, 2)]
        public decimal OrderCost { get; set; }
    }
}

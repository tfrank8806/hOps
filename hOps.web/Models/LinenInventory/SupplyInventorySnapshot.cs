#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Models
{
    public class SupplyInventorySnapshot
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;

        [Precision(18, 2)]
        public decimal MonthlyBudget { get; set; }

        [Precision(18, 2)]
        public decimal TotalInventoryValue { get; set; }

        [Precision(18, 2)]
        public decimal TotalOrderCost { get; set; }

        [Required]
        public string DataJson { get; set; } = string.Empty;

        [Required]
        public string SavedByUserId { get; set; } = string.Empty;

        public ApplicationUser SavedByUser { get; set; } = null!;

        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

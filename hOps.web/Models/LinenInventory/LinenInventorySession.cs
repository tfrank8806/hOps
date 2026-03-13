#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Models
{
    public class LinenInventorySession
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;

        public int Year { get; set; }

        public int Month { get; set; }

        public DateTime InventoryDate { get; set; }

        [Precision(18, 2)]
        public decimal MonthlyBudget { get; set; }

        [MaxLength(200)]
        public string? PerformedBy { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;

        public ApplicationUser CreatedByUser { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [Precision(18, 2)]
        public decimal TotalCost { get; set; }

        [Precision(18, 2)]
        public decimal ProjectedNeedCost { get; set; }

        public ICollection<LinenInventorySessionItem> Items { get; set; } = new List<LinenInventorySessionItem>();
    }
}

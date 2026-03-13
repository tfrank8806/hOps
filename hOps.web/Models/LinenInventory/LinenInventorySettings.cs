#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Models
{
    public class LinenInventorySettings
    {
        [Key]
        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;

        [MaxLength(200)]
        public string? PropertyLabel { get; set; }

        [Precision(18, 2)]
        public decimal DefaultMonthlyBudget { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }

        public ApplicationUser? UpdatedByUser { get; set; }
    }
}

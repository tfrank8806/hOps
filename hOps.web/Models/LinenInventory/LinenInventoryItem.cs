#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Models
{
    public class LinenInventoryItem
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? OrderItemNumber { get; set; }

        [Precision(18, 2)]
        public decimal OrderCaseCount { get; set; } = 1m;

        [Precision(18, 2)]
        public decimal OrderCasePrice { get; set; }

        [Precision(18, 2)]
        public decimal ParLevelTarget { get; set; } = 2m;

        public int SortOrder { get; set; }

        public bool IsArchived { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }

        public ApplicationUser? UpdatedByUser { get; set; }

        public ICollection<LinenInventoryItemRequirement> Requirements { get; set; } = new List<LinenInventoryItemRequirement>();
    }
}

#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PreventiveMaintenanceTask
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}


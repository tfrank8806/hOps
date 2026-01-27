#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PreventiveMaintenanceSetting
    {
        public int Id { get; set; }

        [Range(1, 52)]
        public int FrequencyPerYear { get; set; } = 4;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        public string? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedByUser { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}


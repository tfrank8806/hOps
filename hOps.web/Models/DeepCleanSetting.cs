#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class DeepCleanSetting
    {
        [Key]
        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;

        [Range(1, 52)]
        public int FrequencyPerYear { get; set; } = 1;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedByUser { get; set; }
    }
}

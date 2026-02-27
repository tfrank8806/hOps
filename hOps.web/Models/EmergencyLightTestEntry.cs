#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class EmergencyLightTestEntry
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Location { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime TestedAtUtc { get; set; } = DateTime.UtcNow.Date;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        public string CreatedByUserId { get; set; } = string.Empty;
        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

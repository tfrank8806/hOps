#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class EquipmentItem
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        [MaxLength(96)]
        public string? Category { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(160)]
        public string? Location { get; set; }

        [MaxLength(160)]
        public string? Brand { get; set; }

        [MaxLength(160)]
        public string? Model { get; set; }

        [MaxLength(160)]
        public string? SerialNumber { get; set; }

        [MaxLength(160)]
        public string? VendorName { get; set; }

        [MaxLength(32)]
        public string? VendorPhone { get; set; }

        [MaxLength(160)]
        [EmailAddress]
        public string? VendorEmail { get; set; }

        public DateTime? InstalledOn { get; set; }
        public DateTime? WarrantyEndsOn { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

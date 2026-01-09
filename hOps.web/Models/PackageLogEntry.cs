using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PackageLogEntry
    {
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        [Required]
        [MaxLength(256)]
        public string RecipientName { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? RoomNumber { get; set; }

        [MaxLength(128)]
        public string? Carrier { get; set; }

        [MaxLength(128)]
        public string? TrackingNumber { get; set; }

        [MaxLength(512)]
        public string? Notes { get; set; }

        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(150)]
        public string? StorageLocation { get; set; }

        public DateTime? ArrivalDate { get; set; }

        public DateTime? DepartureDate { get; set; }

        public bool Delivered { get; set; }

        public DateTime? DeliveredAt { get; set; }

        public DateTime? PackageReceivedDate { get; set; }

        [Required]
        public string LoggedById { get; set; } = string.Empty;
        public ApplicationUser? LoggedBy { get; set; }
    }
}

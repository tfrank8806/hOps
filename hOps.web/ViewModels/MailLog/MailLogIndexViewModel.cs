using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.MailLog
{
    public class MailLogIndexViewModel
    {
        public int? CurrentPropertyId { get; set; }
        public string? CurrentPropertyName { get; set; }
        public PackageLogEntryForm Form { get; set; } = new();
        public List<PackageLogEntryRowViewModel> Entries { get; set; } = new();
        public bool HasProperty => CurrentPropertyId.HasValue;
    }

    public class PackageLogEntryRowViewModel
    {
        public int Id { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string? RoomNumber { get; set; }
        public string? Carrier { get; set; }
        public string? TrackingNumber { get; set; }
        public string? StorageLocation { get; set; }
        public DateTime? ArrivalDate { get; set; }
        public DateTime? DepartureDate { get; set; }
        public string? Notes { get; set; }
        public DateTime LoggedAt { get; set; }
        public string LoggedByName { get; set; } = string.Empty;
        public bool Delivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    public class PackageLogEntryForm
    {
        [Required]
        [MaxLength(256)]
        [Display(Name = "Recipient Name")]
        public string RecipientName { get; set; } = string.Empty;

        [MaxLength(64)]
        [Display(Name = "Room Number")]
        public string? RoomNumber { get; set; }

        [MaxLength(128)]
        public string? Carrier { get; set; }

        [MaxLength(128)]
        [Display(Name = "Tracking Number")]
        public string? TrackingNumber { get; set; }

        [MaxLength(150)]
        [Display(Name = "Storage Location")]
        public string? StorageLocation { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Arrival Date")]
        public DateTime? ArrivalDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Departure Date")]
        public DateTime? DepartureDate { get; set; }

        [MaxLength(512)]
        public string? Notes { get; set; }
    }
}

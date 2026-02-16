#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Maintenance
{
    public class MaintenanceEquipmentIndexViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public IReadOnlyList<MaintenanceEquipmentListItemViewModel> Items { get; set; } = Array.Empty<MaintenanceEquipmentListItemViewModel>();
    }

    public class MaintenanceEquipmentListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Location { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? InstalledOn { get; set; }
        public DateTime? WarrantyEndsOn { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class MaintenanceEquipmentEditorViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public bool IsEditMode => Id.HasValue;

        public int? Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(96)]
        public string? Category { get; set; }

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

        [EmailAddress]
        [MaxLength(160)]
        public string? VendorEmail { get; set; }

        [DataType(DataType.Date)]
        public DateTime? InstalledOn { get; set; }

        [DataType(DataType.Date)]
        public DateTime? WarrantyEndsOn { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class MaintenanceEquipmentDetailsViewModel
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanManage { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Location { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? VendorName { get; set; }
        public string? VendorPhone { get; set; }
        public string? VendorEmail { get; set; }
        public DateTime? InstalledOn { get; set; }
        public DateTime? WarrantyEndsOn { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}

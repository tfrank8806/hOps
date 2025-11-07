using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.WorkOrders
{
    public class LayoutWorkOrderRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int PropertyId { get; set; }

        [Required]
        [MaxLength(128)]
        public string RoomNumber { get; set; } = string.Empty;

        [MaxLength(128)]
        public string? RoomLabel { get; set; }

        [Required]
        [MaxLength(256)]
        public string Issue { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Details { get; set; }

        public int? WorkOrderTypeId { get; set; }

        public int? DepartmentId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class WorkOrder
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(128)]
        public string Location { get; set; } = string.Empty;

        public int? WorkOrderTypeId { get; set; }
        public WorkOrderType? WorkOrderType { get; set; }

        [Required]
        [MaxLength(256)]
        public string Issue { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Details { get; set; }

        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; } = DateTime.UtcNow.Date.AddDays(1);

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int? EquipmentItemId { get; set; }
        public EquipmentItem? EquipmentItem { get; set; }

        public ICollection<WorkOrderProperty> Properties { get; set; } = new List<WorkOrderProperty>();

        public ICollection<WorkOrderAttachment> Attachments { get; set; } = new List<WorkOrderAttachment>();

        public ICollection<UserToDoItem> ToDoItems { get; set; } = new List<UserToDoItem>();
    }
}

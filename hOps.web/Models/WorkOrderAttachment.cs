using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class WorkOrderAttachment
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? OriginalFileName { get; set; }
    }
}

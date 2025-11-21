using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Api
{
    public class WorkOrderListItemDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string WorkOrderType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? DueDateUtc { get; set; }
        public string Creator { get; set; } = string.Empty;
        public IReadOnlyCollection<string> Properties { get; set; } = Array.Empty<string>();
    }

    public class WorkOrderDetailDto : WorkOrderListItemDto
    {
        public string Details { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public int? WorkOrderTypeId { get; set; }
        public IReadOnlyCollection<WorkOrderAttachmentDto> Attachments { get; set; } = Array.Empty<WorkOrderAttachmentDto>();
    }

    public class WorkOrderAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public class WorkOrderListQuery
    {
        public int? PropertyId { get; set; }
        public string? Status { get; set; }
        public int Take { get; set; } = 50;
    }

    public class CreateWorkOrderRequest
    {
        [Required]
        [MaxLength(200)]
        public string Issue { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Details { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public DateTime? DueDateUtc { get; set; }

        public int? DepartmentId { get; set; }

        public int? WorkOrderTypeId { get; set; }

        [Required]
        [MinLength(1)]
        public List<int> PropertyIds { get; set; } = new();
    }

    public class UpdateWorkOrderRequest : CreateWorkOrderRequest
    {
    }
}

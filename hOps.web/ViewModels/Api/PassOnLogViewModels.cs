using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Api
{
    public class PassOnLogListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public string? CreatorPhotoUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsUnread { get; set; }
        public IReadOnlyCollection<string> Properties { get; set; } = Array.Empty<string>();
        public int CommentCount { get; set; }
    }

    public class PassOnLogDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public string? CreatorPhotoUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public IReadOnlyCollection<string> Properties { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<PassOnLogCommentDto> Comments { get; set; } = Array.Empty<PassOnLogCommentDto>();
        public IReadOnlyCollection<PassOnLogAttachmentDto> Attachments { get; set; } = Array.Empty<PassOnLogAttachmentDto>();
    }

    public class PassOnLogCommentDto
    {
        public int Id { get; set; }
        public string Body { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public string? CreatorPhotoUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class PassOnLogAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public class CreatePassOnLogRequest
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<int> PropertyIds { get; set; } = new();
    }

    public class AddPassOnLogCommentRequest
    {
        [Required]
        [MaxLength(2000)]
        public string Body { get; set; } = string.Empty;
    }
}

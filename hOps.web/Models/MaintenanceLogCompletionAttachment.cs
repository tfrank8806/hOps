#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class MaintenanceLogCompletionAttachment
    {
        public int Id { get; set; }

        public int CompletionId { get; set; }
        public MaintenanceLogCycleCompletion Completion { get; set; } = null!;

        [Required]
        [MaxLength(260)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(160)]
        public string? OriginalFileName { get; set; }

        [MaxLength(120)]
        public string? ContentType { get; set; }

        public long FileSizeBytes { get; set; }

        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    }
}


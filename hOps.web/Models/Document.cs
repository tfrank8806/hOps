using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class Document
    {
        public int Id { get; set; }

        [MaxLength(150)]
        public string? Title { get; set; }

        [Required]
        [MaxLength(256)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string OriginalFileName { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? ContentType { get; set; }

        public long FileSizeBytes { get; set; }

        public int? FolderId { get; set; }
        public DocumentFolder? Folder { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        public DocumentAccessScope AccessScope { get; set; }

        public int? PropertyId { get; set; }
        public Property? Property { get; set; }

        [Required]
        public string UploadedById { get; set; } = string.Empty;
        public ApplicationUser UploadedBy { get; set; } = null!;

        public DateTime UploadedAtUtc { get; set; }

        public ICollection<DocumentProperty> DocumentProperties { get; set; } = new List<DocumentProperty>();
    }

    public enum DocumentAccessScope
    {
        PropertyOnly = 0,
        SelectedProperties = 1,
        AllUsers = 2
    }
}

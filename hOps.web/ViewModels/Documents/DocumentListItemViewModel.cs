using System;
using System.Collections.Generic;
using hOps.web.Models;

namespace hOps.web.ViewModels.Documents
{
    public class DocumentListItemViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeDisplay { get; set; } = string.Empty;
        public DateTime UploadedAtUtc { get; set; }
        public string UploadedByDisplayName { get; set; } = string.Empty;
        public DocumentAccessScope AccessScope { get; set; }
        public string AccessSummary { get; set; } = string.Empty;
        public IReadOnlyList<string> TargetProperties { get; set; } = Array.Empty<string>();
        public int? FolderId { get; set; }
        public string? FolderPath { get; set; }
    }
}

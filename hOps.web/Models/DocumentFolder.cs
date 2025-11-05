using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class DocumentFolder
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public int? ParentFolderId { get; set; }
        public DocumentFolder? ParentFolder { get; set; }

        public ICollection<DocumentFolder> SubFolders { get; set; } = new List<DocumentFolder>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();

        [Required]
        public DocumentFolderVisibility Visibility { get; set; } = DocumentFolderVisibility.Global;

        public ICollection<DocumentFolderProperty> FolderProperties { get; set; } = new List<DocumentFolderProperty>();

        [Required]
        public string CreatedById { get; set; } = string.Empty;
        public ApplicationUser CreatedBy { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; }
    }

    public enum DocumentFolderVisibility
    {
        Global = 0,
        SelectedProperties = 1
    }
}

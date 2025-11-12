using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;

namespace hOps.web.ViewModels.Documents
{
    public class DocumentFolderSettingsForm
    {
        [Required]
        public int FolderId { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Folder Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Visibility")]
        public DocumentFolderVisibility Visibility { get; set; } = DocumentFolderVisibility.Global;

        [Display(Name = "Visible To Properties")]
        public List<int> SelectedPropertyIds { get; set; } = new();

        [Display(Name = "Apply visibility to all documents in this folder")]
        public bool ApplyToDocuments { get; set; }
    }
}

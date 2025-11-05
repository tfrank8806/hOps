using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;

namespace hOps.web.ViewModels.Documents
{
    public class DocumentFolderFormViewModel
    {
        [Required]
        [MaxLength(100)]
        [Display(Name = "Folder Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Parent Folder")]
        public int? ParentFolderId { get; set; }

        [Display(Name = "Visibility")]
        public DocumentFolderVisibility Visibility { get; set; } = DocumentFolderVisibility.Global;

        [Display(Name = "Visible To Properties")]
        public List<int> SelectedPropertyIds { get; set; } = new();
    }
}

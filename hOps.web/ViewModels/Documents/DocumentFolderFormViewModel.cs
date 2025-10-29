using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
    }
}

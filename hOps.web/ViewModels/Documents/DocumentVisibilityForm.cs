using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;

namespace hOps.web.ViewModels.Documents
{
    public class DocumentVisibilityForm
    {
        [Required]
        public int DocumentId { get; set; }

        [Display(Name = "Visibility")]
        public DocumentAccessScope AccessScope { get; set; }

        [Display(Name = "Single Property")]
        public int? PropertyId { get; set; }

        [Display(Name = "Selected Properties")]
        public List<int> SelectedPropertyIds { get; set; } = new();
    }
}

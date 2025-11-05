using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;
using Microsoft.AspNetCore.Http;

namespace hOps.web.ViewModels.Documents
{
    public class DocumentUploadFormViewModel
    {
        [Display(Name = "Document Title")]
        [MaxLength(150)]
        public string? Title { get; set; }

        [Display(Name = "Description / Notes")]
        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a file to upload.")]
        public IFormFile? File { get; set; }

        [Display(Name = "Folder")]
        public int? FolderId { get; set; }

        [Display(Name = "Allow Download By")]
        public DocumentAccessScope AccessScope { get; set; } = DocumentAccessScope.PropertyOnly;

        [Display(Name = "Property")]
        public int? PropertyId { get; set; }

        [Display(Name = "Properties")]
        public List<int> SelectedPropertyIds { get; set; } = new List<int>();
    }
}

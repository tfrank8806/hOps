using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.Support
{
    public class SupportTicketViewModel
    {
        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; } = "Issue";

        [Required]
        [StringLength(120)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
        [Display(Name = "How can we help?")]
        public string Message { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Contact email")]
        public string? ContactEmail { get; set; }

        [Display(Name = "Name")]
        public string? ContactName { get; set; }

        [Display(Name = "Attachments")]
        [DataType(DataType.Upload)]
        public List<IFormFile> Attachments { get; set; } = new();

        public IEnumerable<SelectListItem> CategoryOptions { get; set; } = BuildCategoryOptions();

        public bool SubmittedSuccessfully { get; set; }

        public static IEnumerable<SelectListItem> BuildCategoryOptions() =>
            new[]
            {
                new SelectListItem { Text = "Report an Issue", Value = "Issue" },
                new SelectListItem { Text = "Feature Request", Value = "Request" },
                new SelectListItem { Text = "Question / Guidance", Value = "Question" }
            };
    }
}

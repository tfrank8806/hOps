using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.SiteVisit
{
    public class SiteVisitPageViewModel
    {
        [Display(Name = "Property")]
        [StringLength(200)]
        public string? PropertyName { get; set; }

        [Display(Name = "Visit date")]
        [DataType(DataType.Date)]
        public DateTime VisitDate { get; set; } = DateTime.Today;

        [Display(Name = "Visit lead")]
        [StringLength(200)]
        public string? LeaderName { get; set; }

        [Display(Name = "Visit summary")]
        [StringLength(2000)]
        public string? SummaryNotes { get; set; }

        public List<SiteVisitChecklistItemViewModel> Items { get; set; } = new();

        [Required(ErrorMessage = "Please enter at least one recipient email address.")]
        [Display(Name = "Send results to")]
        public string RecipientEmails { get; set; } = string.Empty;

        public bool SubmittedSuccessfully { get; set; }

        public string? SuccessMessage { get; set; }
    }
}

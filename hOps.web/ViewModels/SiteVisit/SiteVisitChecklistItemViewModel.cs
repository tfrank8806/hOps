using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.SiteVisit
{
    public enum SiteVisitChecklistStatus
    {
        NotReviewed,
        Compliant,
        NeedsReview,
        NotCompliant
    }

    public class SiteVisitChecklistItemViewModel
    {
        [Required]
        [StringLength(200)]
        [Display(Name = "Checklist item")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Status")]
        public SiteVisitChecklistStatus Status { get; set; } = SiteVisitChecklistStatus.NotReviewed;

        [StringLength(2000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }
    }
}

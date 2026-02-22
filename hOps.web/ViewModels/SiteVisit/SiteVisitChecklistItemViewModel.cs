using System.ComponentModel.DataAnnotations;

using hOps.web.Models.SiteVisit;

namespace hOps.web.ViewModels.SiteVisit
{
    public class SiteVisitChecklistItemViewModel
    {
        [StringLength(200)]
        [Display(Name = "Section")]
        public string? SectionName { get; set; }

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

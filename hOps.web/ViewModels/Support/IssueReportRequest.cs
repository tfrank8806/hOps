using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Support
{
    public class IssueReportRequest
    {
        [Required]
        [Display(Name = "Details")]
        [StringLength(4000, MinimumLength = 5)]
        public string Details { get; set; } = string.Empty;

        [Display(Name = "Page URL")]
        [StringLength(2048)]
        public string? PageUrl { get; set; }

        [Display(Name = "Screenshot")]
        public string? ScreenshotDataUrl { get; set; }
    }
}

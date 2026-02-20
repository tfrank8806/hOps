using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models.SiteVisit
{
    public class SiteVisitReportItem
    {
        public int Id { get; set; }

        public int SiteVisitReportId { get; set; }

        public SiteVisitReport? Report { get; set; }

        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public SiteVisitChecklistStatus Status { get; set; } = SiteVisitChecklistStatus.NotReviewed;

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }
}

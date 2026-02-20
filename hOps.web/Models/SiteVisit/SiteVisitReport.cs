using System;
using System.Collections.Generic;
using hOps.web.Models;

namespace hOps.web.Models.SiteVisit
{
    public class SiteVisitReport
    {
        public int Id { get; set; }

        public int? PropertyId { get; set; }

        public Property? Property { get; set; }

        public string PropertyName { get; set; } = string.Empty;

        public DateTime VisitDate { get; set; }

        public string? LeaderName { get; set; }

        public string? SummaryNotes { get; set; }

        public string? RecipientEmails { get; set; }

        public string? AssignedTo { get; set; }

        public SiteVisitProgressStatus ProgressStatus { get; set; } = SiteVisitProgressStatus.NotStarted;

        public string? CompletionNotes { get; set; }

        public string? CreatedByUserId { get; set; }

        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public ICollection<SiteVisitReportItem> Items { get; set; } = new List<SiteVisitReportItem>();
    }
}

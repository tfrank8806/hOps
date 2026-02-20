using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models.SiteVisit;

namespace hOps.web.ViewModels.SiteVisit
{
    public class SiteVisitLogViewModel
    {
        public string? CurrentPropertyName { get; set; }

        public IReadOnlyList<SiteVisitLogEntryViewModel> Entries { get; set; } = Array.Empty<SiteVisitLogEntryViewModel>();
    }

    public class SiteVisitLogEntryViewModel
    {
        public int Id { get; set; }

        public string PropertyName { get; set; } = string.Empty;

        public DateTime VisitDate { get; set; }

        public string? LeaderName { get; set; }

        public string? SummaryNotes { get; set; }

        public string? AssignedTo { get; set; }

        public SiteVisitProgressStatus ProgressStatus { get; set; }

        public string? CompletionNotes { get; set; }

        public string? RecipientEmails { get; set; }

        public string? CreatedByDisplayName { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public IReadOnlyList<SiteVisitChecklistItemViewModel> Items { get; set; } = Array.Empty<SiteVisitChecklistItemViewModel>();
    }

    public class SiteVisitLogUpdateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Assigned To")]
        [StringLength(200)]
        public string? AssignedTo { get; set; }

        [Display(Name = "Progress")]
        public SiteVisitProgressStatus ProgressStatus { get; set; } = SiteVisitProgressStatus.NotStarted;

        [Display(Name = "Completion Notes")]
        [StringLength(2000)]
        public string? CompletionNotes { get; set; }

        [Display(Name = "Send to")]
        public string? RecipientEmails { get; set; }

        public string SubmitAction { get; set; } = "save";
    }
}

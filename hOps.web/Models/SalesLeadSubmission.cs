using System;
using System.ComponentModel.DataAnnotations;

#nullable enable

namespace hOps.web.Models
{
    public class SalesLeadSubmission
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;

        public int SalesContactId { get; set; }
        public SalesContact SalesContact { get; set; } = default!;

        public string? SubmittedByUserId { get; set; }
        public ApplicationUser? SubmittedByUser { get; set; }

        [Required]
        [StringLength(200)]
        public string SubmittedByName { get; set; } = string.Empty;

        [StringLength(200)]
        public string GroupName { get; set; } = string.Empty;

        [StringLength(200)]
        public string ContactName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ContactPhone { get; set; }

        [StringLength(256)]
        public string ContactEmail { get; set; } = string.Empty;

        public int? NumberOfRooms { get; set; }
        public int? NumberOfGuests { get; set; }

        public decimal? BudgetMinimum { get; set; }
        public decimal? BudgetMaximum { get; set; }

        public DateTime? EventStartDate { get; set; }
        public DateTime? EventEndDate { get; set; }

        [StringLength(1000)]
        public string InquiryTypes { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? InquiryOtherDetails { get; set; }

        public string? AdditionalDetails { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

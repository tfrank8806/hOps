using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models.SiteVisit
{
    public class SiteVisitTemplate
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? CreatedByUserId { get; set; }

        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<SiteVisitTemplateItem> Items { get; set; } = new List<SiteVisitTemplateItem>();

        public ICollection<SiteVisitReport> Reports { get; set; } = new List<SiteVisitReport>();
    }
}

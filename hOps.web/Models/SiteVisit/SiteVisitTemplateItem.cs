using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models.SiteVisit
{
    public class SiteVisitTemplateItem
    {
        public int Id { get; set; }

        public int SiteVisitTemplateId { get; set; }

        public SiteVisitTemplate Template { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }
}

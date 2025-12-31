using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public enum BookmarkSection
    {
        Property = 0,
        User = 1,
        Team = 2
    }

    public class Bookmark
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(2048)]
        [Url]
        public string Url { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public BookmarkSection Section { get; set; }

        public bool ShowInQuickMenu { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        public ApplicationUser? CreatedBy { get; set; }

        public int? PropertyId { get; set; }

        public Property? Property { get; set; }

        public ICollection<BookmarkOrderPreference> OrderPreferences { get; set; } = new List<BookmarkOrderPreference>();
        public ICollection<BookmarkSectionAssignment> SectionAssignments { get; set; } = new List<BookmarkSectionAssignment>();
    }
}

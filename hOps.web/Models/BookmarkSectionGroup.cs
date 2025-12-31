using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class BookmarkSectionGroup
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public ICollection<BookmarkSectionAssignment> Assignments { get; set; } = new List<BookmarkSectionAssignment>();
    }

    public class BookmarkSectionAssignment
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required]
        public int BookmarkId { get; set; }

        public Bookmark? Bookmark { get; set; }

        [Required]
        public int SectionGroupId { get; set; }

        public BookmarkSectionGroup? SectionGroup { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class BookmarkOrderPreference
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required]
        public int BookmarkId { get; set; }

        public Bookmark? Bookmark { get; set; }

        public int SortOrder { get; set; }
    }
}

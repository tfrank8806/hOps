using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;

namespace hOps.web.ViewModels
{
    public class BookmarkFormViewModel
    {
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
        public BookmarkSection Section { get; set; } = BookmarkSection.User;
    }

    public class BookmarkEditViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public BookmarkSection Section { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(2048)]
        [Url]
        public string Url { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool ShowInQuickMenu { get; set; }
    }

    public class BookmarksIndexViewModel
    {
        public IReadOnlyCollection<Bookmark> PropertyBookmarks { get; set; } = new List<Bookmark>();

        public IReadOnlyCollection<Bookmark> TeamBookmarks { get; set; } = new List<Bookmark>();

        public IReadOnlyCollection<Bookmark> UserBookmarks { get; set; } = new List<Bookmark>();

        public BookmarkFormViewModel Form { get; set; } = new BookmarkFormViewModel();

        public bool CanManagePropertyBookmarks { get; set; }

        public bool HasCurrentProperty { get; set; }

        public string? CurrentPropertyName { get; set; }

        public string CurrentUserId { get; set; } = string.Empty;
    }

    public class BookmarkOrderUpdateRequest
    {
        public List<int> BookmarkIds { get; set; } = new();
    }

    public class BookmarkSectionCreateRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class BookmarkSectionLayoutRequest
    {
        public List<int> SectionIds { get; set; } = new();
        public List<BookmarkSectionAssignmentRequest> Sections { get; set; } = new();
        public List<int> UngroupedBookmarkIds { get; set; } = new();
    }

    public class BookmarkSectionAssignmentRequest
    {
        public int SectionId { get; set; }
        public List<int> BookmarkIds { get; set; } = new();
    }

    public class BookmarkAssignSectionRequest
    {
        public int BookmarkId { get; set; }
        public int? SectionId { get; set; }
    }
}

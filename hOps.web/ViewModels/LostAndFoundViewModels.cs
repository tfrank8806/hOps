using System.ComponentModel.DataAnnotations;
using hOps.web.Models;
using Microsoft.AspNetCore.Http;

namespace hOps.web.ViewModels
{
    public class LostFoundFilterInput
    {
        public string SortOrder { get; set; } = "newest";

        [DataType(DataType.Date)]
        public DateTime? DateFrom { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateTo { get; set; }

        public string? RoomNumber { get; set; }

        public string? GuestName { get; set; }

        public string? FoundBy { get; set; }

        public string? Creator { get; set; }

        public string? Keyword { get; set; }

        public int? PropertyId { get; set; }
    }

    public class LostFoundSubmissionViewModel
    {
        public LostFoundType Type { get; set; } = LostFoundType.Found;

        [DataType(DataType.Date)]
        public DateTime? DateFound { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateReportedLost { get; set; }

        public string? FoundBy { get; set; }

        public string? GuestName { get; set; }

        public string? GuestPhone { get; set; }

        public string? GuestAddress { get; set; }

        public string? Location { get; set; }

        public string? ItemFound { get; set; }

        public string? ItemLost { get; set; }

        public string? Notes { get; set; }

        public string? Stored { get; set; }

        public IFormFile? Photo { get; set; }
    }

    public class LostAndFoundIndexViewModel
    {
        public LostFoundFilterInput Filters { get; set; } = new();

        public LostFoundSubmissionViewModel Submission { get; set; } = new();

        public List<LostFoundEntry> FoundEntries { get; set; } = new();

        public List<LostFoundEntry> LostEntries { get; set; } = new();

        public List<Property> AccessibleProperties { get; set; } = new();

        public List<string> LocationOptions { get; set; } = new();

        public List<string> FoundByOptions { get; set; } = new();

        public List<string> CreatorOptions { get; set; } = new();
    }
}

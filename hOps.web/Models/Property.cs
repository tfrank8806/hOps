#nullable enable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class Property
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "MARSHA / INN Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Property Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }

        // Optional: Used for navigation in EF and View rendering
        public ICollection<UserPropertyAccess> UserAccesses { get; set; } = new List<UserPropertyAccess>();

        public ICollection<Room> Rooms { get; set; } = new List<Room>();

        public ICollection<WorkOrderProperty> WorkOrderLinks { get; set; } = new List<WorkOrderProperty>();
        public ICollection<CalendarEventProperty> CalendarEvents { get; set; } = new List<CalendarEventProperty>();
        public ICollection<LostFoundEntry> LostFoundEntries { get; set; } = new List<LostFoundEntry>();

        public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    }
}

#nullable enable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public ICollection<Department> Departments { get; set; } = new List<Department>();
        public ICollection<WorkOrderType> WorkOrderTypes { get; set; } = new List<WorkOrderType>();
        public ICollection<PhonebookType> PhonebookTypes { get; set; } = new List<PhonebookType>();
        public ICollection<CalendarCategory> CalendarCategories { get; set; } = new List<CalendarCategory>();

        [InverseProperty(nameof(PassOnLogProperty.Property))]
        public ICollection<PassOnLogProperty> PassOnLogLinks { get; set; } = new List<PassOnLogProperty>();

        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<DocumentProperty> DocumentLinks { get; set; } = new List<DocumentProperty>();
        public ICollection<DocumentFolderProperty> DocumentFolderLinks { get; set; } = new List<DocumentFolderProperty>();

        public ICollection<ManagerAnnouncement> ManagerAnnouncements { get; set; } = new List<ManagerAnnouncement>();

        public ICollection<BulletinPost> BulletinPosts { get; set; } = new List<BulletinPost>();

        public ICollection<PackageLogEntry> PackageLogEntries { get; set; } = new List<PackageLogEntry>();
        public ICollection<UserPropertyEmailSubscription> EmailSubscriptions { get; set; } = new List<UserPropertyEmailSubscription>();
    }
}

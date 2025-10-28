using Microsoft.AspNetCore.Identity;

#nullable enable
namespace hOps.web.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? MobilePhone { get; set; }
        public string? ProfilePhotoPath { get; set; }

        public bool MustChangePassword { get; set; } = false;

        public int? DefaultPropertyId { get; set; }
        public Property? DefaultProperty { get; set; }

        public ICollection<UserPropertyAccess>? UserPropertyAccesses { get; set; }

        public ICollection<CalendarEvent> CreatedCalendarEvents { get; set; } = new List<CalendarEvent>();

        public ICollection<Bookmark> CreatedBookmarks { get; set; } = new List<Bookmark>();

        public ICollection<DirectMessage> SentDirectMessages { get; set; } = new List<DirectMessage>();
        public ICollection<DirectMessage> ReceivedDirectMessages { get; set; } = new List<DirectMessage>();
        public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
    }
}

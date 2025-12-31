using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using hOps.web.Utilities;

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

        public bool EmailOnMessage { get; set; } = false;
        public bool EmailOnMention { get; set; } = false;
        public bool EmailOnWorkOrderDepartment { get; set; } = false;
        public bool EmailOnLogEntry { get; set; } = false;
        public bool EmailDailySummary { get; set; } = false;
        public bool EmailOnSchedulePosted { get; set; } = false;
        public DateTime? DailySummaryLastSentUtc { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }

        public int? DefaultPropertyId { get; set; }
        public Property? DefaultProperty { get; set; }

        public ICollection<UserPropertyAccess> UserPropertyAccesses { get; set; } = new List<UserPropertyAccess>();
        public ICollection<UserDepartmentSubscription> DepartmentEmailSubscriptions { get; set; } = new List<UserDepartmentSubscription>();

        public ICollection<CalendarEvent> CreatedCalendarEvents { get; set; } = new List<CalendarEvent>();

        public ICollection<Bookmark> CreatedBookmarks { get; set; } = new List<Bookmark>();
        public ICollection<BookmarkOrderPreference> BookmarkOrderPreferences { get; set; } = new List<BookmarkOrderPreference>();
        public ICollection<BookmarkSectionGroup> BookmarkSectionGroups { get; set; } = new List<BookmarkSectionGroup>();
        public ICollection<BookmarkSectionAssignment> BookmarkSectionAssignments { get; set; } = new List<BookmarkSectionAssignment>();

        public ICollection<DirectMessage> SentDirectMessages { get; set; } = new List<DirectMessage>();
        public ICollection<DirectMessage> ReceivedDirectMessages { get; set; } = new List<DirectMessage>();
        public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
        public ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();
        public ICollection<DocumentFolder> CreatedDocumentFolders { get; set; } = new List<DocumentFolder>();
        public ICollection<UserPropertyEmailSubscription> EmailPropertySubscriptions { get; set; } = new List<UserPropertyEmailSubscription>();
        public ICollection<UserToDoItem> ToDoItems { get; set; } = new List<UserToDoItem>();

        public string TimeZoneId { get; set; } = DefaultTimeZoneProvider.NormalizeForStorage(null);
    }
}

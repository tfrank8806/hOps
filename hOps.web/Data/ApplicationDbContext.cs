using hOps.web.Models;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Entities you referenced in controllers/views:
        public DbSet<Property> Properties { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<UserPropertyAccess> UserPropertyAccesses { get; set; }
        public DbSet<RoomType> RoomTypes { get; set; }
        public DbSet<UserAccessRequest> UserAccessRequests { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<WorkOrderType> WorkOrderTypes { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<WorkOrderAttachment> WorkOrderAttachments { get; set; }
        public DbSet<WorkOrderProperty> WorkOrderProperties { get; set; }
        public DbSet<PhonebookType> PhonebookTypes { get; set; }
        public DbSet<PhonebookContact> PhonebookContacts { get; set; }
        public DbSet<CalendarCategory> CalendarCategories { get; set; }
        public DbSet<RoomLayout> RoomLayouts { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<CalendarEventProperty> CalendarEventProperties { get; set; }
        public DbSet<LostFoundEntry> LostFoundEntries { get; set; }

        public DbSet<ManagerAnnouncement> ManagerAnnouncements { get; set; }

        public DbSet<BulletinPost> BulletinPosts { get; set; }
        public DbSet<ManagerAnnouncementAttachment> ManagerAnnouncementAttachments { get; set; }
        public DbSet<BulletinPostAttachment> BulletinPostAttachments { get; set; }

        public DbSet<PackageLogEntry> PackageLogEntries { get; set; }

        public DbSet<Bookmark> Bookmarks { get; set; }

        public DbSet<PassOnLog> PassOnLogs { get; set; }
        public DbSet<PassOnLogProperty> PassOnLogProperties { get; set; }
        public DbSet<PassOnLogComment> PassOnLogComments { get; set; }
        public DbSet<PassOnLogView> PassOnLogViews { get; set; }
        public DbSet<PassOnLogAttachment> PassOnLogAttachments { get; set; }
        public DbSet<DirectMessageConversation> DirectMessageConversations { get; set; }
        public DbSet<DirectMessage> DirectMessages { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<UserDepartmentSubscription> UserDepartmentSubscriptions { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentFolder> DocumentFolders { get; set; }
        public DbSet<DocumentProperty> DocumentProperties { get; set; }
        public DbSet<DocumentFolderProperty> DocumentFolderProperties { get; set; }
        public DbSet<UserPropertyEmailSubscription> UserPropertyEmailSubscriptions { get; set; }
        public DbSet<UserToDoItem> UserToDoItems { get; set; }
        public DbSet<ScheduleSettings> ScheduleSettings { get; set; }
        public DbSet<ScheduleShiftTemplate> ScheduleShiftTemplates { get; set; }
        public DbSet<ScheduleEmployee> ScheduleEmployees { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<ScheduleAssignment> ScheduleAssignments { get; set; }
        public DbSet<ScheduleTimeOffRequest> ScheduleTimeOffRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.DefaultProperty)
                .WithMany()
                .HasForeignKey(u => u.DefaultPropertyId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ApplicationUser>()
                .Property(u => u.TimeZoneId)
                .HasDefaultValue(DefaultTimeZoneProvider.DefaultTimeZoneId);

            // Composite key for UserPropertyAccess
            builder.Entity<UserPropertyAccess>()
                .HasKey(upa => new { upa.ApplicationUserId, upa.PropertyId });

            builder.Entity<UserPropertyAccess>()
                .HasOne(upa => upa.ApplicationUser)
                .WithMany(u => u.UserPropertyAccesses)
                .HasForeignKey(upa => upa.ApplicationUserId);

            builder.Entity<LostFoundEntry>()
                .HasOne(e => e.MatchedEntry)
                .WithMany()
                .HasForeignKey(e => e.MatchedEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserPropertyAccess>()
                .HasOne(upa => upa.Property)
                .WithMany(p => p.UserAccesses)
                .HasForeignKey(upa => upa.PropertyId);

            builder.Entity<UserPropertyEmailSubscription>()
                .HasKey(pes => new { pes.UserId, pes.PropertyId });

            builder.Entity<UserPropertyEmailSubscription>()
                .HasOne(pes => pes.User)
                .WithMany(u => u.EmailPropertySubscriptions)
                .HasForeignKey(pes => pes.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserPropertyEmailSubscription>()
                .HasOne(pes => pes.Property)
                .WithMany(p => p.EmailSubscriptions)
                .HasForeignKey(pes => pes.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RoomLayout>()
                .HasOne(rl => rl.Room)
                .WithMany(r => r.RoomLayouts)
                .HasForeignKey(rl => rl.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Document>()
                .HasOne(d => d.Folder)
                .WithMany(f => f.Documents)
                .HasForeignKey(d => d.FolderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DocumentFolder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.SubFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Schedule>()
                .HasOne(s => s.CreatedBy)
                .WithMany()
                .HasForeignKey(s => s.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Schedule>()
                .HasOne(s => s.UpdatedBy)
                .WithMany()
                .HasForeignKey(s => s.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Schedule>()
                .HasOne(s => s.PostedBy)
                .WithMany()
                .HasForeignKey(s => s.PostedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserPropertyEmailSubscription>()
                .Property(pes => pes.IncludeInLogAlerts)
                .HasDefaultValue(true);

            builder.Entity<UserPropertyEmailSubscription>()
                .Property(pes => pes.IncludeInDailySummary)
                .HasDefaultValue(true);

            builder.Entity<UserPropertyEmailSubscription>()
                .Property(pes => pes.IncludeInWorkOrderAlerts)
                .HasDefaultValue(true);

            builder.Entity<WorkOrder>()
                .HasMany(wo => wo.Properties)
                .WithOne(wp => wp.WorkOrder)
                .HasForeignKey(wp => wp.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkOrderProperty>()
                .HasOne(wp => wp.WorkOrder)
                .WithMany(wo => wo.Properties)
                .HasForeignKey(wp => wp.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkOrderType>()
                .ToTable("WorkOrderTypes");

            builder.Entity<WorkOrderProperty>()
                .HasOne(wp => wp.Property)
                .WithMany(p => p.WorkOrderLinks)
                .HasForeignKey(wp => wp.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkOrderAttachment>()
                .HasOne(wa => wa.WorkOrder)
                .WithMany(wo => wo.Attachments)
                .HasForeignKey(wa => wa.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<CalendarEventProperty>()
                .HasKey(cep => new { cep.CalendarEventId, cep.PropertyId });

            builder.Entity<CalendarEventProperty>()
                .HasOne(cep => cep.Property)
                .WithMany(p => p.CalendarEvents)
                .HasForeignKey(cep => cep.PropertyId);

            builder.Entity<CalendarEventProperty>()
                .HasOne(cep => cep.CalendarEvent)
                .WithMany(e => e.EventProperties)
                .HasForeignKey(cep => cep.CalendarEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CalendarEvent>()
                .HasMany(e => e.EventProperties)
                .WithOne(ep => ep.CalendarEvent)
                .HasForeignKey(ep => ep.CalendarEventId);

            builder.Entity<CalendarEvent>()
                .HasOne(e => e.CreatedBy)
                .WithMany(u => u.CreatedCalendarEvents)
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Similarly declare keys/relations for other models (if needed)
            // e.g., UserAccessRequest, RoomLayout etc.

            builder.Entity<PassOnLogProperty>()
                .HasKey(lp => new { lp.PassOnLogId, lp.PropertyId });

            builder.Entity<PassOnLogProperty>()
                .HasOne(lp => lp.PassOnLog)
                .WithMany(l => l.Properties)
                .HasForeignKey(lp => lp.PassOnLogId);

            builder.Entity<PassOnLogProperty>()
                .HasOne(lp => lp.Property)
                .WithMany(p => p.PassOnLogLinks)
                .HasForeignKey(lp => lp.PropertyId);

            builder.Entity<PassOnLogComment>()
                .HasOne(c => c.PassOnLog)
                .WithMany(l => l.Comments)
                .HasForeignKey(c => c.PassOnLogId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PassOnLogAttachment>()
                .HasOne(a => a.PassOnLog)
                .WithMany(l => l.Attachments)
                .HasForeignKey(a => a.PassOnLogId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PassOnLogView>()
                .HasKey(v => new { v.PassOnLogId, v.ViewerId });

            builder.Entity<PassOnLogView>()
                .HasOne(v => v.PassOnLog)
                .WithMany(l => l.Views)
                .HasForeignKey(v => v.PassOnLogId);

            builder.Entity<PassOnLogView>()
                .HasOne(v => v.Viewer)
                .WithMany()
                .HasForeignKey(v => v.ViewerId);

            builder.Entity<PassOnLogComment>()
                .HasOne(c => c.PassOnLog)
                .WithMany(l => l.Comments)
                .HasForeignKey(c => c.PassOnLogId);

            builder.Entity<PassOnLogComment>()
                .HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedById);

            builder.Entity<PassOnLogAttachment>()
                .HasOne(a => a.PassOnLog)
                .WithMany(l => l.Attachments)
                .HasForeignKey(a => a.PassOnLogId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Bookmark>()
                .HasOne(b => b.CreatedBy)
                .WithMany(u => u.CreatedBookmarks)
                .HasForeignKey(b => b.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Bookmark>()
                .HasOne(b => b.Property)
                .WithMany(p => p.Bookmarks)
                .HasForeignKey(b => b.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Bookmark>()
                .Property(b => b.Name)
                .HasMaxLength(200);

            builder.Entity<Bookmark>()
                .Property(b => b.Url)
                .HasMaxLength(2048);

            builder.Entity<ManagerAnnouncementAttachment>()
                .HasOne(a => a.ManagerAnnouncement)
                .WithMany(a => a.Attachments)
                .HasForeignKey(a => a.ManagerAnnouncementId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BulletinPostAttachment>()
                .HasOne(a => a.BulletinPost)
                .WithMany(p => p.Attachments)
                .HasForeignKey(a => a.BulletinPostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Bookmark>()
                .Property(b => b.Description)
                .HasMaxLength(500);

            builder.Entity<DirectMessageConversation>()
                .HasIndex(c => new { c.ParticipantAId, c.ParticipantBId })
                .IsUnique();

            builder.Entity<DirectMessageConversation>()
                .HasOne(c => c.ParticipantA)
                .WithMany()
                .HasForeignKey(c => c.ParticipantAId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DirectMessageConversation>()
                .HasOne(c => c.ParticipantB)
                .WithMany()
                .HasForeignKey(c => c.ParticipantBId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DirectMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DirectMessage>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentDirectMessages!)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DirectMessage>()
                .HasOne(m => m.Recipient)
                .WithMany(u => u.ReceivedDirectMessages!)
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserNotification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications!)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserNotification>()
                .HasOne(n => n.DirectMessage)
                .WithMany()
                .HasForeignKey(n => n.DirectMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserToDoItem>()
                .HasOne(t => t.User)
                .WithMany(u => u.ToDoItems)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserToDoItem>()
                .HasOne(t => t.WorkOrder)
                .WithMany(w => w.ToDoItems)
                .HasForeignKey(t => t.WorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<UserDepartmentSubscription>()
                .HasKey(s => new { s.UserId, s.DepartmentId });

            builder.Entity<UserDepartmentSubscription>()
                .HasOne(s => s.User)
                .WithMany(u => u.DepartmentEmailSubscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserDepartmentSubscription>()
                .HasOne(s => s.Department)
                .WithMany()
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Department>()
                .HasOne(d => d.Property)
                .WithMany(p => p.Departments)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkOrderType>()
                .HasOne(t => t.Property)
                .WithMany(p => p.WorkOrderTypes)
                .HasForeignKey(t => t.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PhonebookType>()
                .HasOne(t => t.Property)
                .WithMany(p => p.PhonebookTypes)
                .HasForeignKey(t => t.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CalendarCategory>()
                .HasOne(c => c.Property)
                .WithMany(p => p.CalendarCategories)
                .HasForeignKey(c => c.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Document>()
                .HasOne(d => d.UploadedBy)
                .WithMany(u => u.UploadedDocuments)
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Document>()
                .HasOne(d => d.Folder)
                .WithMany(f => f.Documents)
                .HasForeignKey(d => d.FolderId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Document>()
                .HasOne(d => d.Property)
                .WithMany(p => p.Documents)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DocumentProperty>()
                .HasKey(dp => new { dp.DocumentId, dp.PropertyId });

            builder.Entity<DocumentProperty>()
                .HasOne(dp => dp.Document)
                .WithMany(d => d.DocumentProperties)
                .HasForeignKey(dp => dp.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DocumentProperty>()
                .HasOne(dp => dp.Property)
                .WithMany(p => p.DocumentLinks)
                .HasForeignKey(dp => dp.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DocumentFolderProperty>()
                .HasKey(fp => new { fp.DocumentFolderId, fp.PropertyId });

            builder.Entity<DocumentFolderProperty>()
                .HasOne(fp => fp.DocumentFolder)
                .WithMany(f => f.FolderProperties)
                .HasForeignKey(fp => fp.DocumentFolderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DocumentFolderProperty>()
                .HasOne(fp => fp.Property)
                .WithMany(p => p.DocumentFolderLinks)
                .HasForeignKey(fp => fp.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DocumentFolder>()
                .HasOne(f => f.CreatedBy)
                .WithMany(u => u.CreatedDocumentFolders)
                .HasForeignKey(f => f.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DocumentFolder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.SubFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ScheduleSettings>()
                .HasOne(s => s.Property)
                .WithOne(p => p.ScheduleSettings)
                .HasForeignKey<ScheduleSettings>(s => s.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ScheduleSettings>()
                .HasOne(s => s.UpdatedByUser)
                .WithMany()
                .HasForeignKey(s => s.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ScheduleShiftTemplate>()
                .HasOne(t => t.Property)
                .WithMany(p => p.ScheduleShiftTemplates)
                .HasForeignKey(t => t.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ScheduleShiftTemplate>()
                .HasIndex(t => new { t.PropertyId, t.SortOrder });

            builder.Entity<ScheduleEmployee>()
                .HasOne(e => e.Property)
                .WithMany(p => p.ScheduleEmployees)
                .HasForeignKey(e => e.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ScheduleEmployee>()
                .HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ScheduleEmployee>()
                .HasIndex(e => new { e.PropertyId, e.ApplicationUserId })
                .IsUnique();

            builder.Entity<Schedule>()
                .HasOne(s => s.Property)
                .WithMany(p => p.Schedules)
                .HasForeignKey(s => s.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Schedule>()
                .HasOne(s => s.CreatedBy)
                .WithMany()
                .HasForeignKey(s => s.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Schedule>()
                .HasOne(s => s.UpdatedBy)
                .WithMany()
                .HasForeignKey(s => s.UpdatedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Schedule>()
                .HasOne(s => s.PostedBy)
                .WithMany()
                .HasForeignKey(s => s.PostedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ScheduleAssignment>()
                .HasOne(a => a.Schedule)
                .WithMany(s => s.Assignments)
                .HasForeignKey(a => a.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ScheduleAssignment>()
                .HasOne(a => a.Employee)
                .WithMany(e => e.Assignments)
                .HasForeignKey(a => a.ScheduleEmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ScheduleTimeOffRequest>()
                .HasOne(r => r.Property)
                .WithMany(p => p.ScheduleTimeOffRequests)
                .HasForeignKey(r => r.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ScheduleTimeOffRequest>()
                .HasOne(r => r.Employee)
                .WithMany(e => e.TimeOffRequests)
                .HasForeignKey(r => r.ScheduleEmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ScheduleTimeOffRequest>()
                .HasOne(r => r.SubmittedByUser)
                .WithMany()
                .HasForeignKey(r => r.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ScheduleTimeOffRequest>()
                .HasOne(r => r.DecisionByUser)
                .WithMany()
                .HasForeignKey(r => r.DecisionByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

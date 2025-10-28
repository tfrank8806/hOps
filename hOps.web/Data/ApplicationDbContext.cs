using hOps.web.Models;
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

        public DbSet<PackageLogEntry> PackageLogEntries { get; set; }

        public DbSet<Bookmark> Bookmarks { get; set; }

        public DbSet<PassOnLog> PassOnLogs { get; set; }
        public DbSet<PassOnLogProperty> PassOnLogProperties { get; set; }
        public DbSet<PassOnLogComment> PassOnLogComments { get; set; }
        public DbSet<PassOnLogView> PassOnLogViews { get; set; }
        public DbSet<DirectMessageConversation> DirectMessageConversations { get; set; }
        public DbSet<DirectMessage> DirectMessages { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.DefaultProperty)
                .WithMany()
                .HasForeignKey(u => u.DefaultPropertyId)
                .OnDelete(DeleteBehavior.SetNull);

            // Composite key for UserPropertyAccess
            builder.Entity<UserPropertyAccess>()
                .HasKey(upa => new { upa.ApplicationUserId, upa.PropertyId });

            builder.Entity<UserPropertyAccess>()
                .HasOne(upa => upa.ApplicationUser)
                .WithMany(u => u.UserPropertyAccesses)
                .HasForeignKey(upa => upa.ApplicationUserId);

            builder.Entity<UserPropertyAccess>()
                .HasOne(upa => upa.Property)
                .WithMany(p => p.UserAccesses)
                .HasForeignKey(upa => upa.PropertyId);

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
        }
    }
}

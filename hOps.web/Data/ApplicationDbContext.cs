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
        public DbSet<PhonebookType> PhonebookTypes { get; set; }
        public DbSet<CalendarCategory> CalendarCategories { get; set; }
        public DbSet<RoomLayout> RoomLayouts { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<CalendarEventProperty> CalendarEventProperties { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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

            builder.Entity<CalendarEventProperty>()
                .HasKey(cep => new { cep.CalendarEventId, cep.PropertyId });

            builder.Entity<CalendarEventProperty>()
                .HasOne(cep => cep.CalendarEvent)
                .WithMany(e => e.EventProperties)
                .HasForeignKey(cep => cep.CalendarEventId);

            builder.Entity<CalendarEventProperty>()
                .HasOne(cep => cep.Property)
                .WithMany(p => p.CalendarEvents)
                .HasForeignKey(cep => cep.PropertyId);

            builder.Entity<CalendarEvent>()
                .HasOne(e => e.CreatedBy)
                .WithMany(u => u.CreatedCalendarEvents)
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Similarly declare keys/relations for other models (if needed)
            // e.g., UserAccessRequest, RoomLayout etc.
        }
    }
}

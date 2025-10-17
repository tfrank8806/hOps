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

        public DbSet<PassOnLog> PassOnLogs { get; set; }
        public DbSet<PassOnLogProperty> PassOnLogProperties { get; set; }
        public DbSet<PassOnLogComment> PassOnLogComments { get; set; }
        public DbSet<PassOnLogView> PassOnLogViews { get; set; }

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
                .WithMany()
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
        }
    }
}

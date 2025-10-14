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

        public DbSet<Property> Properties { get; set; }
        public DbSet<UserAccessRequest> UserAccessRequests { get; set; }
        public DbSet<UserPropertyAccess> UserPropertyAccesses { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<WorkOrderType> WorkOrderTypes { get; set; }
        public DbSet<RoomType> RoomTypes { get; set; }
        public DbSet<PhonebookType> PhonebookTypes { get; set; }
        public DbSet<CalendarCategory> CalendarCategories { get; set; }



        // Future: add WorkOrders, Rooms, Departments, etc.

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Composite key: UserId + PropertyId
            builder.Entity<UserPropertyAccess>()
                .HasKey(upa => new { upa.ApplicationUserId, upa.PropertyId });

            // Optional: define navigation relationships if you need them
            builder.Entity<UserPropertyAccess>()
                .HasOne(upa => upa.ApplicationUser)
                .WithMany(u => u.UserPropertyAccesses)
                .HasForeignKey(upa => upa.ApplicationUserId);

            builder.Entity<UserPropertyAccess>()
                .HasOne(upa => upa.Property)
                .WithMany()
                .HasForeignKey(upa => upa.PropertyId);
        }
    }
}

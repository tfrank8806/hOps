using System;

#nullable enable
namespace hOps.web.Models
{
    public class UserDepartmentSubscription
    {
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

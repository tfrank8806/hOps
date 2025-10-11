using Microsoft.AspNetCore.Identity;

#nullable enable
namespace hOps.web.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? MobilePhone { get; set; }

        public ICollection<UserPropertyAccess>? PropertyAccesses { get; set; }
    }
}

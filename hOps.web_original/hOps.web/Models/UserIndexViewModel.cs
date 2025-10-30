using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace hOps.web.Models
{
    public class UserIndexViewModel
    {
        public List<ApplicationUser> Users { get; set; } = new();
        public IList<string> CurrentUserRoles { get; set; } = new List<string>();
        public Dictionary<string, string> UserRoles { get; set; } = new Dictionary<string, string>();
    }
}
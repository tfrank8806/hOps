using System;
using System.Collections.Generic;

namespace hOps.web.Models
{
    public class UserWithAccessViewModel
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public IList<string> Roles { get; set; } = new List<string>();
        public IList<int> PropertyIds { get; set; } = new List<int>();
        public bool CanDelete { get; set; }
        public bool CanResetPassword { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
    }
}

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
        // Optionally, you can include a list of property names or full Property objects
    }
}

using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class CreateUserViewModel
    {
        [Required]
        [EmailAddress]
        public string ? Email { get; set; }

        [Required]
        public string ? Password { get; set; }

        [Required]
        public string ? FirstName { get; set; }

        [Required]
        public string ? LastName { get; set; }

        public string? MobilePhone { get; set; }

        public string? Role { get; set; }

        public List<int>? PropertyIds { get; set; }
    }

    public class EditUserViewModel
    {
        [Required]
        public string ? Id { get; set; }

        [Required]
        [EmailAddress]
        public string ? Email { get; set; }

        [Required]
        public string ? FirstName { get; set; }

        [Required]
        public string ? LastName { get; set; }

        public string? MobilePhone { get; set; }

        public string? Role { get; set; }

        public List<int>? PropertyIds { get; set; }
    }
}

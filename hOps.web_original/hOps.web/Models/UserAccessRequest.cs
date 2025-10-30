using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class UserAccessRequest
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; } = "";

        [Required]
        public string LastName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        public string? MobilePhone { get; set; }

        [Required]
        public string PropertyCode { get; set; } = "";

        [Required]
        public string PasswordHash { get; set; } = "";

        public DateTime RequestedAt { get; set; }

        public bool IsApproved { get; set; } = false;
        public bool IsRejected { get; set; } = false;
    }
}

using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Api
{
    public class LoginRequest
    {
        [Required]
        [MaxLength(256)]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public UserSummaryViewModel User { get; set; } = new();
    }

    public class UserSummaryViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePhotoUrl { get; set; }
        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace hOps.web.ViewModels
{
    public class ProfileFormViewModel
    {
        [Display(Name = "Profile Photo")]
        public string? ProfilePhotoPath { get; set; }

        [Display(Name = "Upload New Photo")]
        public IFormFile? ProfilePhoto { get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }

    public class ChangePasswordFormViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmPassword { get; set; }
    }

    public class MyProfileViewModel
    {
        public ProfileFormViewModel Profile { get; set; } = new();
        public ChangePasswordFormViewModel ChangePassword { get; set; } = new();
    }
}

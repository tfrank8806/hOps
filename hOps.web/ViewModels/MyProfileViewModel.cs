using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

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

        [Display(Name = "Default Property")]
        public int? DefaultPropertyId { get; set; }

        [Display(Name = "Time Zone")]
        public string? TimeZoneId { get; set; }
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

    public class EmailPreferencesViewModel
    {
        [Display(Name = "Email me when I receive a direct message")]
        public bool EmailOnMessage { get; set; }

        [Display(Name = "Email me when I am mentioned")]
        public bool EmailOnMention { get; set; }

        [Display(Name = "Email me when new work orders are assigned to my selected departments")]
        public bool EmailOnWorkOrderDepartment { get; set; }

        [Display(Name = "Email me when new log entries are created")]
        public bool EmailOnLogEntry { get; set; }

        [Display(Name = "Send me a daily summary email")]
        public bool EmailDailySummary { get; set; }

        public List<int> SelectedDepartmentIds { get; set; } = new();
        public List<EmailPreferenceDepartmentOption> DepartmentOptions { get; set; } = new();
    }

    public class EmailPreferenceDepartmentOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public class MyProfileViewModel
    {
        public ProfileFormViewModel Profile { get; set; } = new();
        public ChangePasswordFormViewModel ChangePassword { get; set; } = new();
        public IEnumerable<SelectListItem> PropertyOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> TimeZoneOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public EmailPreferencesViewModel EmailPreferences { get; set; } = new();
    }
}

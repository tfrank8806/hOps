using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Admin
{
    public class AdminUsersPageViewModel
    {
        public List<Models.UserWithAccessViewModel> Users { get; set; } = new();

        public AdminCreateUserInputModel CreateUser { get; set; } = new();

        public List<AdminPropertyOptionViewModel> AvailableProperties { get; set; } = new();

        public List<string> AvailableRoles { get; set; } = new();

        public Dictionary<int, string> PropertyNameLookup { get; set; } = new();

        public bool CanManageRoles { get; set; }
    }

    public class AdminCreateUserInputModel
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? FirstName { get; set; }

        [Required]
        public string? LastName { get; set; }

        [Phone]
        public string? MobilePhone { get; set; }

        public List<int> PropertyIds { get; set; } = new();

        public List<string> SelectedRoles { get; set; } = new();
    }

    public class AdminPropertyOptionViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Code { get; set; }

        public string DisplayLabel => string.IsNullOrWhiteSpace(Code) ? Name : $"{Name} ({Code})";
    }
}

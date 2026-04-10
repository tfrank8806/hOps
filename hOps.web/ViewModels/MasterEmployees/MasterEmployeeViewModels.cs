using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.MasterEmployees
{
    public class MasterEmployeeRowViewModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;

        public string DisplayName => $"{FirstName} {LastName}".Trim();
    }

    public class MasterEmployeeListViewModel
    {
        public bool HasPropertySelected { get; set; }
        public string? PropertyName { get; set; }
        public List<MasterEmployeeRowViewModel> Employees { get; set; } = new();
        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class MasterEmployeeFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        [Required]
        [StringLength(150)]
        public string Position { get; set; } = string.Empty;

        public IEnumerable<SelectListItem> DepartmentOptions { get; set; } = Enumerable.Empty<SelectListItem>();
    }
}

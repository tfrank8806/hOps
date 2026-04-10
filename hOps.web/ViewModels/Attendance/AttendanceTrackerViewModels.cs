using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.Attendance
{
    public class AttendanceRecordFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int? MasterEmployeeId { get; set; }

        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime? AttendanceDate { get; set; }

        [Required]
        [Display(Name = "Attendance Type")]
        public AttendanceRecordType? AttendanceType { get; set; }

        public IEnumerable<SelectListItem> EmployeeOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> AttendanceTypeOptions { get; set; } = new List<SelectListItem>();
    }

    public class AttendanceRecordRowViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime AttendanceDate { get; set; }
        public AttendanceRecordType AttendanceType { get; set; }
        public string AttendanceTypeDisplay { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    public class AttendanceTrackerViewModel
    {
        public bool HasPropertySelected { get; set; }
        public string? PropertyName { get; set; }
        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public AttendanceRecordFormViewModel Form { get; set; } = new AttendanceRecordFormViewModel();
        public List<AttendanceRecordRowViewModel> Records { get; set; } = new();
    }
}

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

    public class AttendanceSummaryRowViewModel
    {
        public int MasterEmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int TardyCount { get; set; }
        public int LeftEarlyCount { get; set; }
        public int CallOffCount { get; set; }
        public int NoCallNoShowCount { get; set; }
        public int SickCount { get; set; }
        public int VacationCount { get; set; }
        public int PersonalCount { get; set; }
        public int BereavementCount { get; set; }
        public int TotalCount =>
            TardyCount + LeftEarlyCount + CallOffCount + NoCallNoShowCount +
            SickCount + VacationCount + PersonalCount + BereavementCount;
    }

    public class AttendanceDetailEntryViewModel
    {
        public int RecordId { get; set; }
        public int MasterEmployeeId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public AttendanceRecordType AttendanceType { get; set; }
        public string AttendanceTypeDisplay { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string CreatedByDisplay { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
    }

    public static class AttendanceHistoryFilterModes
    {
        public const string Month = "month";
        public const string Custom = "custom";
    }

    public class AttendanceHistoryFilterViewModel
    {
        public string Mode { get; set; } = AttendanceHistoryFilterModes.Month;
        public string MonthValue { get; set; } = string.Empty;
        public DateTime? CustomStartDate { get; set; }
        public DateTime? CustomEndDate { get; set; }
        public DateTime RangeStartDate { get; set; }
        public DateTime RangeEndDate { get; set; }
    }

    public class AttendanceTrackerViewModel
    {
        public bool HasPropertySelected { get; set; }
        public string? PropertyName { get; set; }
        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public AttendanceRecordFormViewModel Form { get; set; } = new AttendanceRecordFormViewModel();
        public AttendanceHistoryFilterViewModel Filter { get; set; } = new AttendanceHistoryFilterViewModel();
        public List<AttendanceSummaryRowViewModel> SummaryRows { get; set; } = new();
        public List<AttendanceDetailEntryViewModel> SelectedEmployeeDetails { get; set; } = new();
        public int? SelectedEmployeeId { get; set; }
        public string? SelectedEmployeeDisplayName { get; set; }
        public AttendanceMonthlyGridViewModel MonthlyGrid { get; set; } = new AttendanceMonthlyGridViewModel();
        public DateTime? SelectedEmployeeDetailDate { get; set; }
    }

    public class AttendanceMonthlyGridViewModel
    {
        public DateTime MonthStart { get; set; }
        public DateTime MonthEnd { get; set; }
        public string MonthValue { get; set; } = string.Empty;
        public string MonthDisplay => MonthStart.ToString("MMMM yyyy");
        public List<DateTime> Days { get; set; } = new();
        public List<AttendanceGridRowViewModel> Rows { get; set; } = new();
        public List<AttendanceGridDayTotalViewModel> DayTotals { get; set; } = new();
        public List<AttendanceCodeLegendItemViewModel> LegendItems { get; set; } = new();
        public int GrandTotal { get; set; }
        public bool HasEmployees => Rows.Count > 0;
    }

    public class AttendanceGridRowViewModel
    {
        public int MasterEmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public List<AttendanceGridCellViewModel> Cells { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class AttendanceGridCellViewModel
    {
        public DateTime Date { get; set; }
        public List<AttendanceGridEntryViewModel> Entries { get; set; } = new();
        public bool HasEntries => Entries.Count > 0;
        public string Tooltip { get; set; } = string.Empty;
    }

    public class AttendanceGridEntryViewModel
    {
        public int RecordId { get; set; }
        public AttendanceRecordType AttendanceType { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsExcused { get; set; }
    }

    public class AttendanceGridDayTotalViewModel
    {
        public DateTime Date { get; set; }
        public int TotalCount { get; set; }
    }

    public class AttendanceCodeLegendItemViewModel
    {
        public AttendanceRecordType AttendanceType { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsExcused { get; set; }
    }
}

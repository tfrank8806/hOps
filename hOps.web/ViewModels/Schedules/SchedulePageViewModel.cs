using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.Schedules
{
    public class SchedulePageViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;

        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }

        public bool HasSchedule { get; set; }
        public int? ScheduleId { get; set; }
        public ScheduleStatus? Status { get; set; }
        public bool CanManage { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public string? PostedByName { get; set; }

        public List<ScheduleDayColumnViewModel> DayColumns { get; set; } = new();
        public List<ScheduleEmployeeRowViewModel> EmployeeRows { get; set; } = new();
        public List<ScheduleShiftTemplateViewModel> ShiftTemplates { get; set; } = new();
        public List<ScheduleEmployeeOptionViewModel> EmployeeOptions { get; set; } = new();
        public ScheduleSortOption SortOption { get; set; } = ScheduleSortOption.EmployeeName;
        public List<SelectListItem> SortOptions { get; set; } = new();

        public ScheduleAssignmentFormViewModel AssignmentForm { get; set; } = new();
        public ScheduleEmployeeFormViewModel EmployeeForm { get; set; } = new();
        public TimeOffRequestFormViewModel TimeOffForm { get; set; } = new();

        public List<TimeOffRequestListItemViewModel> PendingRequests { get; set; } = new();
        public List<TimeOffRequestListItemViewModel> MyRequests { get; set; } = new();
        public List<ScheduleShiftAlertViewModel> ShiftAlerts { get; set; } = new();

        public ScheduleSettingsSummaryViewModel SettingsSummary { get; set; } = new();

        public bool ShowCreateDraftAction { get; set; }
        public bool ShowPostAction => HasSchedule && Status == ScheduleStatus.Draft && CanManage;
        public bool ShowUnlockAction => HasSchedule && Status == ScheduleStatus.Posted && CanManage;
        public bool WasPreviouslyPosted => HasSchedule && Status == ScheduleStatus.Draft && PostedAtUtc.HasValue;
        public bool ShowUnpostedMessage => !HasSchedule || Status == ScheduleStatus.Draft;
        public string? AlertMessage { get; set; }
    }

    public class ScheduleSettingsSummaryViewModel
    {
        public DayOfWeek StartDayOfWeek { get; set; } = DayOfWeek.Monday;
        public int ShiftTemplateCount { get; set; }
        public string? SettingsUrl { get; set; }
    }

    public class ScheduleDayColumnViewModel
    {
        public DateTime Date { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool IsToday { get; set; }
    }

    public class ScheduleEmployeeRowViewModel
    {
        public int ScheduleEmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public bool IsManual { get; set; }
        public string SourceLabel { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string PrimaryShiftName { get; set; } = string.Empty;
        public int? PrimaryShiftOrder { get; set; }
        public int SortOrder { get; set; }
        public Dictionary<DateTime, List<ScheduleAssignmentItemViewModel>> AssignmentsByDate { get; set; } = new();
        public Dictionary<DateTime, List<TimeOffBadgeViewModel>> TimeOffByDate { get; set; } = new();
    }

    public class ScheduleAssignmentItemViewModel
    {
        public int AssignmentId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan? ShiftStartTime { get; set; }
        public TimeSpan? ShiftEndTime { get; set; }
        public string? Notes { get; set; }
        public string? ColorHex { get; set; }
    }

    public class TimeOffBadgeViewModel
    {
        public string Label { get; set; } = string.Empty;
        public TimeOffRequestStatus Status { get; set; }
    }

    public class ScheduleShiftTemplateViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string ColorHex { get; set; } = "#3b82f6";
    }

    public class ScheduleEmployeeOptionViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string SourceLabel { get; set; } = "User";
        public bool IsManual { get; set; }
        public bool EmailAlertsEnabled { get; set; }
        public string? Email { get; set; }
    }

    public class ScheduleAssignmentFormViewModel
    {
        public int? AssignmentId { get; set; }

        [Required]
        public int ScheduleId { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int ScheduleEmployeeId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Shift Date")]
        public DateTime ShiftDate { get; set; }

        [Display(Name = "Shift Template")]
        public int? ShiftTemplateId { get; set; }

        [Required]
        [Display(Name = "Shift Name")]
        public string ShiftName { get; set; } = string.Empty;

        [Display(Name = "Start Time")]
        public string? ShiftStartTime { get; set; }

        [Display(Name = "End Time")]
        public string? ShiftEndTime { get; set; }

        [Display(Name = "Notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Apply to days")]
        public List<DayOfWeek> RepeatOnDays { get; set; } = new();

        public string? ShiftColorHex { get; set; }
    }

    public class ScheduleEmployeeFormViewModel
    {
        [Required]
        [Display(Name = "Employee name")]
        public string DisplayName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email (optional)")]
        public string? Email { get; set; }

        [Display(Name = "Send email alerts when schedules are posted")]
        public bool EmailAlertsEnabled { get; set; } = true;
    }

    public class TimeOffRequestFormViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "End date")]
        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(500)]
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public class TimeOffRequestListItemViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public TimeOffRequestStatus Status { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string SubmittedByName { get; set; } = string.Empty;
        public string? DecisionByName { get; set; }
        public DateTime? DecisionAtUtc { get; set; }
        public string? DecisionNotes { get; set; }
    }

    public class ScheduleShiftAlertViewModel
    {
        public int ShiftTemplateId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum ScheduleSortOption
    {
        EmployeeName,
        ShiftName,
        ShiftNumber,
        CustomOrder
    }

    public class ScheduleEmployeeOrderRequest
    {
        public int ScheduleId { get; set; }
        public string? WeekStart { get; set; }
        public List<int> EmployeeIds { get; set; } = new();
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.Settings
{
    public class ScheduleSetupViewModel
    {
        public int SelectedPropertyId { get; set; }
        public List<SelectListItem> PropertyOptions { get; set; } = new();

        [Display(Name = "Week starts on")]
        public DayOfWeek StartDayOfWeek { get; set; } = DayOfWeek.Monday;

        public List<ScheduleShiftTemplateInputModel> ShiftTemplates { get; set; } = new();
        public List<ScheduleManualEmployeeViewModel> ManualEmployees { get; set; } = new();
    }

    public class ScheduleShiftTemplateInputModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Template name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Shift name")]
        public string ShiftName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start time")]
        public string StartTime { get; set; } = "09:00";

        [Required]
        [Display(Name = "End time")]
        public string EndTime { get; set; } = "17:00";

        public int SortOrder { get; set; }

        [Display(Name = "Color")]
        public string ColorHex { get; set; } = "#3b82f6";

        [Display(Name = "Shift Alert")]
        public bool AlertIfMissing { get; set; }

        public bool IsDeleted { get; set; }
    }

    public class ScheduleManualEmployeeViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public int AssignmentCount { get; set; }
        public bool HasAssignments => AssignmentCount > 0;
    }
}

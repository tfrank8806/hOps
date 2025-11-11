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
    }

    public class ScheduleShiftTemplateInputModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Shift name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start time")]
        public string StartTime { get; set; } = "09:00";

        [Required]
        [Display(Name = "End time")]
        public string EndTime { get; set; } = "17:00";

        public int SortOrder { get; set; }
    }
}

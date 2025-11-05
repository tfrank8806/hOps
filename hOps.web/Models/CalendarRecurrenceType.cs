using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public enum CalendarRecurrenceType
    {
        [Display(Name = "One-time")]
        None = 0,

        [Display(Name = "Daily")]
        Daily = 1,

        [Display(Name = "Weekly")]
        Weekly = 2,

        [Display(Name = "Monthly")]
        Monthly = 3,

        [Display(Name = "Yearly")]
        Yearly = 4,

        [Display(Name = "Bi-weekly")]
        BiWeekly = 5
    }
}

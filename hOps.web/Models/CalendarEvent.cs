using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class CalendarEvent
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CalendarCategoryId { get; set; }
        public CalendarCategory? Category { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? StartTime { get; set; }

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? EndTime { get; set; }

        public CalendarRecurrenceType Recurrence { get; set; } = CalendarRecurrenceType.None;

        [DataType(DataType.MultilineText)]
        public string? Details { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;
        public ApplicationUser? CreatedBy { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<CalendarEventProperty> EventProperties { get; set; } = new List<CalendarEventProperty>();

        public ICollection<CalendarEventException> Exceptions { get; set; } = new List<CalendarEventException>();

        public bool NotifyAllDepartments { get; set; } = true;

        public int? TargetDepartmentId { get; set; }
        public Department? TargetDepartment { get; set; }

        public ICollection<CalendarEventReminder> Reminders { get; set; } = new List<CalendarEventReminder>();
    }
}

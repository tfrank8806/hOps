using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using hOps.web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels
{
    public class CalendarEventFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeSpan? StartTime { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [DataType(DataType.Time)]
        [Display(Name = "End Time")]
        public TimeSpan? EndTime { get; set; }

        [Display(Name = "Make Recurring")]
        public CalendarRecurrenceType Recurrence { get; set; } = CalendarRecurrenceType.None;

        [DataType(DataType.MultilineText)]
        [Display(Name = "Details")]
        public string? Details { get; set; }

        [Display(Name = "Properties")]
        public List<int> SelectedPropertyIds { get; set; } = new();

        [Display(Name = "Attachments")]
        [DataType(DataType.Upload)]
        public List<IFormFile>? Attachments { get; set; }

        public List<string> AttachmentsToRemove { get; set; } = new();

        public List<CalendarEventAttachmentViewModel> ExistingAttachments { get; set; } = new();

        [Display(Name = "Reminders")]
        public List<int> SelectedReminderOffsets { get; set; } = new();

        [Display(Name = "All Departments")]
        public bool NotifyAllDepartments { get; set; } = true;

        [Display(Name = "Department")]
        public int? TargetDepartmentId { get; set; }
    }

    public class CalendarEventDisplayViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryColor { get; set; } = "#198754";
        public string CategoryTextColor { get; set; } = "#ffffff";
        public DateTime StartDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan? EndTime { get; set; }
        public CalendarRecurrenceType Recurrence { get; set; }
        public string? Details { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public List<string> PropertyNames { get; set; } = new();
        public List<CalendarEventAttachmentViewModel> Attachments { get; set; } = new();
        public HashSet<DateTime> DeletedOccurrenceDates { get; set; } = new();
        public bool IsContinuationSegment { get; set; }

        public DateTime StartDateTime => StartDate.Date.Add(StartTime ?? TimeSpan.Zero);

        public DateTime EndDateTime
        {
            get
            {
                if (EndTime.HasValue)
                {
                    return EndDate.Date.Add(EndTime.Value);
                }

                // End of the day when no time is specified
                return EndDate.Date.AddDays(1).AddTicks(-1);
            }
        }

        public bool IsMultiDay => EndDate.Date > StartDate.Date;

        public int SpanDays => (EndDate.Date - StartDate.Date).Days + 1;

        public string DateDisplay
        {
            get
            {
                if (StartDate.Date == EndDate.Date)
                {
                    return StartDate.ToString("D");
                }

                return $"{StartDate:MMM d, yyyy} - {EndDate:MMM d, yyyy}";
            }
        }

        public string? TimeDisplay
        {
            get
            {
                if (StartTime.HasValue && EndTime.HasValue)
                {
                    return $"{DateTime.Today.Add(StartTime.Value):t} - {DateTime.Today.Add(EndTime.Value):t}";
                }

                if (StartTime.HasValue)
                {
                    return DateTime.Today.Add(StartTime.Value).ToString("t");
                }

                if (EndTime.HasValue)
                {
                    return $"Until {DateTime.Today.Add(EndTime.Value):t}";
                }

                return null;
            }
        }

        public string RecurrenceDisplay => Recurrence == CalendarRecurrenceType.None
            ? "One-time event"
            : $"Repeats {GetRecurrenceLabel(Recurrence)}";

        private static string GetRecurrenceLabel(CalendarRecurrenceType recurrence)
        {
            var member = typeof(CalendarRecurrenceType)
                .GetMember(recurrence.ToString())
                .FirstOrDefault();

            var display = member?.GetCustomAttribute<DisplayAttribute>();
            if (!string.IsNullOrWhiteSpace(display?.Name))
            {
                return display.Name;
            }

            return recurrence.ToString();
        }

        public string PropertiesDisplay => PropertyNames.Count > 0
            ? string.Join(", ", PropertyNames)
            : "No property selected";

        public string CreatedByDisplay => string.IsNullOrWhiteSpace(CreatedByName)
            ? "Unknown"
            : CreatedByName;

        public CalendarEventDisplayViewModel CloneWithDates(DateTime startDate, DateTime endDate)
        {
            return new CalendarEventDisplayViewModel
            {
                Id = Id,
                Title = Title,
                CategoryName = CategoryName,
                CategoryColor = CategoryColor,
                CategoryTextColor = CategoryTextColor,
                StartDate = startDate,
                StartTime = StartTime,
                EndDate = endDate,
                EndTime = EndTime,
                Recurrence = Recurrence,
                Details = Details,
                CreatedByName = CreatedByName,
                CreatedAtUtc = CreatedAtUtc,
                PropertyNames = new List<string>(PropertyNames),
                Attachments = Attachments.Select(a => a.Clone()).ToList(),
                DeletedOccurrenceDates = new HashSet<DateTime>(DeletedOccurrenceDates)
            };
        }

        public CalendarEventDisplayViewModel CreateContinuationSegment()
        {
            var clone = CloneWithDates(StartDate, EndDate);
            clone.IsContinuationSegment = true;
            return clone;
        }
    }

    public class CalendarDayViewModel
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public List<CalendarEventDisplayViewModel> Events { get; set; } = new();
        public List<CalendarEventDisplayViewModel> ContinuingEvents { get; set; } = new();
        public bool HasEvents => Events.Count > 0 || ContinuingEvents.Count > 0;
    }

    public class CalendarViewModel
    {
        public DateTime CurrentMonth { get; set; }
        public DateTime PreviousMonth { get; set; }
        public DateTime NextMonth { get; set; }
        public List<CalendarDayViewModel> Days { get; set; } = new();
        public List<CalendarEventDisplayViewModel> UpcomingEvents { get; set; } = new();
        public CalendarEventFormViewModel Form { get; set; } = new();
        public IEnumerable<SelectListItem> CategoryOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public List<Property> AccessibleProperties { get; set; } = new();
        public bool ShowPropertySelection { get; set; }
        public IEnumerable<SelectListItem> ReminderOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DepartmentOptions { get; set; } = Enumerable.Empty<SelectListItem>();
    }

    public class CalendarEventManageViewModel
    {
        public string Heading { get; set; } = "Edit Event";
        public CalendarEventFormViewModel Form { get; set; } = new();
        public IEnumerable<SelectListItem> CategoryOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public List<Property> AccessibleProperties { get; set; } = new();
        public bool ShowPropertySelection { get; set; }
        public IEnumerable<SelectListItem> ReminderOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DepartmentOptions { get; set; } = Enumerable.Empty<SelectListItem>();
    }

    public class CalendarEventAttachmentViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;

        public CalendarEventAttachmentViewModel Clone()
        {
            return new CalendarEventAttachmentViewModel
            {
                FileName = FileName,
                DisplayName = DisplayName,
                DownloadUrl = DownloadUrl
            };
        }
    }
}

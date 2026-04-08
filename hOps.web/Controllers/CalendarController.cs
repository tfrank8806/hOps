using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace hOps.web.Controllers
{
    public class CalendarController : BaseController
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CalendarController> _logger;

        public CalendarController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment, ILogger<CalendarController> logger)
            : base(context, userManager)
        {
            _environment = environment;
            _logger = logger;
        }

        private const long MaxAttachmentSizeBytes = 5 * 1024 * 1024; // 5 MB
        private const int MaxAttachmentCount = 5;
        private static readonly HashSet<string> AllowedAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/jpg",
            "image/heic",
            "image/heif",
            "image/gif",
            "image/webp",
            "image/bmp",
            "application/pdf"
        };

        [HttpGet]
        public async Task<IActionResult> Index(int? month, int? year)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var targetMonth = ResolveTargetMonth(month, year, DateTime.Today);
            var viewModel = await BuildViewModelAsync(user, targetMonth);

            if (TempData["SuccessMessage"] is string message)
            {
                ViewBag.SuccessMessage = message;
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Form")] CalendarEventFormViewModel form, int? month, int? year)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(user.Id);
            var currentPropertyId = (ViewBag.CurrentProperty as Property)?.Id;
            if (currentPropertyId.HasValue)
            {
                accessibleProperties = accessibleProperties
                    .Where(p => p.Id == currentPropertyId.Value)
                    .ToList();
            }

            var propertyIds = accessibleProperties.Select(p => p.Id).ToList();
            var propertySelectionKey = "Form.SelectedPropertyIds";

            if (!propertyIds.Any())
            {
                ModelState.AddModelError(string.Empty, "You do not have access to any properties to associate with this event.");
            }

            form.SelectedPropertyIds = (form.SelectedPropertyIds ?? new List<int>())
                .Where(id => propertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!form.SelectedPropertyIds.Any() && propertyIds.Count == 1)
            {
                form.SelectedPropertyIds = new List<int> { propertyIds[0] };
            }

            if (!form.SelectedPropertyIds.Any())
            {
                ModelState.AddModelError(propertySelectionKey, "Select at least one property.");
            }
            else if (form.SelectedPropertyIds.Any(id => !propertyIds.Contains(id)))
            {
                ModelState.AddModelError(propertySelectionKey, "You can only select properties you have access to.");
            }

            form.StartDate = form.StartDate == default ? DateTime.Today : form.StartDate.Date;
            form.EndDate = form.EndDate == default ? form.StartDate : form.EndDate.Date;

            if (form.EndDate < form.StartDate)
            {
                ModelState.AddModelError("Form.EndDate", "End date cannot be before start date.");
            }

            if (form.StartDate == form.EndDate && form.StartTime.HasValue && form.EndTime.HasValue && form.EndTime < form.StartTime)
            {
                ModelState.AddModelError("Form.EndTime", "End time cannot be before start time when the event occurs on a single day.");
            }

            var categoryExists = await FilterCalendarCategoriesByProperties(propertyIds)
                .AnyAsync(c => c.Id == form.CategoryId);
            if (!categoryExists)
            {
                ModelState.AddModelError("Form.CategoryId", "Select a valid category.");
            }

            ValidateAttachments(form.Attachments, 0, "Form.Attachments");

            form.SelectedReminderOffsets ??= new List<int>();
            var reminderSelections = NormalizeReminderSelections(form.SelectedReminderOffsets);

            var validDepartmentIds = await GetDepartmentIdsForPropertiesAsync(form.SelectedPropertyIds);
            if (form.NotifyAllDepartments)
            {
                form.TargetDepartmentId = null;
            }
            else
            {
                if (!form.TargetDepartmentId.HasValue)
                {
                    ModelState.AddModelError("Form.TargetDepartmentId", "Select a department for this event.");
                }
                else if (!validDepartmentIds.Contains(form.TargetDepartmentId.Value))
                {
                    ModelState.AddModelError("Form.TargetDepartmentId", "Choose a department associated with the selected properties.");
                }
            }

            var targetMonth = ResolveTargetMonth(month, year, form.StartDate);

            if (!ModelState.IsValid)
            {
                var viewModel = await BuildViewModelAsync(user, targetMonth, form);
                return View("Index", viewModel);
            }

            var normalizedStartDate = NormalizeCalendarDate(form.StartDate);
            var normalizedEndDate = NormalizeCalendarDate(form.EndDate);

            var calendarEvent = new CalendarEvent
            {
                CalendarCategoryId = form.CategoryId,
                Title = form.Title.Trim(),
                StartDate = normalizedStartDate,
                StartTime = form.StartTime,
                EndDate = normalizedEndDate,
                EndTime = form.EndTime,
                Recurrence = form.Recurrence,
                Details = string.IsNullOrWhiteSpace(form.Details) ? null : form.Details.Trim(),
                CreatedById = user.Id,
                CreatedAtUtc = DateTime.UtcNow,
                NotifyAllDepartments = form.NotifyAllDepartments,
                TargetDepartmentId = form.NotifyAllDepartments ? null : form.TargetDepartmentId
            };

            foreach (var propertyId in form.SelectedPropertyIds.Distinct())
            {
                calendarEvent.EventProperties.Add(new CalendarEventProperty
                {
                    PropertyId = propertyId
                });
            }

            _context.CalendarEvents.Add(calendarEvent);
            await _context.SaveChangesAsync();

            try
            {
                await SaveAttachmentsAsync(calendarEvent.Id, form.Attachments);
            }
            catch
            {
                TempData["WarningMessage"] = "Event saved, but one or more attachments could not be uploaded.";
            }

            await SyncEventRemindersAsync(calendarEvent, reminderSelections);

            TempData["SuccessMessage"] = "Event created successfully.";

            return RedirectToAction(nameof(Index), new { month = calendarEvent.StartDate.Month, year = calendarEvent.StartDate.Year });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(user.Id);
            if (!accessibleProperties.Any())
            {
                return NotFound();
            }

            var calendarEvent = await _context.CalendarEvents
                .Include(e => e.EventProperties)
                .Include(e => e.Reminders)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (calendarEvent == null)
            {
                return NotFound();
            }

            var accessiblePropertyIds = accessibleProperties
                .Select(p => p.Id)
                .ToHashSet();

            var selectedPropertyIds = calendarEvent.EventProperties
                .Where(ep => accessiblePropertyIds.Contains(ep.PropertyId))
                .Select(ep => ep.PropertyId)
                .Distinct()
                .ToList();

            if (!selectedPropertyIds.Any())
            {
                return NotFound();
            }

            var propertyIds = accessibleProperties.Select(p => p.Id).ToList();
            var categoryOptions = await GetCalendarCategoryOptionsAsync(propertyIds);

            var form = new CalendarEventFormViewModel
            {
                Id = calendarEvent.Id,
                CategoryId = calendarEvent.CalendarCategoryId,
                Title = calendarEvent.Title,
                StartDate = calendarEvent.StartDate,
                StartTime = calendarEvent.StartTime,
                EndDate = calendarEvent.EndDate,
                EndTime = calendarEvent.EndTime,
                Recurrence = calendarEvent.Recurrence,
                Details = calendarEvent.Details,
                SelectedPropertyIds = selectedPropertyIds,
                ExistingAttachments = LoadAttachmentViewModels(calendarEvent.Id),
                NotifyAllDepartments = calendarEvent.NotifyAllDepartments,
                TargetDepartmentId = calendarEvent.TargetDepartmentId,
                SelectedReminderOffsets = calendarEvent.Reminders
                    .Select(r => (int)r.ReminderType)
                    .Distinct()
                    .ToList()
            };

            var departmentOptions = await BuildDepartmentOptionsAsync(accessibleProperties.Select(p => p.Id), form.TargetDepartmentId);
            var reminderOptions = BuildReminderOptions(form.SelectedReminderOffsets);

            var viewModel = new CalendarEventManageViewModel
            {
                Heading = "Edit Event",
                Form = form,
                CategoryOptions = categoryOptions,
                AccessibleProperties = accessibleProperties,
                ShowPropertySelection = accessibleProperties.Count > 1,
                DepartmentOptions = departmentOptions,
                ReminderOptions = reminderOptions
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] CalendarEventFormViewModel form)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (form.Id.HasValue && form.Id.Value != id)
            {
                return BadRequest();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(user.Id);
            if (!accessibleProperties.Any())
            {
                return NotFound();
            }

            var accessiblePropertyIds = accessibleProperties
                .Select(p => p.Id)
                .ToHashSet();

            var calendarEvent = await _context.CalendarEvents
                .Include(e => e.EventProperties)
                .Include(e => e.Exceptions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (calendarEvent == null)
            {
                return NotFound();
            }

            var existingAccessiblePropertyIds = calendarEvent.EventProperties
                .Where(ep => accessiblePropertyIds.Contains(ep.PropertyId))
                .Select(ep => ep.PropertyId)
                .Distinct()
                .ToList();

            if (!existingAccessiblePropertyIds.Any())
            {
                return NotFound();
            }

            var existingAttachments = LoadAttachmentViewModels(calendarEvent.Id);
            form.ExistingAttachments = existingAttachments;

            var attachmentsToRemove = form.AttachmentsToRemove?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => Path.GetFileName(name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var remainingAttachmentCount = existingAttachments
                .Count(a => !attachmentsToRemove.Contains(a.FileName, StringComparer.OrdinalIgnoreCase));

            form.Id = id;
            form.SelectedPropertyIds = form.SelectedPropertyIds?.Distinct().ToList() ?? new List<int>();

            var propertySelectionKey = "Form.SelectedPropertyIds";

            if (!form.SelectedPropertyIds.Any() && accessibleProperties.Count == 1)
            {
                form.SelectedPropertyIds = new List<int> { accessibleProperties[0].Id };
            }

            if (!form.SelectedPropertyIds.Any())
            {
                ModelState.AddModelError(propertySelectionKey, "Select at least one property.");
            }
            else if (form.SelectedPropertyIds.Any(selectedId => !accessiblePropertyIds.Contains(selectedId)))
            {
                ModelState.AddModelError(propertySelectionKey, "You can only select properties you have access to.");
            }

            form.StartDate = form.StartDate == default ? calendarEvent.StartDate.Date : form.StartDate.Date;
            form.EndDate = form.EndDate == default ? form.StartDate : form.EndDate.Date;

            if (form.EndDate < form.StartDate)
            {
                ModelState.AddModelError("Form.EndDate", "End date cannot be before start date.");
            }

            if (form.StartDate == form.EndDate && form.StartTime.HasValue && form.EndTime.HasValue && form.EndTime < form.StartTime)
            {
                ModelState.AddModelError("Form.EndTime", "End time cannot be before start time when the event occurs on a single day.");
            }

            var categoryExists = await FilterCalendarCategoriesByProperties(accessiblePropertyIds)
                .AnyAsync(c => c.Id == form.CategoryId);
            if (!categoryExists)
            {
                ModelState.AddModelError("Form.CategoryId", "Select a valid category.");
            }

            ValidateAttachments(form.Attachments, remainingAttachmentCount, "Form.Attachments");

            form.SelectedReminderOffsets ??= new List<int>();
            var reminderSelections = NormalizeReminderSelections(form.SelectedReminderOffsets);

            var validDepartmentIds = await GetDepartmentIdsForPropertiesAsync(form.SelectedPropertyIds);
            if (form.NotifyAllDepartments)
            {
                form.TargetDepartmentId = null;
            }
            else
            {
                if (!form.TargetDepartmentId.HasValue)
                {
                    ModelState.AddModelError("Form.TargetDepartmentId", "Select a department for this event.");
                }
                else if (!validDepartmentIds.Contains(form.TargetDepartmentId.Value))
                {
                    ModelState.AddModelError("Form.TargetDepartmentId", "Choose a department associated with the selected properties.");
                }
            }

            if (!ModelState.IsValid)
            {
                var categoryOptions = await GetCalendarCategoryOptionsAsync(accessibleProperties.Select(p => p.Id));
                var departmentOptions = await BuildDepartmentOptionsAsync(accessibleProperties.Select(p => p.Id), form.TargetDepartmentId);
                var reminderOptions = BuildReminderOptions(form.SelectedReminderOffsets);

                form.ExistingAttachments = existingAttachments;

                var viewModel = new CalendarEventManageViewModel
                {
                    Heading = "Edit Event",
                    Form = form,
                    CategoryOptions = categoryOptions,
                    AccessibleProperties = accessibleProperties,
                    ShowPropertySelection = accessibleProperties.Count > 1,
                    DepartmentOptions = departmentOptions,
                    ReminderOptions = reminderOptions
                };

                return View(viewModel);
            }

            var normalizedStartDate = NormalizeCalendarDate(form.StartDate);
            var normalizedEndDate = NormalizeCalendarDate(form.EndDate);

            calendarEvent.CalendarCategoryId = form.CategoryId;
            calendarEvent.Title = form.Title.Trim();
            calendarEvent.StartDate = normalizedStartDate;
            calendarEvent.StartTime = form.StartTime;
            calendarEvent.EndDate = normalizedEndDate;
            calendarEvent.EndTime = form.EndTime;
            calendarEvent.Recurrence = form.Recurrence;
            calendarEvent.Details = string.IsNullOrWhiteSpace(form.Details) ? null : form.Details.Trim();
            calendarEvent.NotifyAllDepartments = form.NotifyAllDepartments;
            calendarEvent.TargetDepartmentId = form.NotifyAllDepartments ? null : form.TargetDepartmentId;

            var newPropertyIds = form.SelectedPropertyIds;

            var currentAccessiblePropertyIds = calendarEvent.EventProperties
                .Where(ep => accessiblePropertyIds.Contains(ep.PropertyId))
                .Select(ep => ep.PropertyId)
                .Distinct()
                .ToList();

            var propertiesToRemove = currentAccessiblePropertyIds
                .Except(newPropertyIds)
                .ToList();

            var propertiesToAdd = newPropertyIds
                .Except(currentAccessiblePropertyIds)
                .ToList();

            if (propertiesToRemove.Any())
            {
                var eventPropertiesToRemove = calendarEvent.EventProperties
                    .Where(ep => propertiesToRemove.Contains(ep.PropertyId))
                    .ToList();

                _context.CalendarEventProperties.RemoveRange(eventPropertiesToRemove);
            }

            foreach (var propertyId in propertiesToAdd)
            {
                calendarEvent.EventProperties.Add(new CalendarEventProperty
                {
                    PropertyId = propertyId,
                    CalendarEventId = calendarEvent.Id
                });
            }

            await _context.SaveChangesAsync();

            if (attachmentsToRemove.Any())
            {
                DeleteAttachments(calendarEvent.Id, attachmentsToRemove);
            }

            try
            {
                await SaveAttachmentsAsync(calendarEvent.Id, form.Attachments);
            }
            catch
            {
                TempData["WarningMessage"] = "Event updated, but one or more attachments could not be uploaded.";
            }

            await SyncEventRemindersAsync(calendarEvent, reminderSelections);

            TempData["SuccessMessage"] = "Event updated successfully.";

            return RedirectToAction(nameof(Index), new { month = calendarEvent.StartDate.Month, year = calendarEvent.StartDate.Year });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CalendarEventDeleteScope scope, DateTime? occurrenceDate, int? month, int? year)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(user.Id);
            if (!accessibleProperties.Any())
            {
                return NotFound();
            }

            var accessiblePropertyIds = accessibleProperties
                .Select(p => p.Id)
                .ToHashSet();

            var calendarEvent = await _context.CalendarEvents
                .Include(e => e.EventProperties)
                .Include(e => e.Exceptions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (calendarEvent == null)
            {
                return NotFound();
            }

            if (!calendarEvent.EventProperties.Any(ep => accessiblePropertyIds.Contains(ep.PropertyId)))
            {
                return NotFound();
            }

            if (calendarEvent.Recurrence == CalendarRecurrenceType.None)
            {
                scope = CalendarEventDeleteScope.EntireSeries;
            }

            var fallbackDate = occurrenceDate?.Date ?? calendarEvent.StartDate;
            var redirectTarget = ResolveTargetMonth(month, year, fallbackDate);

            if (scope == CalendarEventDeleteScope.SingleOccurrence)
            {
                if (!occurrenceDate.HasValue)
                {
                    return BadRequest();
                }

                var normalizedOccurrence = NormalizeCalendarDate(occurrenceDate.Value);
                var alreadyDeleted = calendarEvent.Exceptions
                    .Any(ex => ex.Type == CalendarEventExceptionType.DeletedOccurrence
                               && ex.OccurrenceDate.Date == normalizedOccurrence.Date);

                if (!alreadyDeleted)
                {
                    var exception = new CalendarEventException
                    {
                        CalendarEventId = calendarEvent.Id,
                        OccurrenceDate = normalizedOccurrence,
                        Type = CalendarEventExceptionType.DeletedOccurrence
                    };
                    _context.CalendarEventExceptions.Add(exception);
                    var occurrenceStartUtc = CalendarEventTimeHelper.CombineDateAndTime(normalizedOccurrence, calendarEvent.StartTime);
                    await RemoveOccurrenceRemindersAsync(calendarEvent.Id, occurrenceStartUtc);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Event occurrence deleted.";
                return RedirectToAction(nameof(Index), new { month = redirectTarget.Month, year = redirectTarget.Year });
            }

            _context.CalendarEvents.Remove(calendarEvent);
            await _context.SaveChangesAsync();
            DeleteAllAttachments(calendarEvent.Id);

            TempData["SuccessMessage"] = "Event deleted successfully.";
            return RedirectToAction(nameof(Index), new { month = redirectTarget.Month, year = redirectTarget.Year });
        }

        private async Task<CalendarViewModel> BuildViewModelAsync(ApplicationUser user, DateTime targetMonth, CalendarEventFormViewModel? formOverride = null)
        {
            var accessibleProperties = await GetAccessiblePropertiesAsync(user.Id);
            var currentPropertyId = (ViewBag.CurrentProperty as Property)?.Id;
            var visibleProperties = currentPropertyId.HasValue
                ? accessibleProperties.Where(p => p.Id == currentPropertyId.Value).ToList()
                : accessibleProperties.ToList();

            var propertyIds = visibleProperties.Select(p => p.Id).ToList();
            var form = formOverride ?? new CalendarEventFormViewModel();
            form.SelectedPropertyIds ??= new List<int>();

            var departmentOptions = await BuildDepartmentOptionsAsync(propertyIds, form.TargetDepartmentId);

            form.SelectedReminderOffsets ??= new List<int>();
            var reminderOptions = BuildReminderOptions(form.SelectedReminderOffsets);
            var categoryOptions = await GetCalendarCategoryOptionsAsync(propertyIds);

            if (formOverride == null)
            {
                var today = DateTime.Today;
                form.StartDate = today;
                form.EndDate = today;

                if (categoryOptions.Count > 0)
                {
                    form.CategoryId = int.Parse(categoryOptions.First().Value);
                }
            }

            if (!form.SelectedPropertyIds.Any())
            {
                if (currentPropertyId.HasValue && visibleProperties.Any(p => p.Id == currentPropertyId.Value))
                {
                    form.SelectedPropertyIds = new List<int> { currentPropertyId.Value };
                }
                else if (visibleProperties.Count == 1)
                {
                    form.SelectedPropertyIds = new List<int> { visibleProperties[0].Id };
                }
            }

            form.SelectedPropertyIds = form.SelectedPropertyIds
                .Where(id => propertyIds.Contains(id))
                .Distinct()
                .ToList();

            IQueryable<CalendarEvent> eventsQuery = _context.CalendarEvents
                .Include(e => e.Category)
                .Include(e => e.EventProperties).ThenInclude(ep => ep.Property)
                .Include(e => e.CreatedBy)
                .Include(e => e.Exceptions);

            if (propertyIds.Any())
            {
                eventsQuery = eventsQuery.Where(e => e.EventProperties.Any(ep => propertyIds.Contains(ep.PropertyId)));
            }
            else
            {
                eventsQuery = eventsQuery.Where(e => false);
            }

            var events = await eventsQuery.ToListAsync();

            var gridStart = GetCalendarGridStart(targetMonth);
            var gridEnd = GetCalendarGridEnd(targetMonth);

            var days = BuildCalendarDays(gridStart, gridEnd, targetMonth);
            var dayLookup = days.ToDictionary(d => d.Date.Date);

            var displayEvents = events
                .Select(e => MapToDisplayModel(e, LoadAttachmentViewModels(e.Id)))
                .ToList();

            var occurrencesInView = CalendarRecurrenceHelper
                .ExpandOccurrences(displayEvents, gridStart, gridEnd)
                .ToList();

            foreach (var occurrence in occurrencesInView)
            {
                var startDate = occurrence.StartDate.Date;
                var endDate = occurrence.EndDate.Date;
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (!dayLookup.TryGetValue(date, out var day))
                    {
                        continue;
                    }

                    if (date == startDate)
                    {
                        day.Events.Add(occurrence);
                    }
                    else
                    {
                        day.ContinuingEvents.Add(occurrence.CreateContinuationSegment());
                    }
                }
            }

            foreach (var day in days)
            {
                day.Events = day.Events
                    .OrderBy(e => e.StartDateTime)
                    .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                day.ContinuingEvents = day.ContinuingEvents
                    .OrderBy(e => e.EndDateTime)
                    .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var now = DateTime.Now;
            var upcoming = CalendarRecurrenceHelper
                .ExpandOccurrences(displayEvents, now.Date, now.Date.AddMonths(6))
                .Where(e => e.EndDateTime >= now)
                .OrderBy(e => e.StartDateTime)
                .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            return new CalendarViewModel
            {
                CurrentMonth = targetMonth,
                PreviousMonth = targetMonth.AddMonths(-1),
                NextMonth = targetMonth.AddMonths(1),
                Days = days,
                UpcomingEvents = upcoming,
                Form = form,
                CategoryOptions = categoryOptions,
                AccessibleProperties = visibleProperties,
                ShowPropertySelection = visibleProperties.Count > 1,
                ReminderOptions = reminderOptions,
                DepartmentOptions = departmentOptions
            };
        }

        private async Task<List<Property>> GetAccessiblePropertiesAsync(string userId)
        {
            var properties = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Select(upa => upa.Property)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var currentPropertyId = (ViewBag.CurrentProperty as Property)?.Id;
            if (currentPropertyId.HasValue)
            {
                properties = properties
                    .Where(p => p.Id == currentPropertyId.Value)
                    .ToList();
            }

            return properties;
        }

        private IQueryable<CalendarCategory> FilterCalendarCategoriesByProperties(IEnumerable<int>? propertyIds)
        {
            var allowedIds = propertyIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var query = _context.CalendarCategories.AsQueryable();
            if (allowedIds.Any())
            {
                query = query.Where(c => !c.PropertyId.HasValue || allowedIds.Contains(c.PropertyId.Value));
            }
            else
            {
                query = query.Where(c => c.PropertyId == null);
            }

            return query;
        }

        private async Task<List<SelectListItem>> GetCalendarCategoryOptionsAsync(IEnumerable<int>? propertyIds = null)
        {
            return await FilterCalendarCategoriesByProperties(propertyIds)
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> BuildDepartmentOptionsAsync(IEnumerable<int> propertyIds, int? selectedDepartmentId)
        {
            if (!propertyIds.Any())
            {
                return new List<SelectListItem>();
            }

            var departments = await _context.Departments
                .Where(d => d.PropertyId.HasValue && propertyIds.Contains(d.PropertyId.Value))
                .Include(d => d.Property)
                .OrderBy(d => d.Property!.Name)
                .ThenBy(d => d.Name)
                .ToListAsync();

            return departments
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Property == null ? d.Name ?? "Department" : $"{d.Name} ({d.Property.Name})",
                    Selected = selectedDepartmentId.HasValue && d.Id == selectedDepartmentId.Value
                })
                .ToList();
        }

        private static List<SelectListItem> BuildReminderOptions(IEnumerable<int> selectedValues)
        {
            var selected = new HashSet<int>(selectedValues ?? Array.Empty<int>());
            return Enum.GetValues(typeof(CalendarEventReminderOffset))
                .Cast<CalendarEventReminderOffset>()
                .Select(option => new SelectListItem
                {
                    Value = ((int)option).ToString(),
                    Text = GetReminderOptionLabel(option),
                    Selected = selected.Contains((int)option)
                })
                .ToList();
        }

        private static CalendarEventDisplayViewModel MapToDisplayModel(CalendarEvent calendarEvent, List<CalendarEventAttachmentViewModel> attachments)
        {
            var deletedOccurrenceDates = calendarEvent.Exceptions?
                .Where(ex => ex.Type == CalendarEventExceptionType.DeletedOccurrence)
                .Select(ex => NormalizeCalendarDate(ex.OccurrenceDate).Date)
                .ToHashSet() ?? new HashSet<DateTime>();

            var createdByName = string.Empty;
            if (calendarEvent.CreatedBy != null)
            {
                var parts = new[] { calendarEvent.CreatedBy.FirstName, calendarEvent.CreatedBy.LastName }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                createdByName = string.Join(" ", parts);

                if (string.IsNullOrWhiteSpace(createdByName))
                {
                    createdByName = calendarEvent.CreatedBy.Email ?? calendarEvent.CreatedBy.UserName ?? string.Empty;
                }
            }

            var color = calendarEvent.Category?.Color;
            if (string.IsNullOrWhiteSpace(color))
            {
                color = "#6c757d";
            }

            return new CalendarEventDisplayViewModel
            {
                Id = calendarEvent.Id,
                Title = calendarEvent.Title,
                CategoryName = calendarEvent.Category?.Name ?? "Uncategorized",
                CategoryColor = color,
                CategoryTextColor = GetTextColorForBackground(color),
                StartDate = calendarEvent.StartDate,
                StartTime = calendarEvent.StartTime,
                EndDate = calendarEvent.EndDate,
                EndTime = calendarEvent.EndTime,
                Recurrence = calendarEvent.Recurrence,
                Details = calendarEvent.Details,
                CreatedByName = createdByName,
                CreatedAtUtc = calendarEvent.CreatedAtUtc,
                PropertyNames = calendarEvent.EventProperties
                    .Select(ep => ep.Property.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList(),
                Attachments = attachments.Select(a => a.Clone()).ToList(),
                DeletedOccurrenceDates = deletedOccurrenceDates
            };
        }

        private void ValidateAttachments(IEnumerable<IFormFile>? files, int existingCount, string modelStateKey)
        {
            if (files == null)
            {
                return;
            }

            var validFiles = files
                .Where(file => file != null && file.Length > 0)
                .ToList();

            if (!validFiles.Any())
            {
                return;
            }

            if (existingCount + validFiles.Count > MaxAttachmentCount)
            {
                ModelState.AddModelError(modelStateKey, $"Please upload no more than {MaxAttachmentCount} files in total.");
            }

            foreach (var file in validFiles)
            {
                if (file.Length > MaxAttachmentSizeBytes)
                {
                    ModelState.AddModelError(modelStateKey, $"'{file.FileName}' exceeds the {MaxAttachmentSizeBytes / (1024 * 1024)} MB limit.");
                }

                var contentType = file.ContentType ?? string.Empty;
                var extension = Path.GetExtension(file.FileName) ?? string.Empty;
                if (!IsAllowedAttachment(contentType, extension))
                {
                    ModelState.AddModelError(modelStateKey, $"'{file.FileName}' is not an allowed file type. Upload images or PDF files.");
                }
            }
        }

        private async Task SaveAttachmentsAsync(int eventId, IEnumerable<IFormFile>? files)
        {
            if (files == null)
            {
                return;
            }

            var filesToSave = files
                .Where(file => file != null && file.Length > 0)
                .ToList();

            if (!filesToSave.Any())
            {
                return;
            }

            var uploadRoot = GetAttachmentDirectoryPath(eventId);
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in filesToSave)
            {
                var storedFileName = BuildStoredFileName(file.FileName);
                var physicalPath = Path.Combine(uploadRoot, storedFileName);

                try
                {
                    await using var stream = System.IO.File.Create(physicalPath);
                    await file.CopyToAsync(stream);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Calendar: failed to save attachment '{File}' for event {EventId}", file.FileName, eventId);
                    throw;
                }
            }
        }

        private void DeleteAttachments(int eventId, IEnumerable<string> storedFileNames)
        {
            if (storedFileNames == null)
            {
                return;
            }

            var directory = GetAttachmentDirectoryPath(eventId);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var fileName in storedFileNames)
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var safeName = Path.GetFileName(fileName);
                var physicalPath = Path.Combine(directory, safeName);
                if (!System.IO.File.Exists(physicalPath))
                {
                    continue;
                }

                try
                {
                    System.IO.File.Delete(physicalPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Calendar: failed to delete attachment '{File}' for event {EventId}", safeName.Replace("\r", "").Replace("\n", ""), eventId);
                }
            }
        }

        private void DeleteAllAttachments(int eventId)
        {
            var directory = GetAttachmentDirectoryPath(eventId);
            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Calendar: failed to delete attachment directory for event {EventId}", eventId);
            }
        }

        private List<CalendarEventAttachmentViewModel> LoadAttachmentViewModels(int eventId)
        {
            var directory = GetAttachmentDirectoryPath(eventId);
            if (!Directory.Exists(directory))
            {
                return new List<CalendarEventAttachmentViewModel>();
            }

            return Directory
                .EnumerateFiles(directory)
                .Select(path => Path.GetFileName(path))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new CalendarEventAttachmentViewModel
                {
                    FileName = name,
                    DisplayName = GetOriginalFileName(name),
                    DownloadUrl = GetAttachmentRelativePath(eventId, name)
                })
                .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string GetAttachmentDirectoryPath(int eventId)
        {
            return Path.Combine(_environment.WebRootPath, "uploads", "calendar", eventId.ToString());
        }

        private static string GetAttachmentRelativePath(int eventId, string storedFileName)
        {
            return $"/uploads/calendar/{eventId}/{storedFileName}".Replace("\\", "/");
        }

        private static string BuildStoredFileName(string originalFileName)
        {
            var baseName = Path.GetFileNameWithoutExtension(originalFileName);
            var extension = Path.GetExtension(originalFileName);
            var sanitizedBase = SanitizeBaseFileName(baseName);
            var encoded = Uri.EscapeDataString(sanitizedBase);
            return $"{Guid.NewGuid():N}__{encoded}{extension}";
        }

        private static string GetOriginalFileName(string storedFileName)
        {
            var name = Path.GetFileName(storedFileName);
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Attachment";
            }

            var parts = name.Split(new[] { "__" }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var extension = Path.GetExtension(name);
                var encodedBase = Path.GetFileNameWithoutExtension(parts[1]);
                var decodedBase = Uri.UnescapeDataString(encodedBase);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    return decodedBase + extension;
                }

                return decodedBase;
            }

            return name;
        }

        private static string SanitizeBaseFileName(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return "attachment";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = new string(baseName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            cleaned = Regex.Replace(cleaned, "_{2,}", "_").Trim('_');
            if (cleaned.Length > 80)
            {
                cleaned = cleaned[..80];
            }
            return string.IsNullOrWhiteSpace(cleaned) ? "attachment" : cleaned;
        }

        private static bool IsAllowedAttachment(string contentType, string extension)
        {
            if (!string.IsNullOrWhiteSpace(contentType) && AllowedAttachmentContentTypes.Contains(contentType))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTextColorForBackground(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                return "#ffffff";
            }

            var color = hexColor.TrimStart('#');
            if (color.Length == 3)
            {
                color = string.Concat(color.Select(c => $"{c}{c}"));
            }

            if (color.Length != 6 || !int.TryParse(color, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            {
                return "#ffffff";
            }

            var r = (rgb >> 16) & 0xFF;
            var g = (rgb >> 8) & 0xFF;
            var b = rgb & 0xFF;

            var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
            return luminance > 0.6 ? "#212529" : "#ffffff";
        }

        private static List<CalendarDayViewModel> BuildCalendarDays(DateTime gridStart, DateTime gridEnd, DateTime targetMonth)
        {
            var days = new List<CalendarDayViewModel>();
            for (var date = gridStart.Date; date <= gridEnd.Date; date = date.AddDays(1))
            {
                days.Add(new CalendarDayViewModel
                {
                    Date = date,
                    IsCurrentMonth = date.Month == targetMonth.Month && date.Year == targetMonth.Year
                });
            }

            return days;
        }

        private static DateTime GetCalendarGridStart(DateTime month)
        {
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var offset = (int)firstDay.DayOfWeek;
            return firstDay.AddDays(-offset);
        }

        private static DateTime GetCalendarGridEnd(DateTime month)
        {
            var lastDay = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            var offset = 6 - (int)lastDay.DayOfWeek;
            return lastDay.AddDays(offset);
        }

        private static DateTime ResolveTargetMonth(int? month, int? year, DateTime fallback)
        {
            if (month.HasValue && year.HasValue && month.Value >= 1 && month.Value <= 12)
            {
                return new DateTime(year.Value, month.Value, 1);
            }

            if (fallback == default)
            {
                fallback = DateTime.Today;
            }

            return new DateTime(fallback.Year, fallback.Month, 1);
        }

        private static List<CalendarEventReminderOffset> NormalizeReminderSelections(IEnumerable<int>? values)
        {
            var offsets = new HashSet<CalendarEventReminderOffset>();
            if (values != null)
            {
                foreach (var raw in values)
                {
                    if (Enum.IsDefined(typeof(CalendarEventReminderOffset), raw))
                    {
                        offsets.Add((CalendarEventReminderOffset)raw);
                    }
                }
            }

            return offsets.ToList();
        }

        private static string GetReminderOptionLabel(CalendarEventReminderOffset offset)
        {
            return offset switch
            {
                CalendarEventReminderOffset.DayOfEvent => "Day of event",
                CalendarEventReminderOffset.OneDayBefore => "1 day before",
                CalendarEventReminderOffset.TwoDaysBefore => "2 days before",
                CalendarEventReminderOffset.OneWeekBefore => "1 week before",
                _ => "Reminder"
            };
        }

        private static TimeSpan GetReminderOffsetSpan(CalendarEventReminderOffset offset)
        {
            return offset switch
            {
                CalendarEventReminderOffset.DayOfEvent => TimeSpan.Zero,
                CalendarEventReminderOffset.OneDayBefore => TimeSpan.FromDays(1),
                CalendarEventReminderOffset.TwoDaysBefore => TimeSpan.FromDays(2),
                CalendarEventReminderOffset.OneWeekBefore => TimeSpan.FromDays(7),
                _ => TimeSpan.Zero
            };
        }

        private async Task<HashSet<int>> GetDepartmentIdsForPropertiesAsync(IEnumerable<int> propertyIds)
        {
            var ids = (propertyIds ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            if (!ids.Any())
            {
                return new HashSet<int>();
            }

            var departmentIds = await _context.Departments
                .Where(d => d.PropertyId.HasValue && ids.Contains(d.PropertyId.Value))
                .Select(d => d.Id)
                .ToListAsync();

            return departmentIds.ToHashSet();
        }

        private static CalendarEventDisplayViewModel BuildReminderDisplayModel(CalendarEvent calendarEvent)
        {
            return new CalendarEventDisplayViewModel
            {
                Id = calendarEvent.Id,
                Title = calendarEvent.Title,
                StartDate = calendarEvent.StartDate,
                StartTime = calendarEvent.StartTime,
                EndDate = calendarEvent.EndDate,
                EndTime = calendarEvent.EndTime,
                Recurrence = calendarEvent.Recurrence,
                Details = calendarEvent.Details,
                CreatedAtUtc = calendarEvent.CreatedAtUtc,
                PropertyNames = new List<string>(),
                Attachments = new List<CalendarEventAttachmentViewModel>(),
                DeletedOccurrenceDates = calendarEvent.Exceptions?
                    .Where(ex => ex.Type == CalendarEventExceptionType.DeletedOccurrence)
                    .Select(ex => NormalizeCalendarDate(ex.OccurrenceDate).Date)
                    .ToHashSet() ?? new HashSet<DateTime>()
            };
        }

        private async Task SyncEventRemindersAsync(CalendarEvent calendarEvent, IReadOnlyCollection<CalendarEventReminderOffset> reminderOffsets)
        {
            var existing = await _context.CalendarEventReminders
                .Where(r => r.CalendarEventId == calendarEvent.Id)
                .ToListAsync();
            if (existing.Any())
            {
                _context.CalendarEventReminders.RemoveRange(existing);
            }

            if (reminderOffsets == null || reminderOffsets.Count == 0)
            {
                await _context.SaveChangesAsync();
                return;
            }

            var displayModel = BuildReminderDisplayModel(calendarEvent);
            var occurrences = CalendarRecurrenceHelper
                .ExpandOccurrences(new[] { displayModel }, calendarEvent.StartDate, calendarEvent.EndDate)
                .ToList();

            var newEntries = new List<CalendarEventReminder>();
            foreach (var occurrence in occurrences)
            {
                var occurrenceStartUtc = CalendarEventTimeHelper.CombineDateAndTime(occurrence.StartDate, occurrence.StartTime);
                foreach (var offset in reminderOffsets)
                {
                    var scheduledUtc = occurrenceStartUtc - GetReminderOffsetSpan(offset);
                    newEntries.Add(new CalendarEventReminder
                    {
                        CalendarEventId = calendarEvent.Id,
                        ReminderType = offset,
                        OccurrenceStartUtc = occurrenceStartUtc,
                        ScheduledSendUtc = scheduledUtc,
                        IsSent = false
                    });
                }
            }

            if (newEntries.Count > 0)
            {
                _context.CalendarEventReminders.AddRange(newEntries);
            }

            await _context.SaveChangesAsync();
        }

        private async Task RemoveOccurrenceRemindersAsync(int calendarEventId, DateTime occurrenceStartUtc)
        {
            var reminders = await _context.CalendarEventReminders
                .Where(r => r.CalendarEventId == calendarEventId && r.OccurrenceStartUtc == occurrenceStartUtc)
                .ToListAsync();
            if (reminders.Count == 0)
            {
                return;
            }

            _context.CalendarEventReminders.RemoveRange(reminders);
        }

        private static DateTime NormalizeCalendarDate(DateTime value)
        {
            var dateOnly = value.Date;
            return DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
        }
    }
}

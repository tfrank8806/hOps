using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace hOps.web.Controllers
{
    public class CalendarController : BaseController
    {
        public CalendarController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

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

            form.SelectedPropertyIds = form.SelectedPropertyIds
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

            var categoryExists = await _context.CalendarCategories.AnyAsync(c => c.Id == form.CategoryId);
            if (!categoryExists)
            {
                ModelState.AddModelError("Form.CategoryId", "Select a valid category.");
            }

            var targetMonth = ResolveTargetMonth(month, year, form.StartDate);

            if (!ModelState.IsValid)
            {
                var viewModel = await BuildViewModelAsync(user, targetMonth, form);
                return View("Index", viewModel);
            }

            var calendarEvent = new CalendarEvent
            {
                CalendarCategoryId = form.CategoryId,
                Title = form.Title.Trim(),
                StartDate = form.StartDate.Date,
                StartTime = form.StartTime,
                EndDate = form.EndDate.Date,
                EndTime = form.EndTime,
                Recurrence = form.Recurrence,
                Details = string.IsNullOrWhiteSpace(form.Details) ? null : form.Details.Trim(),
                CreatedById = user.Id,
                CreatedAtUtc = DateTime.UtcNow
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

            var categoryOptions = await GetCalendarCategoryOptionsAsync();

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
                SelectedPropertyIds = selectedPropertyIds
            };

            var viewModel = new CalendarEventManageViewModel
            {
                Heading = "Edit Event",
                Form = form,
                CategoryOptions = categoryOptions,
                AccessibleProperties = accessibleProperties,
                ShowPropertySelection = accessibleProperties.Count > 1
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

            var categoryExists = await _context.CalendarCategories.AnyAsync(c => c.Id == form.CategoryId);
            if (!categoryExists)
            {
                ModelState.AddModelError("Form.CategoryId", "Select a valid category.");
            }

            if (!ModelState.IsValid)
            {
                var categoryOptions = await GetCalendarCategoryOptionsAsync();

                var viewModel = new CalendarEventManageViewModel
                {
                    Heading = "Edit Event",
                    Form = form,
                    CategoryOptions = categoryOptions,
                    AccessibleProperties = accessibleProperties,
                    ShowPropertySelection = accessibleProperties.Count > 1
                };

                return View(viewModel);
            }

            calendarEvent.CalendarCategoryId = form.CategoryId;
            calendarEvent.Title = form.Title.Trim();
            calendarEvent.StartDate = form.StartDate.Date;
            calendarEvent.StartTime = form.StartTime;
            calendarEvent.EndDate = form.EndDate.Date;
            calendarEvent.EndTime = form.EndTime;
            calendarEvent.Recurrence = form.Recurrence;
            calendarEvent.Details = string.IsNullOrWhiteSpace(form.Details) ? null : form.Details.Trim();

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

            TempData["SuccessMessage"] = "Event updated successfully.";

            return RedirectToAction(nameof(Index), new { month = calendarEvent.StartDate.Month, year = calendarEvent.StartDate.Year });
        }

        private async Task<CalendarViewModel> BuildViewModelAsync(ApplicationUser user, DateTime targetMonth, CalendarEventFormViewModel? formOverride = null)
        {
            var accessibleProperties = await GetAccessiblePropertiesAsync(user.Id);
            var currentPropertyId = (ViewBag.CurrentProperty as Property)?.Id;
            var visibleProperties = currentPropertyId.HasValue
                ? accessibleProperties.Where(p => p.Id == currentPropertyId.Value).ToList()
                : accessibleProperties.ToList();

            var categoryOptions = await GetCalendarCategoryOptionsAsync();

            var form = formOverride ?? new CalendarEventFormViewModel();

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

            var propertyIds = visibleProperties.Select(p => p.Id).ToList();
            form.SelectedPropertyIds = form.SelectedPropertyIds
                .Where(id => propertyIds.Contains(id))
                .Distinct()
                .ToList();

            IQueryable<CalendarEvent> eventsQuery = _context.CalendarEvents
                .Include(e => e.Category)
                .Include(e => e.EventProperties).ThenInclude(ep => ep.Property)
                .Include(e => e.CreatedBy);

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

            var displayEvents = events.Select(MapToDisplayModel).ToList();
            var relevantEvents = displayEvents
                .Where(e => e.StartDate.Date <= gridEnd.Date && e.EndDate.Date >= gridStart.Date)
                .ToList();

            foreach (var calendarEvent in relevantEvents)
            {
                for (var date = calendarEvent.StartDate.Date; date <= calendarEvent.EndDate.Date; date = date.AddDays(1))
                {
                    if (date >= gridStart.Date && date <= gridEnd.Date && dayLookup.TryGetValue(date, out var day))
                    {
                        day.Events.Add(calendarEvent);
                    }
                }
            }

            foreach (var day in days)
            {
                day.Events = day.Events
                    .OrderBy(e => e.StartDateTime)
                    .ThenBy(e => e.Title)
                    .ToList();
            }

            var now = DateTime.Now;
            var upcoming = displayEvents
                .Where(e => e.EndDateTime >= now)
                .OrderBy(e => e.StartDateTime)
                .ThenBy(e => e.Title)
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
                ShowPropertySelection = visibleProperties.Count > 1
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

        private async Task<List<SelectListItem>> GetCalendarCategoryOptionsAsync()
        {
            return await _context.CalendarCategories
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();
        }

        private static CalendarEventDisplayViewModel MapToDisplayModel(CalendarEvent calendarEvent)
        {
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
                    .ToList()
            };
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
    }
}

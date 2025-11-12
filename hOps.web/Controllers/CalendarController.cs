using hOps.web.Data;
using hOps.web.Models;
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

            ValidateAttachments(form.Attachments, 0, "Form.Attachments");

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

            try
            {
                await SaveAttachmentsAsync(calendarEvent.Id, form.Attachments);
            }
            catch
            {
                TempData["WarningMessage"] = "Event saved, but one or more attachments could not be uploaded.";
            }

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
                SelectedPropertyIds = selectedPropertyIds,
                ExistingAttachments = LoadAttachmentViewModels(calendarEvent.Id)
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

            var categoryExists = await _context.CalendarCategories.AnyAsync(c => c.Id == form.CategoryId);
            if (!categoryExists)
            {
                ModelState.AddModelError("Form.CategoryId", "Select a valid category.");
            }

            ValidateAttachments(form.Attachments, remainingAttachmentCount, "Form.Attachments");

            if (!ModelState.IsValid)
            {
                var categoryOptions = await GetCalendarCategoryOptionsAsync();

                form.ExistingAttachments = existingAttachments;

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

            var displayEvents = events
                .Select(e => MapToDisplayModel(e, LoadAttachmentViewModels(e.Id)))
                .ToList();

            var occurrencesInView = ExpandOccurrences(displayEvents, gridStart, gridEnd).ToList();

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
            var upcoming = ExpandOccurrences(displayEvents, now.Date, now.Date.AddMonths(6))
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

        private static CalendarEventDisplayViewModel MapToDisplayModel(CalendarEvent calendarEvent, List<CalendarEventAttachmentViewModel> attachments)
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
                Attachments = attachments.Select(a => a.Clone()).ToList()
            };
        }

        private static IEnumerable<CalendarEventDisplayViewModel> ExpandOccurrences(IEnumerable<CalendarEventDisplayViewModel> events, DateTime rangeStart, DateTime rangeEnd)
        {
            foreach (var calendarEvent in events)
            {
                foreach (var occurrence in EnumerateOccurrences(calendarEvent, rangeStart, rangeEnd))
                {
                    yield return occurrence;
                }
            }
        }

        private static IEnumerable<CalendarEventDisplayViewModel> EnumerateOccurrences(CalendarEventDisplayViewModel calendarEvent, DateTime rangeStart, DateTime rangeEnd)
        {
            var duration = calendarEvent.EndDate.Date - calendarEvent.StartDate.Date;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            var occurrenceStart = AlignOccurrenceStart(calendarEvent, rangeStart, duration);
            var safetyCounter = 0;

            while (occurrenceStart <= rangeEnd && safetyCounter < 1000)
            {
                var occurrenceEnd = occurrenceStart.Add(duration);
                if (occurrenceEnd >= rangeStart && occurrenceStart <= rangeEnd)
                {
                    yield return calendarEvent.CloneWithDates(occurrenceStart, occurrenceEnd);
                }

                if (calendarEvent.Recurrence == CalendarRecurrenceType.None)
                {
                    yield break;
                }

            occurrenceStart = GetNextOccurrenceStart(occurrenceStart, calendarEvent.Recurrence);
                if (occurrenceStart == DateTime.MinValue)
                {
                    yield break;
                }

                safetyCounter++;
            }
        }

        private static DateTime AlignOccurrenceStart(CalendarEventDisplayViewModel calendarEvent, DateTime rangeStart, TimeSpan duration)
        {
            var start = calendarEvent.StartDate.Date;
            if (calendarEvent.Recurrence == CalendarRecurrenceType.None || start >= rangeStart)
            {
                return start;
            }

            switch (calendarEvent.Recurrence)
            {
                case CalendarRecurrenceType.Daily:
                case CalendarRecurrenceType.Weekly:
                case CalendarRecurrenceType.BiWeekly:
                    var stepDays = calendarEvent.Recurrence == CalendarRecurrenceType.Daily
                        ? 1
                        : calendarEvent.Recurrence == CalendarRecurrenceType.Weekly
                            ? 7
                            : 14;
                    var diff = (int)((rangeStart.Date - start).TotalDays / stepDays);
                    if (diff > 0)
                    {
                        start = start.AddDays(diff * stepDays);
                    }
                    while (start.Add(duration) < rangeStart.Date)
                    {
                        start = start.AddDays(stepDays);
                    }
                    return start;

                case CalendarRecurrenceType.Monthly:
                    while (start.Add(duration) < rangeStart.Date)
                    {
                        start = start.AddMonths(1);
                    }
                    return start;

                case CalendarRecurrenceType.Yearly:
                    while (start.Add(duration) < rangeStart.Date)
                    {
                        start = start.AddYears(1);
                    }
                    return start;

                default:
                    return start;
            }
        }

        private static DateTime GetNextOccurrenceStart(DateTime currentStart, CalendarRecurrenceType recurrence)
        {
            return recurrence switch
            {
                CalendarRecurrenceType.Daily => currentStart.AddDays(1),
                CalendarRecurrenceType.Weekly => currentStart.AddDays(7),
                CalendarRecurrenceType.BiWeekly => currentStart.AddDays(14),
                CalendarRecurrenceType.Monthly => currentStart.AddMonths(1),
                CalendarRecurrenceType.Yearly => currentStart.AddYears(1),
                _ => DateTime.MinValue
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
    }
}

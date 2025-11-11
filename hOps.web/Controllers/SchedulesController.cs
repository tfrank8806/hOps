using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class SchedulesController : BaseController
    {
        private readonly IUserTimeZoneService _timeZoneService;
        private readonly SchedulePublicationService _publicationService;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<SchedulesController> _logger;
        private readonly SchedulePdfRenderer _pdfRenderer;
        private const string ScheduleSortSessionKey = "ScheduleSortOption";

        public SchedulesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUserTimeZoneService timeZoneService,
            SchedulePublicationService publicationService,
            IEmailSender emailSender,
            ILogger<SchedulesController> logger,
            SchedulePdfRenderer pdfRenderer)
            : base(context, userManager)
        {
            _timeZoneService = timeZoneService;
            _publicationService = publicationService;
            _emailSender = emailSender;
            _logger = logger;
            _pdfRenderer = pdfRenderer;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? weekStart = null, string? sortOption = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var selectedSort = ResolveSortOption(sortOption);
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                ViewData["Title"] = "Schedules";
                return View(new SchedulePageViewModel
                {
                    AlertMessage = "Select a property to view schedules.",
                    SortOption = selectedSort,
                    SortOptions = BuildSortOptions(selectedSort)
                });
            }

            await EnsureScheduleEmployeesForPropertyAsync(property.Id, currentUser.Id);

            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var canManage = currentRoles.Contains("Admin") || currentRoles.Contains("Manager");

            var settings = await _context.ScheduleSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.PropertyId == property.Id);
            var startDay = settings?.StartDayOfWeek ?? DayOfWeek.Monday;

            var referenceDate = ParseWeekStart(weekStart) ?? _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;
            var alignedWeekStart = AlignToWeekStart(referenceDate, startDay);
            var normalizedWeekStart = DateTime.SpecifyKind(alignedWeekStart, DateTimeKind.Utc);
            var alignedWeekEnd = alignedWeekStart.AddDays(6);

            var schedule = await _context.Schedules
                .Include(s => s.Assignments)
                    .ThenInclude(a => a.Employee)
                .Where(s => s.PropertyId == property.Id && s.WeekStartDate == normalizedWeekStart)
                .FirstOrDefaultAsync();

            string? autoShiftMessage = null;

            if (schedule == null)
            {
                var draftCandidates = await _context.Schedules
                    .Include(s => s.Assignments)
                        .ThenInclude(a => a.Employee)
                    .Where(s => s.PropertyId == property.Id && s.Status == ScheduleStatus.Draft)
                    .ToListAsync();

                var bestCandidate = draftCandidates
                    .Select(s => new
                    {
                        Schedule = s,
                        DeltaDays = CalculateWeekShiftDelta(s.WeekStartDate, normalizedWeekStart)
                    })
                    .Where(x => x.DeltaDays != 0 && Math.Abs(x.DeltaDays) <= 6)
                    .OrderBy(x => Math.Abs(x.DeltaDays))
                    .FirstOrDefault();

                if (bestCandidate != null)
                {
                    var draftToShift = bestCandidate.Schedule;
                    var deltaDays = bestCandidate.DeltaDays;

                    draftToShift.WeekStartDate = draftToShift.WeekStartDate.AddDays(deltaDays);
                    draftToShift.WeekEndDate = draftToShift.WeekStartDate.AddDays(6);
                    foreach (var assignment in draftToShift.Assignments)
                    {
                        assignment.ShiftDate = assignment.ShiftDate.AddDays(deltaDays);
                    }

                    await _context.SaveChangesAsync();
                    schedule = draftToShift;
                    alignedWeekStart = schedule.WeekStartDate.Date;
                    alignedWeekEnd = alignedWeekStart.AddDays(6);
                    normalizedWeekStart = schedule.WeekStartDate;
                    autoShiftMessage = "Draft schedule updated to match the new week start day.";
                }
            }

            var dayColumns = Enumerable.Range(0, 7)
                .Select(i => alignedWeekStart.AddDays(i))
                .ToList();

            var shiftTemplates = await _context.ScheduleShiftTemplates
                .Where(t => t.PropertyId == property.Id)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Name)
                .ToListAsync();

            var scheduleEmployees = await _context.ScheduleEmployees
                .Where(e => e.PropertyId == property.Id)
                .OrderBy(e => e.DisplayName)
                .ToListAsync();

            var pendingRequests = await _context.ScheduleTimeOffRequests
                .Where(r => r.PropertyId == property.Id && r.Status == TimeOffRequestStatus.Pending)
                .Include(r => r.Employee)
                .Include(r => r.SubmittedByUser)
                .OrderBy(r => r.StartDate)
                .ToListAsync();

            var approvedRequestsForWeek = await _context.ScheduleTimeOffRequests
                .Where(r => r.PropertyId == property.Id &&
                            r.Status == TimeOffRequestStatus.Approved &&
                            r.StartDate <= alignedWeekEnd &&
                            r.EndDate >= alignedWeekStart)
                .Include(r => r.Employee)
                .ToListAsync();

            var myRequests = await _context.ScheduleTimeOffRequests
                .Where(r => r.PropertyId == property.Id && r.SubmittedByUserId == currentUser.Id)
                .Include(r => r.Employee)
                .OrderByDescending(r => r.SubmittedAtUtc)
                .Take(10)
                .ToListAsync();

            var scheduleMessage = TempData["ScheduleMessage"] as string;
            var scheduleError = TempData["ScheduleError"] as string;
            var combinedMessages = new List<string>();
            if (!string.IsNullOrWhiteSpace(scheduleMessage))
            {
                combinedMessages.Add(scheduleMessage);
            }
            if (!string.IsNullOrWhiteSpace(autoShiftMessage))
            {
                combinedMessages.Add(autoShiftMessage);
            }
            var alertMessage = combinedMessages.Count > 0 ? string.Join(" ", combinedMessages) : null;

            var vm = new SchedulePageViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                WeekStartDate = alignedWeekStart,
                WeekEndDate = alignedWeekEnd,
                HasSchedule = schedule != null,
                ScheduleId = schedule?.Id,
                Status = schedule?.Status,
                CanManage = canManage,
                SettingsSummary = new ScheduleSettingsSummaryViewModel
                {
                    StartDayOfWeek = startDay,
                    ShiftTemplateCount = shiftTemplates.Count,
                    SettingsUrl = Url.Action("ScheduleSetup", "Settings", new { propertyId = property.Id })
                },
                DayColumns = dayColumns
                    .Select(d => new ScheduleDayColumnViewModel
                    {
                        Date = d,
                        Label = d.ToString("ddd, MMM d"),
                        IsToday = d.Date == _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date
                    })
                    .ToList(),
                ShiftTemplates = shiftTemplates
                    .Select(t => new ScheduleShiftTemplateViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        ShiftName = string.IsNullOrWhiteSpace(t.ShiftName) ? t.Name : t.ShiftName,
                        StartTime = t.StartTime,
                        EndTime = t.EndTime,
                        ColorHex = string.IsNullOrWhiteSpace(t.ColorHex) ? "#3b82f6" : t.ColorHex
                    })
                    .ToList(),
                EmployeeOptions = scheduleEmployees
                    .Select(e => new ScheduleEmployeeOptionViewModel
                    {
                        Id = e.Id,
                        DisplayName = e.IsActive ? e.DisplayName : $"{e.DisplayName} (Inactive)",
                        SourceLabel = e.ApplicationUserId == null ? "Manual" : "User",
                        IsManual = e.ApplicationUserId == null,
                        EmailAlertsEnabled = e.EmailAlertsEnabled,
                        Email = e.Email
                    })
                    .ToList(),
                SortOption = selectedSort,
                SortOptions = BuildSortOptions(selectedSort),
                AssignmentForm = new ScheduleAssignmentFormViewModel
                {
                    ScheduleId = schedule?.Id ?? 0,
                    ShiftDate = alignedWeekStart,
                    ShiftName = shiftTemplates
                        .Select(t => string.IsNullOrWhiteSpace(t.ShiftName) ? t.Name : t.ShiftName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Shift",
                    RepeatDays = 1
                },
                EmployeeForm = new ScheduleEmployeeFormViewModel(),
                TimeOffForm = new TimeOffRequestFormViewModel
                {
                    StartDate = alignedWeekStart,
                    EndDate = alignedWeekStart.AddDays(1)
                },
                PendingRequests = pendingRequests
                    .Select(MapRequest)
                    .ToList(),
                MyRequests = myRequests
                    .Select(MapRequest)
                    .ToList(),
                ShowCreateDraftAction = schedule == null && canManage,
                AlertMessage = alertMessage
            };

            if (!string.IsNullOrWhiteSpace(scheduleError))
            {
                ViewBag.ScheduleError = scheduleError;
            }

            if (schedule != null)
            {
                vm.EmployeeRows = BuildEmployeeRows(schedule, vm.DayColumns, approvedRequestsForWeek, scheduleEmployees, selectedSort);
            }

            ViewData["Title"] = "Schedules";
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSchedule(string format, string? weekStart = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["ScheduleError"] = "Select a property before downloading schedules.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedFormat = (format ?? "pdf").Trim().ToLowerInvariant();
            if (normalizedFormat != "pdf" && normalizedFormat != "xlsx")
            {
                normalizedFormat = "pdf";
            }

            var settings = await _context.ScheduleSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.PropertyId == property.Id);
            var startDay = settings?.StartDayOfWeek ?? DayOfWeek.Monday;

            var referenceDate = ParseWeekStart(weekStart) ?? _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;
            var alignedWeekStart = AlignToWeekStart(referenceDate, startDay);
            var normalizedWeekStart = DateTime.SpecifyKind(alignedWeekStart, DateTimeKind.Utc);
            var alignedWeekEnd = alignedWeekStart.AddDays(6);

            var schedule = await _context.Schedules
                .Include(s => s.Assignments)
                    .ThenInclude(a => a.Employee)
                .FirstOrDefaultAsync(s => s.PropertyId == property.Id && s.WeekStartDate == normalizedWeekStart);

            if (schedule == null)
            {
                TempData["ScheduleError"] = "No schedule exists for that week.";
                return RedirectToAction(nameof(Index), new { weekStart = alignedWeekStart.ToString("yyyy-MM-dd") });
            }

            var dayColumns = Enumerable.Range(0, 7)
                .Select(i => alignedWeekStart.AddDays(i))
                .ToList();

            var approvedRequests = await _context.ScheduleTimeOffRequests
                .Where(r => r.PropertyId == property.Id &&
                            r.Status == TimeOffRequestStatus.Approved &&
                            r.StartDate <= alignedWeekEnd &&
                            r.EndDate >= alignedWeekStart)
                .Include(r => r.Employee)
                .ToListAsync();

            var gridRows = ScheduleGridBuilder.BuildRows(dayColumns, schedule.Assignments, approvedRequests);
            if (!gridRows.Any())
            {
                TempData["ScheduleError"] = "Add at least one shift before downloading the schedule.";
                return RedirectToAction(nameof(Index), new { weekStart = alignedWeekStart.ToString("yyyy-MM-dd") });
            }

            var scheduleEmployees = await _context.ScheduleEmployees
                .Where(e => e.PropertyId == property.Id)
                .OrderBy(e => e.DisplayName)
                .ToListAsync();

            var dayColumnViewModels = dayColumns
                .Select(d => new ScheduleDayColumnViewModel
                {
                    Date = d,
                    Label = d.ToString("ddd, MMM d"),
                    IsToday = false
                })
                .ToList();

            var sortOption = ResolveSortOption(null);
            var employeeRows = BuildEmployeeRows(schedule, dayColumnViewModels, approvedRequests, scheduleEmployees, sortOption);
            if (employeeRows.Any())
            {
                var lookup = gridRows.ToDictionary(r => r.ScheduleEmployeeId);
                var orderedRows = new List<ScheduleGridRow>();
                foreach (var row in employeeRows)
                {
                    if (lookup.TryGetValue(row.ScheduleEmployeeId, out var match))
                    {
                        orderedRows.Add(match);
                    }
                }

                if (orderedRows.Count != gridRows.Count)
                {
                    var seen = new HashSet<int>(orderedRows.Select(r => r.ScheduleEmployeeId));
                    orderedRows.AddRange(gridRows.Where(r => !seen.Contains(r.ScheduleEmployeeId)));
                }

                gridRows = orderedRows;
            }

            if (normalizedFormat == "xlsx")
            {
                var fileBytes = BuildScheduleExcel(property.Name, dayColumns, gridRows);
                var fileName = BuildScheduleFileName(property.Name, alignedWeekStart, "xlsx");
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            else
            {
                var pdfRows = gridRows
                    .Select(r => new SchedulePdfRow
                    {
                        EmployeeName = r.EmployeeName,
                        CellLines = r.CellLines
                    })
                    .ToList();

                var title = $"{alignedWeekStart:MMM d} - {alignedWeekEnd:MMM d}";
                var pdfBytes = _pdfRenderer.Render(property.Name, title, dayColumns, pdfRows);
                var fileName = BuildScheduleFileName(property.Name, alignedWeekStart, "pdf");
                return File(pdfBytes, "application/pdf", fileName);
            }
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDraft(string weekStart)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["ScheduleError"] = "Select a property before creating a schedule.";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var settings = await _context.ScheduleSettings.FirstOrDefaultAsync(s => s.PropertyId == property.Id);
            var startDay = settings?.StartDayOfWeek ?? DayOfWeek.Monday;
            var targetDate = ParseWeekStart(weekStart) ?? DateTime.Today;
            var alignedWeekStart = AlignToWeekStart(targetDate, startDay);
            var normalizedWeekStart = DateTime.SpecifyKind(alignedWeekStart, DateTimeKind.Utc);
            var existing = await _context.Schedules
                .FirstOrDefaultAsync(s => s.PropertyId == property.Id && s.WeekStartDate == normalizedWeekStart);

            if (existing != null)
            {
                TempData["ScheduleError"] = "A schedule already exists for that week.";
                return RedirectToAction(nameof(Index), new { weekStart = alignedWeekStart.ToString("yyyy-MM-dd") });
            }

            var schedule = new Schedule
            {
                PropertyId = property.Id,
                WeekStartDate = normalizedWeekStart,
                WeekEndDate = normalizedWeekStart.AddDays(6),
                Status = ScheduleStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };

            _context.Schedules.Add(schedule);
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Draft schedule created.";
            return RedirectToAction(nameof(Index), new { weekStart = alignedWeekStart.ToString("yyyy-MM-dd") });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAssignment([Bind(Prefix = "AssignmentForm")] ScheduleAssignmentFormViewModel form, string? weekStart = null)
        {
            if (!ModelState.IsValid)
            {
                TempData["ScheduleError"] = "Please complete all required fields for the shift.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var schedule = await _context.Schedules
                .Include(s => s.Assignments)
                .FirstOrDefaultAsync(s => s.Id == form.ScheduleId);

            if (schedule == null)
            {
                TempData["ScheduleError"] = "Schedule not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (schedule.Status != ScheduleStatus.Draft)
            {
                TempData["ScheduleError"] = "Posted schedules cannot be modified.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var employee = await _context.ScheduleEmployees
                .FirstOrDefaultAsync(e => e.Id == form.ScheduleEmployeeId && e.PropertyId == schedule.PropertyId);

            if (employee == null)
            {
                TempData["ScheduleError"] = "Employee not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            ScheduleShiftTemplate? selectedTemplate = null;
            if (form.ShiftTemplateId.HasValue)
            {
                selectedTemplate = await _context.ScheduleShiftTemplates
                    .FirstOrDefaultAsync(t => t.Id == form.ShiftTemplateId.Value && t.PropertyId == schedule.PropertyId);
            }

            if (string.IsNullOrWhiteSpace(form.ShiftName) && selectedTemplate != null)
            {
                form.ShiftName = string.IsNullOrWhiteSpace(selectedTemplate.ShiftName)
                    ? selectedTemplate.Name
                    : selectedTemplate.ShiftName;
            }

            var (startTime, endTime) = ParseShiftTimes(form, selectedTemplate);
            var normalizedShiftName = string.IsNullOrWhiteSpace(form.ShiftName) ? "Shift" : form.ShiftName.Trim();
            var resolvedColorHex = NormalizeColorHex(selectedTemplate?.ColorHex) ?? NormalizeColorHex(form.ShiftColorHex);

            var repeatDays = form.RepeatDays < 1 ? 1 : Math.Min(form.RepeatDays, 21);
            var repeatLimitDate = schedule.WeekEndDate.Date;
            var firstDate = DateTime.SpecifyKind(form.ShiftDate.Date, DateTimeKind.Utc);

            var assignmentsToInsert = new List<ScheduleAssignment>();
            var processed = 0;
            var candidateDate = firstDate;

            while (processed < repeatDays && candidateDate <= repeatLimitDate)
            {
                if (form.RepeatSkipWeekends && (candidateDate.DayOfWeek == DayOfWeek.Saturday || candidateDate.DayOfWeek == DayOfWeek.Sunday))
                {
                    candidateDate = candidateDate.AddDays(1);
                    processed++;
                    continue;
                }

                var alreadyExists = schedule.Assignments.Any(a =>
                    a.ScheduleEmployeeId == employee.Id &&
                    a.ShiftDate.Date == candidateDate.Date &&
                    string.Equals(a.ShiftName, normalizedShiftName, StringComparison.OrdinalIgnoreCase));

                if (!alreadyExists)
                {
                    assignmentsToInsert.Add(new ScheduleAssignment
                    {
                        ScheduleId = schedule.Id,
                        ScheduleEmployeeId = employee.Id,
                        ShiftDate = candidateDate,
                        ShiftName = normalizedShiftName,
                        ShiftStartTime = startTime,
                        ShiftEndTime = endTime,
                        Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim(),
                        ColorHex = resolvedColorHex
                    });
                }

                candidateDate = candidateDate.AddDays(1);
                processed++;
            }

            if (!assignmentsToInsert.Any())
            {
                TempData["ScheduleError"] = "No new shifts were added. The selected dates may already have identical assignments.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            _context.ScheduleAssignments.AddRange(assignmentsToInsert);
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = assignmentsToInsert.Count == 1
                ? "Shift added."
                : $"{assignmentsToInsert.Count} shifts added.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAssignment([Bind(Prefix = "AssignmentForm")] ScheduleAssignmentFormViewModel form, string? weekStart = null)
        {
            if (!form.AssignmentId.HasValue)
            {
                TempData["ScheduleError"] = "Unable to update this shift.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var assignment = await _context.ScheduleAssignments
                .Include(a => a.Schedule)
                .FirstOrDefaultAsync(a => a.Id == form.AssignmentId.Value);

            if (assignment == null)
            {
                TempData["ScheduleError"] = "Shift not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (assignment.Schedule.Status != ScheduleStatus.Draft)
            {
                TempData["ScheduleError"] = "Posted schedules cannot be modified.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var employee = await _context.ScheduleEmployees
                .FirstOrDefaultAsync(e => e.Id == form.ScheduleEmployeeId && e.PropertyId == assignment.Schedule.PropertyId);

            if (employee == null)
            {
                TempData["ScheduleError"] = "Employee not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            ScheduleShiftTemplate? selectedTemplate = null;
            if (form.ShiftTemplateId.HasValue)
            {
                selectedTemplate = await _context.ScheduleShiftTemplates
                    .FirstOrDefaultAsync(t => t.Id == form.ShiftTemplateId.Value && t.PropertyId == assignment.Schedule.PropertyId);
            }

            if (string.IsNullOrWhiteSpace(form.ShiftName) && selectedTemplate != null)
            {
                form.ShiftName = string.IsNullOrWhiteSpace(selectedTemplate.ShiftName)
                    ? selectedTemplate.Name
                    : selectedTemplate.ShiftName;
            }

            var (startTime, endTime) = ParseShiftTimes(form, selectedTemplate);
            var normalizedShiftName = string.IsNullOrWhiteSpace(form.ShiftName) ? "Shift" : form.ShiftName.Trim();
            var resolvedColorHex = NormalizeColorHex(selectedTemplate?.ColorHex) ?? NormalizeColorHex(form.ShiftColorHex) ?? assignment.ColorHex;

            assignment.ScheduleEmployeeId = employee.Id;
            assignment.ShiftDate = DateTime.SpecifyKind(form.ShiftDate.Date, DateTimeKind.Utc);
            assignment.ShiftName = normalizedShiftName;
            assignment.ShiftStartTime = startTime;
            assignment.ShiftEndTime = endTime;
            assignment.Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();
            assignment.ColorHex = resolvedColorHex;

            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Shift updated.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAssignment(int assignmentId, string? weekStart = null)
        {
            var assignment = await _context.ScheduleAssignments
                .Include(a => a.Schedule)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null)
            {
                TempData["ScheduleError"] = "Shift not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (assignment.Schedule.Status != ScheduleStatus.Draft)
            {
                TempData["ScheduleError"] = "Posted schedules cannot be modified.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            _context.ScheduleAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Shift removed.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveEmployeeFromSchedule(int scheduleId, int scheduleEmployeeId, string? weekStart = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            var schedule = await _context.Schedules
                .Include(s => s.Assignments)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null)
            {
                TempData["ScheduleError"] = "Schedule not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (property == null || schedule.PropertyId != property.Id)
            {
                return Forbid();
            }

            if (schedule.Status != ScheduleStatus.Draft)
            {
                TempData["ScheduleError"] = "You can only remove employees while the schedule is in draft.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var assignments = schedule.Assignments
                .Where(a => a.ScheduleEmployeeId == scheduleEmployeeId)
                .ToList();

            if (!assignments.Any())
            {
                TempData["ScheduleMessage"] = "No assignments to remove for this employee.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            _context.ScheduleAssignments.RemoveRange(assignments);
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Employee removed from this schedule.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PasteAssignment(int scheduleId, int sourceAssignmentId, int targetEmployeeId, DateTime targetDate, string? weekStart = null)
        {
            var property = ViewBag.CurrentProperty as Property;

            var schedule = await _context.Schedules
                .Include(s => s.Assignments)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null || property == null || schedule.PropertyId != property.Id)
            {
                TempData["ScheduleError"] = "Schedule not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (schedule.Status != ScheduleStatus.Draft)
            {
                TempData["ScheduleError"] = "Posted schedules cannot be modified.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var source = await _context.ScheduleAssignments
                .FirstOrDefaultAsync(a => a.Id == sourceAssignmentId && a.ScheduleId == scheduleId);

            if (source == null)
            {
                TempData["ScheduleError"] = "Source shift not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var employee = await _context.ScheduleEmployees
                .FirstOrDefaultAsync(e => e.Id == targetEmployeeId && e.PropertyId == schedule.PropertyId);

            if (employee == null)
            {
                TempData["ScheduleError"] = "Employee not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var normalizedTargetDate = DateTime.SpecifyKind(targetDate.Date, DateTimeKind.Utc);
            if (normalizedTargetDate < schedule.WeekStartDate || normalizedTargetDate > schedule.WeekEndDate)
            {
                TempData["ScheduleError"] = "Target date is outside of this schedule.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var duplicate = schedule.Assignments.Any(a =>
                a.ScheduleEmployeeId == employee.Id &&
                a.ShiftDate.Date == normalizedTargetDate.Date &&
                string.Equals(a.ShiftName, source.ShiftName, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                TempData["ScheduleError"] = "A matching shift already exists in that slot.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var clone = new ScheduleAssignment
            {
                ScheduleId = schedule.Id,
                ScheduleEmployeeId = employee.Id,
                ShiftDate = normalizedTargetDate,
                ShiftName = source.ShiftName,
                ShiftStartTime = source.ShiftStartTime,
                ShiftEndTime = source.ShiftEndTime,
                Notes = source.Notes,
                ColorHex = source.ColorHex
            };

            _context.ScheduleAssignments.Add(clone);
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Shift pasted.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostSchedule(int scheduleId, string? weekStart = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var schedule = await _context.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId);
            if (schedule == null)
            {
                TempData["ScheduleError"] = "Schedule not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (schedule.Status == ScheduleStatus.Posted)
            {
                TempData["ScheduleError"] = "This schedule is already posted.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var scheduleUrl = Url.Action(nameof(Index), "Schedules", new { weekStart = schedule.WeekStartDate.ToString("yyyy-MM-dd") }, Request.Scheme);
            var result = await _publicationService.PublishAsync(scheduleId, currentUser.Id, scheduleUrl);

            if (!result.Success)
            {
                TempData["ScheduleError"] = result.ErrorMessage ?? "Unable to post the schedule.";
            }
            else
            {
                TempData["ScheduleMessage"] = "Schedule posted and notifications sent.";
            }

            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployee([Bind(Prefix = "EmployeeForm")] ScheduleEmployeeFormViewModel form, string? weekStart = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["ScheduleError"] = "Select a property before adding employees.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (!ModelState.IsValid)
            {
                TempData["ScheduleError"] = "Enter a name for the employee.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var employee = new ScheduleEmployee
            {
                PropertyId = property.Id,
                DisplayName = form.DisplayName.Trim(),
                Email = string.IsNullOrWhiteSpace(form.Email) ? null : form.Email.Trim(),
                EmailAlertsEnabled = form.EmailAlertsEnabled,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = currentUser.Id
            };

            _context.ScheduleEmployees.Add(employee);
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Employee added to the schedule roster.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTimeOff([Bind(Prefix = "TimeOffForm")] TimeOffRequestFormViewModel form, string? weekStart = null)
        {
            if (!ModelState.IsValid)
            {
                TempData["ScheduleError"] = "Please complete all request-off fields.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (form.StartDate > form.EndDate)
            {
                TempData["ScheduleError"] = "The start date must be before the end date.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["ScheduleError"] = "Select a property before submitting requests.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var employee = await _context.ScheduleEmployees
                .FirstOrDefaultAsync(e => e.PropertyId == property.Id && e.ApplicationUserId == currentUser.Id);

            if (employee == null)
            {
                await EnsureScheduleEmployeesForPropertyAsync(property.Id, currentUser.Id);
                employee = await _context.ScheduleEmployees
                    .FirstOrDefaultAsync(e => e.PropertyId == property.Id && e.ApplicationUserId == currentUser.Id);
            }

            if (employee == null)
            {
                TempData["ScheduleError"] = "You are not listed for this property.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var request = new ScheduleTimeOffRequest
            {
                PropertyId = property.Id,
                ScheduleEmployeeId = employee.Id,
                SubmittedByUserId = currentUser.Id,
                StartDate = DateTime.SpecifyKind(form.StartDate.Date, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(form.EndDate.Date, DateTimeKind.Utc),
                Reason = form.Reason.Trim(),
                Status = TimeOffRequestStatus.Pending,
                SubmittedAtUtc = DateTime.UtcNow
            };

            _context.ScheduleTimeOffRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Request submitted for review.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DecideRequest(int requestId, string decision, string? notes, string? weekStart = null)
        {
            var request = await _context.ScheduleTimeOffRequests
                .Include(r => r.Employee)
                .Include(r => r.SubmittedByUser)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                TempData["ScheduleError"] = "Request not found.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            if (request.Status != TimeOffRequestStatus.Pending)
            {
                TempData["ScheduleError"] = "This request was already reviewed.";
                return RedirectToAction(nameof(Index), new { weekStart });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            request.Status = string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase)
                ? TimeOffRequestStatus.Approved
                : TimeOffRequestStatus.Denied;
            request.DecisionByUserId = currentUser.Id;
            request.DecisionByUser = currentUser;
            request.DecisionAtUtc = DateTime.UtcNow;
            request.DecisionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = request.SubmittedByUserId,
                Type = "schedule",
                Title = $"Time-off request {request.Status.ToString().ToLowerInvariant()}",
                Content = $"Your request for {request.StartDate:MMM d} - {request.EndDate:MMM d} was {request.Status.ToString().ToLowerInvariant()}.",
                LinkUrl = Url.Action(nameof(Index), "Schedules", new { weekStart }),
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });

            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(request.SubmittedByUser.Email) &&
                (request.SubmittedByUser.EmailOnSchedulePosted || request.Status == TimeOffRequestStatus.Denied))
            {
                try
                {
                    var body = $"<p>Hi {System.Net.WebUtility.HtmlEncode(request.SubmittedByUser.FirstName)},</p>" +
                               $"<p>Your time-off request for {request.StartDate:MMM d} - {request.EndDate:MMM d} was <strong>{request.Status}</strong>.</p>";
            if (!string.IsNullOrWhiteSpace(request.DecisionNotes))
            {
                body += $"<p>Notes: {System.Net.WebUtility.HtmlEncode(request.DecisionNotes)}</p>";
            }
            body += "<p>Visit HotelOps to review the full schedule.</p>";
                    await _emailSender.SendEmailAsync(request.SubmittedByUser.Email!, "Time-off request update", body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to email time-off decision to {UserId}", request.SubmittedByUserId);
                }
            }

            TempData["ScheduleMessage"] = $"Request {request.Status.ToString().ToLowerInvariant()}.";
            return RedirectToAction(nameof(Index), new { weekStart });
        }

        private async Task EnsureScheduleEmployeesForPropertyAsync(int propertyId, string currentUserId)
        {
            var propertyUsers = await _context.UserPropertyAccesses
                .Where(upa => upa.PropertyId == propertyId)
                .Select(upa => upa.ApplicationUser)
                .Where(u => u != null)
                .Select(u => u!)
                .ToListAsync();

            var existingUserEmployees = await _context.ScheduleEmployees
                .Where(e => e.PropertyId == propertyId && e.ApplicationUserId != null)
                .ToListAsync();

            var existingUserIds = existingUserEmployees
                .Where(e => !string.IsNullOrWhiteSpace(e.ApplicationUserId))
                .Select(e => e.ApplicationUserId!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var newEntries = new List<ScheduleEmployee>();

            foreach (var user in propertyUsers)
            {
                if (existingUserIds.Contains(user.Id))
                {
                    continue;
                }

                newEntries.Add(new ScheduleEmployee
                {
                    PropertyId = propertyId,
                    ApplicationUserId = user.Id,
                    DisplayName = BuildDisplayName(user),
                    Email = user.Email,
                    EmailAlertsEnabled = true,
                    CreatedAtUtc = now,
                    CreatedByUserId = currentUserId
                });
            }

            if (newEntries.Any())
            {
                _context.ScheduleEmployees.AddRange(newEntries);
            }

            foreach (var employee in existingUserEmployees)
            {
                var user = propertyUsers.FirstOrDefault(u => string.Equals(u.Id, employee.ApplicationUserId, StringComparison.OrdinalIgnoreCase));
                if (user == null)
                {
                    continue;
                }

                var displayName = BuildDisplayName(user);
                var updated = false;

                if (!string.Equals(displayName, employee.DisplayName, StringComparison.Ordinal))
                {
                    employee.DisplayName = displayName;
                    updated = true;
                }

                if (!string.Equals(employee.Email ?? string.Empty, user.Email ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    employee.Email = user.Email;
                    updated = true;
                }

                if (updated)
                {
                    employee.UpdatedAtUtc = now;
                }
            }

            if (newEntries.Any() || existingUserEmployees.Any(e => e.UpdatedAtUtc == now))
            {
                await _context.SaveChangesAsync();
            }
        }

        private static string BuildDisplayName(ApplicationUser user)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName))
            {
                parts.Add(user.FirstName);
            }
            if (!string.IsNullOrWhiteSpace(user.LastName))
            {
                parts.Add(user.LastName);
            }

            if (parts.Count > 0)
            {
                return string.Join(" ", parts);
            }

            return user.Email ?? user.UserName ?? "Employee";
        }

        private static DateTime AlignToWeekStart(DateTime date, DayOfWeek startDay)
        {
            while (date.DayOfWeek != startDay)
            {
                date = date.AddDays(-1);
            }

            return date.Date;
        }

        private static string? NormalizeColorHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = "#" + trimmed;
            }

            if (trimmed.Length != 7)
            {
                return null;
            }

            for (int i = 1; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex)
                {
                    return null;
                }
            }

            return trimmed.ToLowerInvariant();
        }

        private static int CalculateWeekShiftDelta(DateTime currentStart, DateTime desiredStart)
        {
            var rawDelta = (int)(desiredStart.Date - currentStart.Date).TotalDays;
            if (Math.Abs(rawDelta) > 6)
            {
                return rawDelta;
            }

            if (rawDelta > 3)
            {
                rawDelta -= 7;
            }
            else if (rawDelta < -3)
            {
                rawDelta += 7;
            }

            return rawDelta;
        }

        private static DateTime? ParseWeekStart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(value, out var parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        private static (TimeSpan?, TimeSpan?) ParseShiftTimes(ScheduleAssignmentFormViewModel form, ScheduleShiftTemplate? template)
        {
            TimeSpan? start = TryParseTime(form.ShiftStartTime);
            TimeSpan? end = TryParseTime(form.ShiftEndTime);

            if (template != null)
            {
                start ??= template.StartTime;
                end ??= template.EndTime;
            }

            return (start, end);
        }

        private static TimeSpan? TryParseTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (TimeSpan.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static List<ScheduleEmployeeRowViewModel> BuildEmployeeRows(
            Schedule schedule,
            IReadOnlyList<ScheduleDayColumnViewModel> dayColumns,
            IReadOnlyList<ScheduleTimeOffRequest> approvedRequests,
            IReadOnlyList<ScheduleEmployee>? rosterEmployees = null,
            ScheduleSortOption sortOption = ScheduleSortOption.EmployeeName)
        {
            var rows = new List<ScheduleEmployeeRowViewModel>();

            var assignmentsByEmployee = schedule.Assignments
                .GroupBy(a => a.ScheduleEmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var approvedLookup = new HashSet<(int EmployeeId, DateTime Date)>();
            foreach (var request in approvedRequests)
            {
                var current = request.StartDate.Date;
                var end = request.EndDate.Date;
                while (current <= end)
                {
                    approvedLookup.Add((request.ScheduleEmployeeId, current));
                    current = current.AddDays(1);
                }
            }

            var activeEmployeeIds = assignmentsByEmployee.Keys
                .Concat(approvedRequests.Select(r => r.ScheduleEmployeeId))
                .Distinct()
                .ToList();

            var rosterOrder = rosterEmployees?
                .Select((e, index) => new { e.Id, index })
                .ToDictionary(x => x.Id, x => x.index) ?? new Dictionary<int, int>();

            activeEmployeeIds.Sort((a, b) =>
            {
                var hasA = rosterOrder.TryGetValue(a, out var indexA);
                var hasB = rosterOrder.TryGetValue(b, out var indexB);

                if (hasA && hasB)
                {
                    return indexA.CompareTo(indexB);
                }
                if (hasA)
                {
                    return -1;
                }
                if (hasB)
                {
                    return 1;
                }

                return a.CompareTo(b);
            });

            foreach (var employeeId in activeEmployeeIds)
            {
                assignmentsByEmployee.TryGetValue(employeeId, out var assignmentList);

                var employee = assignmentList?.FirstOrDefault()?.Employee
                    ?? rosterEmployees?.FirstOrDefault(e => e.Id == employeeId)
                    ?? approvedRequests.FirstOrDefault(r => r.ScheduleEmployeeId == employeeId)?.Employee;

                if (employee == null)
                {
                    continue;
                }

                var row = new ScheduleEmployeeRowViewModel
                {
                    ScheduleEmployeeId = employeeId,
                    EmployeeName = employee.DisplayName,
                    IsManual = employee.ApplicationUserId == null,
                    SourceLabel = employee.ApplicationUserId == null ? "Manual" : "User",
                    IsActive = employee.IsActive
                };

                foreach (var day in dayColumns)
                {
                    var dayDate = day.Date.Date;
                    var assignmentsForDay = assignmentsByEmployee.TryGetValue(employeeId, out var employeeAssignments)
                        ? employeeAssignments
                            .Where(a => a.ShiftDate.Date == dayDate)
                            .OrderBy(a => a.ShiftStartTime ?? TimeSpan.Zero)
                            .ToList()
                        : new List<ScheduleAssignment>();

                    row.AssignmentsByDate[dayDate] = assignmentsForDay
                        .Select(a => new ScheduleAssignmentItemViewModel
                        {
                            AssignmentId = a.Id,
                            ShiftName = a.ShiftName,
                            ShiftStartTime = a.ShiftStartTime,
                            ShiftEndTime = a.ShiftEndTime,
                            Notes = a.Notes,
                            ColorHex = a.ColorHex
                        })
                        .ToList();

                    if (approvedLookup.Contains((employeeId, dayDate)))
                    {
                        row.TimeOffByDate[dayDate] = new List<TimeOffBadgeViewModel>
                        {
                            new TimeOffBadgeViewModel
                            {
                                Label = "Approved time off",
                                Status = TimeOffRequestStatus.Approved
                            }
                        };
                    }
                }

                rows.Add(row);
            }

            foreach (var row in rows)
            {
                row.PrimaryShiftName = row.AssignmentsByDate.Values
                    .SelectMany(a => a)
                    .Select(a => a.ShiftName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;
            }

            IOrderedEnumerable<ScheduleEmployeeRowViewModel> orderedRows;
            if (sortOption == ScheduleSortOption.ShiftName)
            {
                orderedRows = rows
                    .OrderBy(r => string.IsNullOrWhiteSpace(r.PrimaryShiftName))
                    .ThenBy(r => r.PrimaryShiftName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.EmployeeName, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                orderedRows = rows.OrderBy(r => r.EmployeeName, StringComparer.OrdinalIgnoreCase);
            }

            return orderedRows.ToList();
        }

        private ScheduleSortOption ResolveSortOption(string? requestedSort)
        {
            var parsed = ParseSortOption(requestedSort);
            if (parsed.HasValue)
            {
                HttpContext.Session.SetString(ScheduleSortSessionKey, parsed.Value.ToString());
                return parsed.Value;
            }

            var stored = ParseSortOption(HttpContext.Session.GetString(ScheduleSortSessionKey));
            if (stored.HasValue)
            {
                return stored.Value;
            }

            HttpContext.Session.SetString(ScheduleSortSessionKey, ScheduleSortOption.EmployeeName.ToString());
            return ScheduleSortOption.EmployeeName;
        }

        private static ScheduleSortOption? ParseSortOption(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Enum.TryParse<ScheduleSortOption>(value, true, out var parsed)
                ? parsed
                : null;
        }

        private static List<SelectListItem> BuildSortOptions(ScheduleSortOption selected)
        {
            return new List<SelectListItem>
            {
                new SelectListItem("Employee name", ScheduleSortOption.EmployeeName.ToString(), selected == ScheduleSortOption.EmployeeName),
                new SelectListItem("Shift name", ScheduleSortOption.ShiftName.ToString(), selected == ScheduleSortOption.ShiftName)
            };
        }

        private static byte[] BuildScheduleExcel(string propertyName, IReadOnlyList<DateTime> dayColumns, IReadOnlyList<ScheduleGridRow> rows)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Schedule");

            worksheet.Cell(1, 1).Value = "Employee";
            for (int i = 0; i < dayColumns.Count; i++)
            {
                var headerCell = worksheet.Cell(1, i + 2);
                headerCell.Value = $"{dayColumns[i]:ddd}\n{dayColumns[i]:MMM d}";
                headerCell.Style.Alignment.WrapText = true;
            }

            var headerRange = worksheet.Range(1, 1, 1, dayColumns.Count + 1);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f5f5");
            worksheet.SheetView.FreezeRows(1);

            int rowIndex = 2;
            foreach (var row in rows)
            {
                worksheet.Cell(rowIndex, 1).Value = row.EmployeeName;
                for (int dayIndex = 0; dayIndex < dayColumns.Count; dayIndex++)
                {
                    var lines = row.CellLines.Count > dayIndex ? row.CellLines[dayIndex] : new List<string>();
                    var cell = worksheet.Cell(rowIndex, dayIndex + 2);
                    if (lines.Count > 0)
                    {
                        cell.Value = string.Join(Environment.NewLine, lines);
                        cell.Style.Alignment.WrapText = true;
                    }
                    else
                    {
                        cell.Value = string.Empty;
                    }
                }

                rowIndex++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string BuildScheduleFileName(string propertyName, DateTime weekStart, string extension)
        {
            var rawName = $"Schedule_{propertyName}_{weekStart:yyyyMMdd}";
            var invalid = Path.GetInvalidFileNameChars();
            var safeName = new string(rawName
                .Select(c => invalid.Contains(c) ? '_' : c)
                .ToArray());
            return $"{safeName}.{extension}";
        }

        private static TimeOffRequestListItemViewModel MapRequest(ScheduleTimeOffRequest request)
        {
            return new TimeOffRequestListItemViewModel
            {
                Id = request.Id,
                EmployeeName = request.Employee.DisplayName,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Reason = request.Reason,
                Status = request.Status,
                SubmittedAtUtc = request.SubmittedAtUtc,
                SubmittedByName = request.SubmittedByUser != null
                    ? $"{request.SubmittedByUser.FirstName} {request.SubmittedByUser.LastName}".Trim()
                    : request.SubmittedByUserId,
                DecisionByName = request.DecisionByUser != null
                    ? $"{request.DecisionByUser.FirstName} {request.DecisionByUser.LastName}".Trim()
                    : null,
                DecisionAtUtc = request.DecisionAtUtc,
                DecisionNotes = request.DecisionNotes
            };
        }
    }
}


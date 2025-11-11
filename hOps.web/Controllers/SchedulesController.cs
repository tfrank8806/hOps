using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
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

        public SchedulesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUserTimeZoneService timeZoneService,
            SchedulePublicationService publicationService,
            IEmailSender emailSender,
            ILogger<SchedulesController> logger)
            : base(context, userManager)
        {
            _timeZoneService = timeZoneService;
            _publicationService = publicationService;
            _emailSender = emailSender;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? weekStart = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                ViewData["Title"] = "Schedules";
                return View(new SchedulePageViewModel
                {
                    AlertMessage = "Select a property to view schedules."
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
                        StartTime = t.StartTime,
                        EndTime = t.EndTime
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
                AssignmentForm = new ScheduleAssignmentFormViewModel
                {
                    ScheduleId = schedule?.Id ?? 0,
                    ShiftDate = alignedWeekStart,
                    ShiftName = shiftTemplates.FirstOrDefault()?.Name ?? "Shift",
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
                vm.EmployeeRows = BuildEmployeeRows(schedule, vm.DayColumns, approvedRequestsForWeek);
            }

            ViewData["Title"] = "Schedules";
            return View(vm);
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
                form.ShiftName = selectedTemplate.Name;
            }

            var (startTime, endTime) = ParseShiftTimes(form, selectedTemplate);
            var normalizedShiftName = string.IsNullOrWhiteSpace(form.ShiftName) ? "Shift" : form.ShiftName.Trim();

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
                        Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim()
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
                form.ShiftName = selectedTemplate.Name;
            }

            var (startTime, endTime) = ParseShiftTimes(form, selectedTemplate);
            var normalizedShiftName = string.IsNullOrWhiteSpace(form.ShiftName) ? "Shift" : form.ShiftName.Trim();

            assignment.ScheduleEmployeeId = employee.Id;
            assignment.ShiftDate = DateTime.SpecifyKind(form.ShiftDate.Date, DateTimeKind.Utc);
            assignment.ShiftName = normalizedShiftName;
            assignment.ShiftStartTime = startTime;
            assignment.ShiftEndTime = endTime;
            assignment.Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();

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
            IReadOnlyList<ScheduleTimeOffRequest> approvedRequests)
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

            var employeeIds = assignmentsByEmployee.Keys
                .Union(approvedRequests.Select(r => r.ScheduleEmployeeId))
                .Distinct()
                .ToList();

            foreach (var employeeId in employeeIds)
            {
                var employee = assignmentsByEmployee.TryGetValue(employeeId, out var assignmentList)
                    ? assignmentList.First().Employee
                    : approvedRequests.First(r => r.ScheduleEmployeeId == employeeId).Employee;

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
                            Notes = a.Notes
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

            }

            return rows.OrderBy(r => r.EmployeeName).ToList();
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

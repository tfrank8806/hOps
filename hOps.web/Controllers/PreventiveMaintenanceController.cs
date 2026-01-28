using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.PreventiveMaintenance;
using hOps.web.ViewModels.WorkOrders;
using hOps.web.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using hOps.web.Utilities;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class PreventiveMaintenanceController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IUserTimeZoneService _timeZoneService;
        private readonly ILogger<PreventiveMaintenanceController> _logger;

        public PreventiveMaintenanceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUserTimeZoneService timeZoneService,
            ILogger<PreventiveMaintenanceController> logger) : base(context, userManager)
        {
            _db = context;
            _timeZoneService = timeZoneService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["PmError"] = "Select a property to access Preventative Maintenance.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var propertyId = property.Id;
            var setting = await _db.PreventiveMaintenanceSettings.AsNoTracking().FirstOrDefaultAsync(s => s.PropertyId == propertyId);
            var frequency = setting?.FrequencyPerYear ?? 0;
            var hasChecklist = await _db.PreventiveMaintenanceTasks.AnyAsync(t => t.PropertyId == propertyId);

            var roomOptions = await _db.Rooms
                .Where(r => r.PropertyId == propertyId && r.IncludeInPreventiveMaintenance)
                .OrderBy(r => r.RoomNumber)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(r.RoomNumber) ? $"Room {r.Id}" : r.RoomNumber
                })
                .ToListAsync();

            var activeSession = await _db.PreventiveMaintenanceSessions
                .Include(s => s.Tasks)
                .Include(s => s.Room)
                .Where(s => s.PropertyId == propertyId &&
                            s.CreatedById == user.Id &&
                            s.Status != PreventiveMaintenanceSessionStatus.Completed &&
                            s.Status != PreventiveMaintenanceSessionStatus.Cancelled)
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefaultAsync();

            var cycleWindows = MaintenanceScheduleHelper.BuildCycleWindows(DateTime.UtcNow, frequency);
            var cycleDefinitions = cycleWindows
                .Select(window => new MaintenanceCycleDefinitionViewModel
                {
                    Index = window.Index,
                    DueDate = window.DueDate,
                    Label = $"Cycle {window.Index}"
                })
                .ToList();

            var roomLogs = await BuildRoomLogsAsync(propertyId, frequency, cycleWindows);

            var viewModel = new PreventiveMaintenanceIndexViewModel
            {
                PropertyId = propertyId,
                PropertyName = property.Name,
                FrequencyPerYear = frequency,
                RoomOptions = roomOptions,
                HasChecklist = hasChecklist,
                RoomLogs = roomLogs,
                ActiveSession = activeSession != null
                    ? BuildActiveSessionViewModel(activeSession, DateTime.UtcNow)
                    : null,
                CycleDefinitions = cycleDefinitions
            };

            ViewBag.LocalNow = _timeZoneService.ConvertToUserTime(DateTime.UtcNow);
            ViewBag.PmError = TempData["PmError"] ?? TempData["PmSetupError"];
            ViewBag.PmMessage = TempData["PmSetupMessage"];

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Archive(int? year = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["PmError"] = "Select a property to view Preventative Maintenance archives.";
                return RedirectToAction("Index", "Home");
            }

            var propertyId = property.Id;
            var sessionsQuery = _db.PreventiveMaintenanceSessions
                .AsNoTracking()
                .Where(s => s.PropertyId == propertyId && s.Status == PreventiveMaintenanceSessionStatus.Completed);

            var availableYears = await sessionsQuery
                .Select(s => (s.CompletedAtUtc ?? s.StartedAtUtc).Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!availableYears.Any())
            {
                var emptyModel = new MaintenanceArchiveViewModel
                {
                    PageTitle = "Preventive Maintenance Archive",
                    PropertyId = propertyId,
                    PropertyName = property.Name,
                    SelectedYear = DateTime.UtcNow.Year,
                    AvailableYears = Array.Empty<int>(),
                    Rooms = Array.Empty<MaintenanceArchiveRoomViewModel>()
                };
                return View("Archive", emptyModel);
            }

            var defaultYear = DateTime.UtcNow.Year - 1;
            var selectedYear = year.HasValue && availableYears.Contains(year.Value)
                ? year.Value
                : (availableYears.Contains(defaultYear) ? defaultYear : availableYears.First());

            var yearStart = new DateTime(selectedYear, 1, 1);
            var yearEnd = new DateTime(selectedYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

            var sessions = await sessionsQuery
                .Where(s => (s.CompletedAtUtc ?? s.StartedAtUtc) >= yearStart &&
                            (s.CompletedAtUtc ?? s.StartedAtUtc) <= yearEnd)
                .OrderBy(s => s.CompletedAtUtc ?? s.StartedAtUtc)
                .ToListAsync();

            var rooms = await _db.Rooms
                .AsNoTracking()
                .Where(r => r.PropertyId == propertyId && r.IncludeInPreventiveMaintenance)
                .OrderBy(r => r.RoomNumber)
                .ToListAsync();

            var roomEntries = new List<MaintenanceArchiveRoomViewModel>();
            foreach (var room in rooms)
            {
                var roomSessions = sessions
                    .Where(s => s.RoomId == room.Id)
                    .Select(s => s.CompletedAtUtc ?? s.StartedAtUtc)
                    .Where(d => d != DateTime.MinValue)
                    .OrderByDescending(d => d)
                    .ToList();

                if (roomSessions.Any())
                {
                    roomEntries.Add(new MaintenanceArchiveRoomViewModel
                    {
                        RoomNumber = room.RoomNumber ?? $"Room {room.Id}",
                        CompletionDates = roomSessions
                    });
                }
            }

            var manualEntries = sessions
                .Where(s => !s.RoomId.HasValue && !string.IsNullOrWhiteSpace(s.RoomNumber))
                .GroupBy(s => s.RoomNumber!.Trim())
                .Select(g => new MaintenanceArchiveRoomViewModel
                {
                    RoomNumber = $"Manual: {g.Key}",
                    CompletionDates = g.Select(s => s.CompletedAtUtc ?? s.StartedAtUtc)
                        .Where(d => d != DateTime.MinValue)
                        .OrderByDescending(d => d)
                        .ToList()
                })
                .Where(entry => entry.CompletionDates.Any())
                .ToList();

            roomEntries.AddRange(manualEntries);

            var archiveModel = new MaintenanceArchiveViewModel
            {
                PageTitle = "Preventive Maintenance Archive",
                PropertyId = propertyId,
                PropertyName = property.Name,
                SelectedYear = selectedYear,
                AvailableYears = availableYears,
                Rooms = roomEntries
            };

            return View("Archive", archiveModel);
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> StartSession([FromBody] PmSessionStartRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { message = "Select a property before starting a PM." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var propertyId = property.Id;
            var hasChecklist = await _db.PreventiveMaintenanceTasks.AnyAsync(t => t.PropertyId == propertyId);
            if (!hasChecklist)
            {
                return BadRequest(new { message = "Set up the PM checklist before starting a session." });
            }

            var existingActive = await _db.PreventiveMaintenanceSessions
                .AnyAsync(s => s.PropertyId == propertyId &&
                               s.CreatedById == user.Id &&
                               s.Status != PreventiveMaintenanceSessionStatus.Completed &&
                               s.Status != PreventiveMaintenanceSessionStatus.Cancelled);
            if (existingActive)
            {
                return Conflict(new { message = "You already have a PM in progress. Finish or pause it before starting another." });
            }

            Room? selectedRoom = null;
            string roomNumber = request.RoomNumber?.Trim() ?? string.Empty;
            if (request.RoomId.HasValue && request.RoomId.Value > 0)
            {
                selectedRoom = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId.Value && r.PropertyId == propertyId);
                if (selectedRoom == null)
                {
                    return BadRequest(new { message = "The selected room is not available for this property." });
                }

                roomNumber = selectedRoom.RoomNumber ?? $"Room {selectedRoom.Id}";
            }

            if (string.IsNullOrWhiteSpace(roomNumber))
            {
                return BadRequest(new { message = "Enter a room number to start the PM." });
            }
            roomNumber = roomNumber.Length > 32 ? roomNumber[..32] : roomNumber;

            var startUtc = DateTime.SpecifyKind(request.StartedAtUtc, DateTimeKind.Utc);
            var nowUtc = DateTime.UtcNow;
            if (startUtc > nowUtc.AddMinutes(5))
            {
                startUtc = nowUtc;
            }

            var tasks = await _db.PreventiveMaintenanceTasks
                .Where(t => t.PropertyId == propertyId)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();

            if (!tasks.Any())
            {
                return BadRequest(new { message = "Add at least one task to the PM checklist." });
            }

            var session = new PreventiveMaintenanceSession
            {
                PropertyId = propertyId,
                RoomId = selectedRoom?.Id,
                RoomNumber = roomNumber,
                CreatedById = user.Id,
                StartedAtUtc = startUtc,
                Status = PreventiveMaintenanceSessionStatus.InProgress,
                LastResumedAtUtc = startUtc,
                LastSavedAtUtc = nowUtc
            };

            foreach (var template in tasks)
            {
                session.Tasks.Add(new PreventiveMaintenanceSessionTask
                {
                    TemplateTaskId = template.Id,
                    TaskName = template.Name,
                    TaskDescription = template.Description,
                    SortOrder = template.SortOrder
                });
            }

            _db.PreventiveMaintenanceSessions.Add(session);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                session = BuildSessionDto(session, nowUtc)
            });
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> UpdateTask([FromBody] PmTaskUpdateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { message = "Select a property before updating tasks." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var session = await _db.PreventiveMaintenanceSessions
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId &&
                                          s.PropertyId == property.Id &&
                                          s.CreatedById == user.Id);

            if (session == null)
            {
                return NotFound(new { message = "PM session not found." });
            }

            if (session.Status == PreventiveMaintenanceSessionStatus.Completed)
            {
                return BadRequest(new { message = "This PM has already been completed." });
            }

            var task = session.Tasks.FirstOrDefault(t => t.Id == request.TaskId);
            if (task == null)
            {
                return NotFound(new { message = "The selected checklist task could not be found." });
            }

            var nowUtc = DateTime.UtcNow;
            task.Status = request.Status;
            task.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            task.CompletedAtUtc = request.Status == PreventiveMaintenanceTaskStatus.Complete ||
                                  request.Status == PreventiveMaintenanceTaskStatus.ReportIssue
                ? nowUtc
                : null;
            task.UpdatedAtUtc = nowUtc;
            session.LastSavedAtUtc = nowUtc;

            await _db.SaveChangesAsync();

            return Ok(new { session = BuildSessionDto(session, nowUtc) });
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> PauseSession([FromBody] PmSessionCommandRequest request)
        {
            return await UpdateSessionStatusAsync(request, PreventiveMaintenanceSessionStatus.Paused);
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> ResumeSession([FromBody] PmSessionCommandRequest request)
        {
            return await UpdateSessionStatusAsync(request, PreventiveMaintenanceSessionStatus.InProgress);
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> FinishSession([FromBody] PmSessionCommandRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { message = "Select a property before finishing the PM." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var session = await _db.PreventiveMaintenanceSessions
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId &&
                                          s.PropertyId == property.Id &&
                                          s.CreatedById == user.Id);

            if (session == null)
            {
                return NotFound(new { message = "PM session not found." });
            }

            if (session.Status == PreventiveMaintenanceSessionStatus.Completed)
            {
                return BadRequest(new { message = "This PM is already completed." });
            }

            if (session.Tasks.Any(t => t.Status != PreventiveMaintenanceTaskStatus.Complete &&
                                       t.Status != PreventiveMaintenanceTaskStatus.ReportIssue))
            {
                return BadRequest(new { message = "Update every task to Complete or Report an Issue before finishing." });
            }

            var nowUtc = DateTime.UtcNow;
            CaptureElapsedTime(session, nowUtc);
            session.Status = PreventiveMaintenanceSessionStatus.Completed;
            session.CompletedAtUtc = nowUtc;
            session.CompletedById = user.Id;
            session.LastResumedAtUtc = null;
            session.LastSavedAtUtc = nowUtc;

            await _db.SaveChangesAsync();

            var workOrdersCreated = await CreateWorkOrdersForIssuesAsync(session, user);

            return Ok(new
            {
                session = BuildSessionDto(session, nowUtc),
                issuesLogged = workOrdersCreated
            });
        }

        private async Task<IActionResult> UpdateSessionStatusAsync(PmSessionCommandRequest request, PreventiveMaintenanceSessionStatus targetStatus)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { message = "Select a property first." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var session = await _db.PreventiveMaintenanceSessions
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId &&
                                          s.PropertyId == property.Id &&
                                          s.CreatedById == user.Id);

            if (session == null)
            {
                return NotFound(new { message = "PM session not found." });
            }

            var nowUtc = DateTime.UtcNow;

            if (targetStatus == PreventiveMaintenanceSessionStatus.Paused)
            {
                if (session.Status != PreventiveMaintenanceSessionStatus.InProgress)
                {
                    return BadRequest(new { message = "Only active PMs can be paused." });
                }

                CaptureElapsedTime(session, nowUtc);
                session.Status = PreventiveMaintenanceSessionStatus.Paused;
                session.PausedAtUtc = nowUtc;
                session.LastResumedAtUtc = null;
            }
            else if (targetStatus == PreventiveMaintenanceSessionStatus.InProgress)
            {
                if (session.Status != PreventiveMaintenanceSessionStatus.Paused)
                {
                    return BadRequest(new { message = "Only paused PMs can be resumed." });
                }

                session.Status = PreventiveMaintenanceSessionStatus.InProgress;
                session.PausedAtUtc = null;
                session.LastResumedAtUtc = nowUtc;
            }

            session.LastSavedAtUtc = nowUtc;
            await _db.SaveChangesAsync();

            return Ok(new { session = BuildSessionDto(session, nowUtc) });
        }

        private async Task<int> CreateWorkOrdersForIssuesAsync(PreventiveMaintenanceSession session, ApplicationUser user)
        {
            var issueTasks = session.Tasks
                .Where(t => t.Status == PreventiveMaintenanceTaskStatus.ReportIssue)
                .ToList();

            if (!issueTasks.Any())
            {
                return 0;
            }

            var createdCount = 0;
            foreach (var issue in issueTasks)
            {
                try
                {
                    var workOrder = new WorkOrder
                    {
                        Status = WorkOrderStatusOptions.DefaultStatus,
                        Issue = Truncate($"PM Issue - {issue.TaskName}", 256),
                        Details = BuildIssueDetails(session, issue),
                        CreatedAt = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.Date.AddDays(1),
                        CreatedById = user.Id,
                        Location = session.RoomNumber
                    };

                    workOrder.Properties.Add(new WorkOrderProperty
                    {
                        PropertyId = session.PropertyId,
                        WorkOrder = workOrder
                    });

                    _db.WorkOrders.Add(workOrder);
                    await _db.SaveChangesAsync();
                    createdCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate a work order for PM session {SessionId} task {TaskId}", session.Id, issue.Id);
                }
            }

            return createdCount;
        }

        private static string BuildIssueDetails(PreventiveMaintenanceSession session, PreventiveMaintenanceSessionTask task)
        {
            var parts = new List<string>
            {
                $"Room: {session.RoomNumber}",
                $"Task: {task.TaskName}"
            };

            if (!string.IsNullOrWhiteSpace(task.TaskDescription))
            {
                parts.Add($"Checklist detail: {task.TaskDescription}");
            }

            if (!string.IsNullOrWhiteSpace(task.Notes))
            {
                parts.Add($"Notes: {task.Notes}");
            }

            return string.Join(Environment.NewLine, parts);
        }

        private async Task<List<PreventiveMaintenanceRoomLogViewModel>> BuildRoomLogsAsync(int propertyId, int frequencyPerYear, IReadOnlyList<MaintenanceScheduleHelper.MaintenanceCycleWindow> cycleWindows)
        {
            var rooms = await _db.Rooms
                .Where(r => r.PropertyId == propertyId && r.IncludeInPreventiveMaintenance)
                .OrderBy(r => r.RoomNumber)
                .ToListAsync();

            var sessions = await _db.PreventiveMaintenanceSessions
                .Include(s => s.CompletedBy)
                .Where(s => s.PropertyId == propertyId && s.Status == PreventiveMaintenanceSessionStatus.Completed)
                .OrderByDescending(s => s.CompletedAtUtc)
                .ToListAsync();

            var currentYear = DateTime.UtcNow.Year;
            var latestLookup = new Dictionary<string, PreventiveMaintenanceSession>(StringComparer.OrdinalIgnoreCase);
            var completionHistory = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
            foreach (var session in sessions)
            {
                var completedAt = session.CompletedAtUtc ?? session.StartedAtUtc;
                var key = session.RoomId.HasValue
                    ? $"room:{session.RoomId.Value}"
                    : $"manual:{(session.RoomNumber ?? string.Empty).Trim()}";

                if (completedAt.Year == currentYear && !latestLookup.ContainsKey(key))
                {
                    latestLookup[key] = session;
                }

                if (!completionHistory.TryGetValue(key, out var history))
                {
                    history = new List<DateTime>();
                    completionHistory[key] = history;
                }

                if (completedAt != DateTime.MinValue && completedAt.Year == currentYear)
                {
                    history.Add(completedAt);
                }
            }

            var logs = new List<PreventiveMaintenanceRoomLogViewModel>();
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                latestLookup.TryGetValue($"room:{room.Id}", out var session);
                completionHistory.TryGetValue($"room:{room.Id}", out var history);
                logs.Add(BuildRoomLog(room.Id, room.RoomNumber ?? $"Room {room.Id}", session, frequencyPerYear, i, cycleWindows, history));
            }

            var manualLogs = latestLookup
                .Where(kvp => kvp.Key.StartsWith("manual:", StringComparison.OrdinalIgnoreCase))
                .Select((kvp, index) =>
                {
                    completionHistory.TryGetValue(kvp.Key, out var history);
                    return BuildRoomLog(null, kvp.Value.RoomNumber, kvp.Value, frequencyPerYear, rooms.Count + index, cycleWindows, history);
                })
                .Take(20);

            logs.AddRange(manualLogs);

            return logs
                .OrderBy(l => l.RoomNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static PreventiveMaintenanceRoomLogViewModel BuildRoomLog(int? roomId, string? roomNumber, PreventiveMaintenanceSession? session, int frequencyPerYear, int roomIndex, IReadOnlyList<MaintenanceScheduleHelper.MaintenanceCycleWindow> cycleWindows, List<DateTime>? completionHistory)
        {
            var log = new PreventiveMaintenanceRoomLogViewModel
            {
                RoomId = roomId,
                RoomNumber = string.IsNullOrWhiteSpace(roomNumber) ? (roomId.HasValue ? $"Room {roomId}" : "Room") : roomNumber!,
                LastDurationSeconds = session?.TotalDurationSeconds,
                CompletedByName = session?.CompletedAtUtc.HasValue == true && session.CompletedAtUtc.Value.Year == DateTime.UtcNow.Year
                    ? BuildUserName(session?.CompletedBy)
                    : null
            };

            if (completionHistory != null && completionHistory.Count > 0)
            {
                var latest = completionHistory
                    .OrderByDescending(d => d)
                    .FirstOrDefault();
                if (latest != default)
                {
                    log.LastCompletedAtUtc = latest;
                }
            }

            var nextDue = MaintenanceScheduleHelper.CalculateNextDueDate(session?.CompletedAtUtc, frequencyPerYear, roomIndex);
            log.NextDueAtUtc = nextDue;
            if (nextDue.HasValue)
            {
                var now = DateTime.UtcNow.Date;
                var dueDate = nextDue.Value.Date;
                if (dueDate <= now)
                {
                    log.IsOverdue = true;
                    log.IsDue = true;
                }
                else if (dueDate <= now.AddDays(7))
                {
                    log.IsDue = true;
                    log.IsOverdue = false;
                }
            }

            var statuses = new List<MaintenanceCycleStatusViewModel>();
            if (cycleWindows != null && cycleWindows.Count > 0)
            {
                var completions = completionHistory?.OrderBy(d => d).ToList() ?? new List<DateTime>();
                foreach (var window in cycleWindows)
                {
                    DateTime? completion = null;
                    for (var i = 0; i < completions.Count; i++)
                    {
                        var candidate = completions[i];
                        if (candidate >= window.StartDate && candidate <= window.DueDate)
                        {
                            completion = candidate;
                            completions.RemoveAt(i);
                            break;
                        }
                    }

                    statuses.Add(new MaintenanceCycleStatusViewModel
                    {
                        Index = window.Index,
                        DueDate = window.DueDate,
                        CompletedAt = completion
                    });
                }
            }

            log.CycleStatuses = statuses;
            return log;
        }

        private static PreventiveMaintenanceActiveSessionViewModel BuildActiveSessionViewModel(PreventiveMaintenanceSession session, DateTime asOfUtc)
        {
            return new PreventiveMaintenanceActiveSessionViewModel
            {
                SessionId = session.Id,
                RoomNumber = session.RoomNumber,
                RoomLabel = session.Room?.RoomNumber ?? session.RoomNumber,
                StartedAtUtc = session.StartedAtUtc,
                Status = session.Status,
                TotalDurationSeconds = GetEffectiveDurationSeconds(session, asOfUtc),
                Tasks = session.Tasks
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.Id)
                    .Select(t => new PreventiveMaintenanceActiveSessionTaskViewModel
                    {
                        TaskId = t.Id,
                        Title = t.TaskName,
                        Description = t.TaskDescription,
                        Status = t.Status,
                        Notes = t.Notes
                    })
                    .ToList()
            };
        }

        private static object BuildSessionDto(PreventiveMaintenanceSession session, DateTime asOfUtc)
        {
            return new
            {
                id = session.Id,
                roomNumber = session.RoomNumber,
                roomLabel = session.Room?.RoomNumber ?? session.RoomNumber,
                status = session.Status.ToString(),
                startedAtUtc = session.StartedAtUtc,
                durationSeconds = GetEffectiveDurationSeconds(session, asOfUtc),
                tasks = session.Tasks
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.Id)
                    .Select(t => new
                    {
                        id = t.Id,
                        title = t.TaskName,
                        description = t.TaskDescription,
                        status = t.Status.ToString(),
                        notes = t.Notes
                    })
            };
        }

        private static double GetEffectiveDurationSeconds(PreventiveMaintenanceSession session, DateTime asOfUtc)
        {
            var total = session.TotalDurationSeconds;
            if (session.Status == PreventiveMaintenanceSessionStatus.InProgress && session.LastResumedAtUtc.HasValue)
            {
                var incremental = (asOfUtc - session.LastResumedAtUtc.Value).TotalSeconds;
                if (incremental > 0)
                {
                    total += incremental;
                }
            }

            return Math.Max(0, total);
        }

        private static void CaptureElapsedTime(PreventiveMaintenanceSession session, DateTime asOfUtc)
        {
            if (!session.LastResumedAtUtc.HasValue)
            {
                return;
            }

            var elapsed = (asOfUtc - session.LastResumedAtUtc.Value).TotalSeconds;
            if (elapsed > 0)
            {
                session.TotalDurationSeconds += elapsed;
            }

            session.LastResumedAtUtc = asOfUtc;
        }

        private static string? BuildUserName(ApplicationUser? user)
        {
            if (user == null)
            {
                return null;
            }

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }

            return user.UserName;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}

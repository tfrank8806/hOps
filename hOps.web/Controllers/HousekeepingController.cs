using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Housekeeping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class HousekeepingController : BaseController
    {
        private readonly MentionService _mentionService;
        private readonly IPassOnLogNotificationService _notificationService;
        private readonly IUserTimeZoneService _timeZoneService;
        private readonly ILogger<HousekeepingController> _logger;
        private const string MissingMprSchemaMessage = "The MPR Tracker database tables are missing. Run the AddHousekeepingMprTracker migration (e.g. `dotnet ef database update`) and reload this page.";

        public HousekeepingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MentionService mentionService,
            IPassOnLogNotificationService notificationService,
            IUserTimeZoneService timeZoneService,
            ILogger<HousekeepingController> logger)
            : base(context, userManager)
        {
            _mentionService = mentionService;
            _notificationService = notificationService;
            _timeZoneService = timeZoneService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult DailyRecap()
        {
            var localDate = _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;
            var model = DailyRecapViewModel.CreateDefault(localDate);
            model.EnsureCollectionIntegrity();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MprTracker(int? month, int? year, string? period, DateTime? start, DateTime? end, int? entryId)
        {
            var now = NormalizeToUtcDate(_timeZoneService.ConvertToUserTime(DateTime.UtcNow));
            var model = new MprTrackerViewModel
            {
                EntryDate = now,
                CanEditStandards = UserCanEditMprStandards(),
                CanManageHousekeepers = UserCanEditMprStandards(),
                LogFilter = new MprTrackerLogFilterViewModel
                {
                    SelectedMonth = month ?? 0,
                    SelectedYear = year ?? 0,
                    PeriodType = string.IsNullOrWhiteSpace(period) ? MprTrackerLogFilterViewModel.PeriodMonth : period!,
                    CustomStartDate = start,
                    CustomEndDate = end
                }
            };

            try
            {
                await PopulateMprTrackerAsync(model);
            }
            catch (Exception ex) when (HandleMissingMprSchema(ex, model))
            {
                // Error message added inside handler.
            }

            if (entryId.HasValue && model.ErrorMessage == null)
            {
                var currentProperty = ViewBag.CurrentProperty as Property;
                if (currentProperty != null)
                {
                    await TryLoadMprEntryForEditAsync(model, currentProperty, entryId.Value);
                }
                else
                {
                    model.ErrorMessage = "Select a property before editing a productivity entry.";
                }
            }

            ApplyTempDataMessages(model);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> HousekeeperList(string? period, int? month, int? year, DateTime? start, DateTime? end)
        {
            var model = new MprHousekeeperListViewModel
            {
                CanManageHousekeepers = UserCanEditMprStandards(),
                FilterPeriod = string.IsNullOrWhiteSpace(period) ? null : period,
                FilterMonth = month,
                FilterYear = year,
                FilterStart = start,
                FilterEnd = end
            };

            await PopulateHousekeeperListAsync(model);
            ApplyTempDataMessages(model);

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> MprTracker(MprTrackerViewModel model)
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            var canEditStandards = UserCanEditMprStandards();
            model.CanEditStandards = canEditStandards;
            model.CanManageHousekeepers = canEditStandards;
            model.EntryDate = NormalizeToUtcDate(model.EntryDate == default
                ? _timeZoneService.ConvertToUserTime(DateTime.UtcNow)
                : model.EntryDate);

            if (currentProperty == null)
            {
                ModelState.AddModelError(string.Empty, "Select a property to track housekeeping productivity.");
            }

            if (!canEditStandards)
            {
                ModelState.Remove(nameof(model.DepartureStandardMinutes));
                ModelState.Remove(nameof(model.LinenChangeStandardMinutes));
                ModelState.Remove(nameof(model.StayoverStandardMinutes));
                model.ResetStandardsToDefaults();
            }

            if (!model.SelectedHousekeeperId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SelectedHousekeeperId), "Select a housekeeper before saving.");
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    await PopulateMprTrackerAsync(model, currentProperty);
                    return View(model);
                }

                model.Calculate();
                var preservedEntryDate = model.EntryDate;

                if (currentProperty == null)
                {
                    await PopulateMprTrackerAsync(model);
                    return View(model);
                }

                var housekeeper = await _context.HousekeeperProfiles
                    .FirstOrDefaultAsync(h => h.Id == model.SelectedHousekeeperId && h.PropertyId == currentProperty.Id && !h.IsDeleted);

                if (housekeeper == null)
                {
                    ModelState.AddModelError(nameof(model.SelectedHousekeeperId), "The selected housekeeper could not be found.");
                    await PopulateMprTrackerAsync(model, currentProperty);
                    return View(model);
                }

                HousekeepingMprEntry? entry = null;
                if (model.EditingEntryId.HasValue)
                {
                    entry = await _context.HousekeepingMprEntries
                        .FirstOrDefaultAsync(e => e.Id == model.EditingEntryId && e.PropertyId == currentProperty.Id);

                    if (entry == null)
                    {
                        ModelState.AddModelError(string.Empty, "The entry you attempted to edit could not be found.");
                        await PopulateMprTrackerAsync(model, currentProperty);
                        return View(model);
                    }
                }

                var currentUser = await _userManager.GetUserAsync(User);
                var isNewEntry = entry == null;
                if (entry == null)
                {
                    entry = new HousekeepingMprEntry
                    {
                        PropertyId = currentProperty.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = currentUser?.Id
                    };

                    _context.HousekeepingMprEntries.Add(entry);
                }

                entry.HousekeeperId = housekeeper.Id;
                entry.HousekeeperName = housekeeper.Name;
                entry.EntryDate = model.EntryDate;
                entry.CheckoutRooms = model.CheckoutRooms;
                entry.LinenChangeRooms = model.LinenChangeRooms;
                entry.StayoverRooms = model.StayoverRooms;
                entry.DeepCleanRooms = model.DeepCleanRooms;
                entry.DndRooms = model.DndRooms;
                entry.HoursWorked = model.HoursWorked;
                entry.TotalMinutesWorked = model.TotalMinutesWorked ?? 0;
                entry.MinutesPerRoom = model.MinutesPerRoom;
                entry.DepartureStandardMinutes = model.DepartureStandardMinutes;
                entry.LinenChangeStandardMinutes = model.LinenChangeStandardMinutes;
                entry.StayoverStandardMinutes = model.StayoverStandardMinutes;
                entry.DeepCleanStandardMinutes = model.DeepCleanStandardMinutes;

                await _context.SaveChangesAsync();

                model.EntrySaved = true;
                model.EditingEntryId = null;
                model.EditingHousekeeperName = null;
                model.EditingEntryDate = null;
                model.StatusMessage = isNewEntry
                    ? $"Saved entry for {housekeeper.Name} on {model.EntryDate:MMM d}."
                    : $"Updated entry for {housekeeper.Name} on {model.EntryDate:MMM d}.";

                await PopulateMprTrackerAsync(model, currentProperty);
                model.ResetEntryInputs(preservedEntryDate);
                ModelState.Clear();
                return View(model);
            }
            catch (Exception ex) when (HandleMissingMprSchema(ex, model))
            {
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddHousekeeper(string name, string? period, int? month, int? year, DateTime? start, DateTime? end, bool? returnToList)
        {
            var routeValues = BuildFilterRouteValues(period, month, year, start, end);
            var redirectAction = returnToList == true ? nameof(HousekeeperList) : nameof(MprTracker);

            if (!UserCanEditMprStandards())
            {
                TempData["MprTrackerError"] = "Only managers can manage housekeeper names.";
                return RedirectToAction(redirectAction, routeValues);
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                TempData["MprTrackerError"] = "Select a property before managing housekeeper names.";
                return RedirectToAction(redirectAction, routeValues);
            }

            var trimmedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                TempData["MprTrackerError"] = "Enter a housekeeper name before adding it.";
                return RedirectToAction(redirectAction, routeValues);
            }

            try
            {
                var normalized = trimmedName.ToUpperInvariant();
                var existing = await _context.HousekeeperProfiles
                    .FirstOrDefaultAsync(h => h.PropertyId == currentProperty.Id && h.Name.ToUpper() == normalized);

                if (existing != null)
                {
                    if (existing.IsDeleted)
                    {
                        existing.IsDeleted = false;
                        existing.DeletedAt = null;
                        await _context.SaveChangesAsync();
                        TempData["MprTrackerStatus"] = $"Restored {existing.Name} to the list.";
                    }
                    else
                    {
                        TempData["MprTrackerError"] = $"{existing.Name} is already listed.";
                    }

                    return RedirectToAction(redirectAction, routeValues);
                }

                var profile = new HousekeeperProfile
                {
                    PropertyId = currentProperty.Id,
                    Name = trimmedName,
                    CreatedAt = DateTime.UtcNow
                };

                _context.HousekeeperProfiles.Add(profile);
                await _context.SaveChangesAsync();

                TempData["MprTrackerStatus"] = $"Added {profile.Name} to the list.";
                return RedirectToAction(redirectAction, routeValues);
            }
            catch (Exception ex) when (HandleMissingMprSchema(ex))
            {
                return RedirectToAction(redirectAction, routeValues);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHousekeeper(int id, string? period, int? month, int? year, DateTime? start, DateTime? end, bool? returnToList)
        {
            var routeValues = BuildFilterRouteValues(period, month, year, start, end);
            var redirectAction = returnToList == true ? nameof(HousekeeperList) : nameof(MprTracker);

            if (!UserCanEditMprStandards())
            {
                TempData["MprTrackerError"] = "Only managers can delete housekeeper names.";
                return RedirectToAction(redirectAction, routeValues);
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                TempData["MprTrackerError"] = "Select a property before managing housekeeper names.";
                return RedirectToAction(redirectAction, routeValues);
            }

            try
            {
                var housekeeper = await _context.HousekeeperProfiles
                    .FirstOrDefaultAsync(h => h.Id == id && h.PropertyId == currentProperty.Id);

                if (housekeeper == null)
                {
                    TempData["MprTrackerError"] = "The selected housekeeper could not be found.";
                    return RedirectToAction(redirectAction, routeValues);
                }

                housekeeper.IsDeleted = true;
                housekeeper.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                TempData["MprTrackerStatus"] = $"Removed {housekeeper.Name} from the dropdown.";
                return RedirectToAction(redirectAction, routeValues);
            }
            catch (Exception ex) when (HandleMissingMprSchema(ex))
            {
                return RedirectToAction(redirectAction, routeValues);
            }
        }

        private bool UserCanEditMprStandards()
        {
            return User.IsInRole("Manager") || User.IsInRole("Admin");
        }

        private object BuildFilterRouteValues(string? period, int? month, int? year, DateTime? start, DateTime? end)
        {
            return new
            {
                period = string.IsNullOrWhiteSpace(period) ? null : period,
                month,
                year,
                start = start.HasValue ? start.Value.ToString("yyyy-MM-dd") : null,
                end = end.HasValue ? end.Value.ToString("yyyy-MM-dd") : null
            };
        }

        private void ApplyTempDataMessages(MprTrackerViewModel model)
        {
            if (TempData.TryGetValue("MprTrackerStatus", out var status) && status is string statusMessage)
            {
                model.StatusMessage = statusMessage;
            }

            if (TempData.TryGetValue("MprTrackerError", out var error) && error is string errorMessage)
            {
                model.ErrorMessage = errorMessage;
            }
        }

        private void ApplyTempDataMessages(MprHousekeeperListViewModel model)
        {
            if (TempData.TryGetValue("MprTrackerStatus", out var status) && status is string statusMessage)
            {
                model.StatusMessage = statusMessage;
            }

            if (TempData.TryGetValue("MprTrackerError", out var error) && error is string errorMessage)
            {
                model.ErrorMessage = errorMessage;
            }
        }

        private async Task PopulateMprTrackerAsync(MprTrackerViewModel model, Property? currentProperty = null)
        {
            currentProperty ??= ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                model.ErrorMessage ??= "Select a property to manage housekeeping productivity.";
                model.Housekeepers = new List<HousekeeperOptionViewModel>();
                model.LogRows = new List<MprTrackerLogRowViewModel>();
                model.LogDates = new List<DateTime>();
                model.LogDailyTotals = new Dictionary<DateTime, MprTrackerLogSummaryViewModel>();
                model.LogOverallTotals = new MprTrackerLogSummaryViewModel();
                return;
            }

            var propertyId = currentProperty.Id;
            var allHousekeepers = await _context.HousekeeperProfiles
                .Where(h => h.PropertyId == propertyId)
                .OrderBy(h => h.Name)
                .ToListAsync();

            model.Housekeepers = allHousekeepers
                .Where(h => !h.IsDeleted)
                .Select(h => new HousekeeperOptionViewModel
                {
                    Id = h.Id,
                    Name = h.Name,
                    IsDeleted = h.IsDeleted
                })
                .ToList();

            if (!model.SelectedHousekeeperId.HasValue && model.Housekeepers.Any())
            {
                model.SelectedHousekeeperId = model.Housekeepers.First().Id;
            }

            var now = NormalizeToUtcDate(_timeZoneService.ConvertToUserTime(DateTime.UtcNow));
            model.LogFilter ??= new MprTrackerLogFilterViewModel();
            if (model.LogFilter.SelectedYear <= 0)
            {
                model.LogFilter.SelectedYear = now.Year;
            }

            if (model.LogFilter.SelectedMonth <= 0)
            {
                model.LogFilter.SelectedMonth = now.Month;
            }

            var (start, end) = model.LogFilter.GetDateRange(now);
            var totalDays = (end.Date - start.Date).Days;
            model.LogDates = new List<DateTime>();
            for (var offset = 0; offset <= totalDays; offset++)
            {
                var date = start.AddDays(offset);
                model.LogDates.Add(date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc));
            }

            var entries = await _context.HousekeepingMprEntries
                .Where(e => e.PropertyId == propertyId && e.EntryDate >= start.Date && e.EntryDate <= end.Date)
                .Include(e => e.Housekeeper)
                .ToListAsync();

            var logData = BuildLogRows(allHousekeepers, entries, model.LogDates);
            model.LogRows = logData.Rows;
            model.LogDailyTotals = logData.ColumnTotals;
            model.LogOverallTotals = logData.OverallTotals;
        }

        private async Task PopulateHousekeeperListAsync(MprHousekeeperListViewModel model)
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            model.HasPropertySelected = currentProperty != null;

            if (currentProperty == null)
            {
                model.ErrorMessage ??= "Select a property to manage housekeeper names.";
                model.Housekeepers = new List<HousekeeperOptionViewModel>();
                return;
            }

            var propertyId = currentProperty.Id;
            var allHousekeepers = await _context.HousekeeperProfiles
                .Where(h => h.PropertyId == propertyId)
                .OrderBy(h => h.Name)
                .ToListAsync();

            model.Housekeepers = allHousekeepers
                .Where(h => !h.IsDeleted)
                .Select(h => new HousekeeperOptionViewModel
                {
                    Id = h.Id,
                    Name = h.Name,
                    IsDeleted = h.IsDeleted
                })
                .ToList();
        }

        private async Task TryLoadMprEntryForEditAsync(MprTrackerViewModel model, Property property, int entryId)
        {
            var entry = await _context.HousekeepingMprEntries
                .Include(e => e.Housekeeper)
                .FirstOrDefaultAsync(e => e.Id == entryId && e.PropertyId == property.Id);

            if (entry == null)
            {
                model.ErrorMessage = "The selected productivity entry could not be found.";
                return;
            }

            if (entry.HousekeeperId.HasValue && model.Housekeepers.All(h => h.Id != entry.HousekeeperId.Value))
            {
                var name = entry.Housekeeper?.Name ?? entry.HousekeeperName;
                model.Housekeepers.Add(new HousekeeperOptionViewModel
                {
                    Id = entry.HousekeeperId.Value,
                    Name = name,
                    IsDeleted = entry.Housekeeper?.IsDeleted ?? true
                });

                model.Housekeepers = model.Housekeepers
                    .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            model.EditingEntryId = entry.Id;
            model.EditingHousekeeperName = entry.Housekeeper?.Name ?? entry.HousekeeperName;
            model.EditingEntryDate = NormalizeToUtcDate(entry.EntryDate);
            model.SelectedHousekeeperId = entry.HousekeeperId ?? model.SelectedHousekeeperId;
            model.EntryDate = NormalizeToUtcDate(entry.EntryDate);
            model.CheckoutRooms = entry.CheckoutRooms;
            model.LinenChangeRooms = entry.LinenChangeRooms;
            model.StayoverRooms = entry.StayoverRooms;
            model.DeepCleanRooms = entry.DeepCleanRooms;
            model.DndRooms = entry.DndRooms;
            model.HoursWorked = entry.HoursWorked;
            model.DepartureStandardMinutes = entry.DepartureStandardMinutes;
            model.LinenChangeStandardMinutes = entry.LinenChangeStandardMinutes;
            model.StayoverStandardMinutes = entry.StayoverStandardMinutes;
            model.DeepCleanStandardMinutes = entry.DeepCleanStandardMinutes;
            model.Calculate();
        }

        private static MprTrackerLogData BuildLogRows(
            List<HousekeeperProfile> housekeepers,
            IEnumerable<HousekeepingMprEntry> entries,
            List<DateTime> logDates)
        {
            var rows = new Dictionary<string, MprTrackerLogRowViewModel>(StringComparer.OrdinalIgnoreCase);
            var columnTotals = new Dictionary<DateTime, MprTrackerLogSummaryViewModel>();
            foreach (var date in logDates)
            {
                var normalized = date.Date;
                if (!columnTotals.ContainsKey(normalized))
                {
                    columnTotals[normalized] = new MprTrackerLogSummaryViewModel();
                }
            }
            var overallTotals = new MprTrackerLogSummaryViewModel();

            foreach (var housekeeper in housekeepers)
            {
                var key = BuildHousekeeperRowKey(housekeeper.Id, housekeeper.Name);
                if (!rows.ContainsKey(key))
                {
                    rows[key] = new MprTrackerLogRowViewModel
                    {
                        HousekeeperId = housekeeper.Id,
                        HousekeeperName = housekeeper.Name,
                        IsDeleted = housekeeper.IsDeleted
                    };
                }
            }

            foreach (var entry in entries)
            {
                var key = BuildHousekeeperRowKey(entry.HousekeeperId, entry.Housekeeper?.Name ?? entry.HousekeeperName);
                if (!rows.TryGetValue(key, out var row))
                {
                    row = new MprTrackerLogRowViewModel
                    {
                        HousekeeperId = entry.HousekeeperId,
                        HousekeeperName = entry.Housekeeper?.Name ?? entry.HousekeeperName,
                        IsDeleted = entry.Housekeeper?.IsDeleted ?? false
                    };
                    rows[key] = row;
                }

                var day = entry.EntryDate.Date;
                if (!row.Cells.TryGetValue(day, out var cell))
                {
                    cell = new MprTrackerLogCellViewModel();
                    row.Cells[day] = cell;
                }

                cell.CheckoutRooms += entry.CheckoutRooms;
                cell.LinenChangeRooms += entry.LinenChangeRooms;
                cell.StayoverRooms += entry.StayoverRooms;
                cell.DeepCleanRooms += entry.DeepCleanRooms;
                cell.DndRooms += entry.DndRooms;
                cell.TotalMinutesWorked += entry.TotalMinutesWorked;
                if (entry.HoursWorked.HasValue)
                {
                    cell.HoursWorked += entry.HoursWorked.Value;
                    cell.HasRecordedHours = true;
                    row.Summary.HoursWorked += entry.HoursWorked.Value;
                    row.Summary.HasRecordedHours = true;
                }
                else
                {
                    cell.HasPendingHours = true;
                    row.Summary.HasPendingHours = true;
                }
                cell.RecalculateMinutesPerRoom();

                row.Summary.CheckoutRooms += entry.CheckoutRooms;
                row.Summary.LinenChangeRooms += entry.LinenChangeRooms;
                row.Summary.StayoverRooms += entry.StayoverRooms;
                row.Summary.DeepCleanRooms += entry.DeepCleanRooms;
                row.Summary.DndRooms += entry.DndRooms;
                row.Summary.TotalMinutesWorked += entry.HoursWorked.HasValue ? entry.TotalMinutesWorked : 0;

                cell.Entries.Add(new MprTrackerLogEntryViewModel
                {
                    Id = entry.Id,
                    EntryDate = entry.EntryDate,
                    CheckoutRooms = entry.CheckoutRooms,
                    LinenChangeRooms = entry.LinenChangeRooms,
                    StayoverRooms = entry.StayoverRooms,
                    DeepCleanRooms = entry.DeepCleanRooms,
                    DndRooms = entry.DndRooms,
                    HoursWorked = entry.HoursWorked,
                    TotalMinutesWorked = entry.TotalMinutesWorked,
                    MinutesPerRoom = entry.MinutesPerRoom,
                    CreatedAt = entry.CreatedAt
                });

                if (!columnTotals.TryGetValue(day, out var dayTotals))
                {
                    dayTotals = new MprTrackerLogSummaryViewModel();
                    columnTotals[day] = dayTotals;
                }

                ApplyTotals(dayTotals, entry);
                ApplyTotals(overallTotals, entry);
            }

            foreach (var row in rows.Values)
            {
                foreach (var cell in row.Cells.Values)
                {
                    if (cell.Entries.Count > 1)
                    {
                        cell.Entries = cell.Entries
                            .OrderBy(e => e.CreatedAt)
                            .ThenBy(e => e.Id)
                            .ToList();
                    }
                }

                var trackedRooms = row.Summary.CheckoutRooms + row.Summary.StayoverRooms;
                row.Summary.MinutesPerRoom = trackedRooms > 0 && row.Summary.HoursWorked > 0
                    ? Math.Round((row.Summary.HoursWorked * 60m) / trackedRooms, 2, MidpointRounding.AwayFromZero)
                    : null;
            }

            foreach (var totals in columnTotals.Values)
            {
                var trackedRooms = totals.CheckoutRooms + totals.StayoverRooms;
                totals.MinutesPerRoom = trackedRooms > 0 && totals.HoursWorked > 0
                    ? Math.Round((totals.HoursWorked * 60m) / trackedRooms, 2, MidpointRounding.AwayFromZero)
                    : null;
            }

            var overallTrackedRooms = overallTotals.CheckoutRooms + overallTotals.StayoverRooms;
            overallTotals.MinutesPerRoom = overallTrackedRooms > 0 && overallTotals.HoursWorked > 0
                ? Math.Round((overallTotals.HoursWorked * 60m) / overallTrackedRooms, 2, MidpointRounding.AwayFromZero)
                : null;

            return new MprTrackerLogData
            {
                Rows = rows.Values
                    .OrderBy(r => r.HousekeeperName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ColumnTotals = columnTotals,
                OverallTotals = overallTotals
            };
        }

        private static string BuildHousekeeperRowKey(int? id, string name)
        {
            return id.HasValue
                ? $"HK:{id.Value}"
                : $"NAME:{(name ?? string.Empty).Trim().ToUpperInvariant()}";
        }

        private static void ApplyTotals(MprTrackerLogSummaryViewModel summary, HousekeepingMprEntry entry)
        {
            summary.CheckoutRooms += entry.CheckoutRooms;
            summary.LinenChangeRooms += entry.LinenChangeRooms;
            summary.StayoverRooms += entry.StayoverRooms;
            summary.DeepCleanRooms += entry.DeepCleanRooms;
            summary.DndRooms += entry.DndRooms;

            if (entry.HoursWorked.HasValue)
            {
                summary.HoursWorked += entry.HoursWorked.Value;
                summary.TotalMinutesWorked += entry.TotalMinutesWorked;
                summary.HasRecordedHours = true;
            }
            else
            {
                summary.HasPendingHours = true;
            }
        }

        private sealed class MprTrackerLogData
        {
            public List<MprTrackerLogRowViewModel> Rows { get; init; } = new();
            public Dictionary<DateTime, MprTrackerLogSummaryViewModel> ColumnTotals { get; init; } = new();
            public MprTrackerLogSummaryViewModel OverallTotals { get; init; } = new();
        }

        private bool HandleMissingMprSchema(Exception ex, MprTrackerViewModel? model = null)
        {
            if (!IsMissingMprSchemaException(ex))
            {
                return false;
            }

            _logger.LogWarning(ex, "Housekeeping MPR Tracker tables are missing. Prompting user to run migration.");

            if (model != null)
            {
                model.ErrorMessage = MissingMprSchemaMessage;
                model.Housekeepers = new List<HousekeeperOptionViewModel>();
                model.LogRows = new List<MprTrackerLogRowViewModel>();
                model.LogDates = new List<DateTime>();
            }
            else
            {
                TempData["MprTrackerError"] = MissingMprSchemaMessage;
            }

            return true;
        }

        private static bool IsMissingMprSchemaException(Exception ex)
        {
            var root = GetInnermostException(ex);
            if (root is PostgresException postgresException && string.Equals(postgresException.SqlState, "42P01", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (root is SqliteException sqliteException
                && sqliteException.SqliteErrorCode == 1
                && sqliteException.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static Exception GetInnermostException(Exception ex)
        {
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex;
        }

        [HttpPost]
        public async Task<IActionResult> DailyRecap(DailyRecapViewModel model)
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                ModelState.AddModelError(string.Empty, "Select a property before posting a daily recap.");
            }

            model.EnsureCollectionIntegrity();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid || currentProperty == null)
            {
                return View(model);
            }

            var log = new PassOnLog
            {
                Title = BuildLogTitle(model, currentProperty),
                Body = BuildLogBody(model, currentProperty),
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };

            log.Properties.Add(new PassOnLogProperty
            {
                PropertyId = currentProperty.Id
            });

            _context.PassOnLogs.Add(log);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save daily recap log for property {PropertyId}", currentProperty.Id);
                ModelState.AddModelError(string.Empty, "We could not save the daily recap. Please try again.");
                return View(model);
            }

            var link = Url.Action(nameof(PassOnLogsController.Details), "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action(nameof(PassOnLogsController.Details), "PassOnLogs", new { id = log.Id })
                ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                log.Body,
                currentUser,
                $"Pass On Log: {log.Title}",
                link,
                log.Body);

            var recipients = await _notificationService.GetLogEntryAlertRecipientsAsync(log, currentUser);
            await _notificationService.NotifyLogSubscribersAsync(log, currentUser, link, recipients);
            await _notificationService.SendLogEntryEmailsAsync(log, currentUser, link, recipients);

            TempData["DailyRecapCreatedLogId"] = log.Id;
            TempData["DailyRecapCreatedTitle"] = log.Title;

            return RedirectToAction(nameof(DailyRecap));
        }

        private static string BuildLogTitle(DailyRecapViewModel model, Property property)
        {
            var dateSegment = model.ReportDate.HasValue
                ? model.ReportDate.Value.ToString("MMMM d, yyyy")
                : "Daily Recap";

            var propertyName = string.IsNullOrWhiteSpace(property.Name)
                ? $"Property #{property.Id}"
                : property.Name.Trim();

            return $"{propertyName} \u2013 Daily Recap ({dateSegment})";
        }

        private static string BuildLogBody(DailyRecapViewModel model, Property property)
        {
            var builder = new StringBuilder();
            var reportDate = model.ReportDate?.ToString("MMMM d, yyyy") ?? "Date TBD";
            var dayOfWeek = model.ReportDate?.ToString("dddd");

            builder.AppendLine($"# Daily Recap \u2013 {reportDate}");
            builder.AppendLine();
            builder.AppendLine($"**Property:** {FormatValue(property.Name, $"Property #{property.Id}")}");
            if (!string.IsNullOrWhiteSpace(property.Code))
            {
                builder.AppendLine($"**Property Code:** {property.Code}");
            }
            if (!string.IsNullOrWhiteSpace(dayOfWeek))
            {
                builder.AppendLine($"**Day of Week:** {dayOfWeek}");
            }
            builder.AppendLine($"**Manager/Supervisor:** {FormatValue(model.ManagerName)}");
            builder.AppendLine();

            AppendStartOfDay(model, builder);
            AppendEndOfDay(model, builder);
            AppendStaffing(model, builder);
            AppendOutOfOrder(model, builder);
            AppendRoomExceptions(model, builder);
            AppendMaintenanceIssues(model, builder);
            AppendInspectionIssues(model, builder);
            AppendPublicAreas(model, builder);
            AppendPerformanceNotes(model, builder);
            AppendAdditionalNotes(model, builder);

            return builder.ToString().Trim();
        }

        private static void AppendStartOfDay(DailyRecapViewModel model, StringBuilder builder)
        {
            if (!HasAnyValue(model.OccupancyPercent, model.CheckOuts, model.Stayovers, model.RoomsOutOfOrderStart))
            {
                return;
            }

            builder.AppendLine("## Start of Day");
            builder.AppendLine($"- **Occupancy %:** {FormatValue(model.OccupancyPercent)}");
            builder.AppendLine($"- **Check-outs:** {FormatValue(model.CheckOuts)}");
            builder.AppendLine($"- **Stayovers:** {FormatValue(model.Stayovers)}");
            builder.AppendLine($"- **Rooms OOO:** {FormatValue(model.RoomsOutOfOrderStart)}");
            builder.AppendLine();
        }

        private static void AppendEndOfDay(DailyRecapViewModel model, StringBuilder builder)
        {
            if (!HasAnyValue(model.VacantClean, model.VacantDirty, model.DeepCleansCompleted, model.RoomsOutOfOrderEnd))
            {
                return;
            }

            builder.AppendLine("## End of Day");
            builder.AppendLine($"- **Vacant Clean:** {FormatValue(model.VacantClean)}");
            builder.AppendLine($"- **Vacant Dirty:** {FormatValue(model.VacantDirty)}");
            builder.AppendLine($"- **Deep Cleans Completed:** {FormatValue(model.DeepCleansCompleted)}");
            builder.AppendLine($"- **Rooms OOO:** {FormatValue(model.RoomsOutOfOrderEnd)}");
            builder.AppendLine();
        }

        private static void AppendStaffing(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.Staffing
                .Where(row => row != null && HasAnyValue(row.Area, row.Scheduled, row.CallOffs, row.Tardies, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 1. Staffing Overview");
            builder.AppendLine("| Area | Scheduled | Call-Offs | Tardies | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Scheduled)} | {EscapeTableValue(row.CallOffs)} | {EscapeTableValue(row.Tardies)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendOutOfOrder(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.OutOfOrderRooms
                .Where(row => row != null && HasAnyValue(row.RoomNumber, row.Status, row.Issue, row.CleanStatus, row.ReasonLeftDirty))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 2. Out of Order / Out of Service Rooms");
            builder.AppendLine("| Room Number | OOO / OOS | Issue | Clean or Dirty | If dirty, why was it left dirty? |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.RoomNumber)} | {EscapeTableValue(row.Status)} | {EscapeTableValue(row.Issue)} | {EscapeTableValue(row.CleanStatus)} | {EscapeTableValue(row.ReasonLeftDirty)} |");
            }

            builder.AppendLine();
        }

        private static void AppendRoomExceptions(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.RoomsNotCleaned
                .Where(row => row != null && HasAnyValue(row.RoomNumber, row.Status, row.Reason, row.AssignedTo, row.ActionPlan))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 3. Rooms Not Cleaned / Rolled Over / DND");
            builder.AppendLine("| Room Number | Room Status | Reason Not Cleaned | Assigned To | Action Plan / Next Step |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.RoomNumber)} | {EscapeTableValue(row.Status)} | {EscapeTableValue(row.Reason)} | {EscapeTableValue(row.AssignedTo)} | {EscapeTableValue(row.ActionPlan)} |");
            }

            builder.AppendLine();
        }

        private static void AppendMaintenanceIssues(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.MaintenanceIssues
                .Where(row => row != null && HasAnyValue(row.Area, row.Issue, row.WorkOrderSubmitted, row.RoomStatus, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 4. Maintenance Issues Found by Housekeeping");
            builder.AppendLine("| Room / Area | Issue Found | Work Order Submitted? | Room Status | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Issue)} | {EscapeTableValue(row.WorkOrderSubmitted)} | {EscapeTableValue(row.RoomStatus)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendInspectionIssues(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.InspectionFailures
                .Where(row => row != null && HasAnyValue(row.Area, row.Issue, row.ResponsibleAssociate, row.CoachingGiven, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 5. Cleanliness / Inspection Failures");
            builder.AppendLine("| Room / Area | Issue | Associate Responsible | Coaching Given? | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Issue)} | {EscapeTableValue(row.ResponsibleAssociate)} | {EscapeTableValue(row.CoachingGiven)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendPublicAreas(DailyRecapViewModel model, StringBuilder builder)
        {
            var rows = model.PublicAreas
                .Where(row => row != null && HasAnyValue(row.Area, row.Completed, row.Issues, row.ItemsToOrder, row.Notes))
                .ToList();

            if (!rows.Any())
            {
                return;
            }

            builder.AppendLine("## 6. Public Areas / Laundry / Supplies");
            builder.AppendLine("_Attach completed & signed Houseman/Public Area Attendant checklist when applicable._");
            builder.AppendLine();
            builder.AppendLine("| Area / Item | Completed? | Issue / Shortage | Items Need Order | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {EscapeTableValue(row.Area)} | {EscapeTableValue(row.Completed)} | {EscapeTableValue(row.Issues)} | {EscapeTableValue(row.ItemsToOrder)} | {EscapeTableValue(row.Notes)} |");
            }

            builder.AppendLine();
        }

        private static void AppendPerformanceNotes(DailyRecapViewModel model, StringBuilder builder)
        {
            if (!HasAnyValue(model.PerformanceHighlights, model.PerformanceCoaching, model.OperationalChallenges, model.TomorrowPlan))
            {
                return;
            }

            builder.AppendLine("## 8. Associate Performance Notes & End-of-Day Summary");
            if (!string.IsNullOrWhiteSpace(model.PerformanceHighlights))
            {
                builder.AppendLine($"- **Top performers / recognition:** {FormatValue(model.PerformanceHighlights)}");
            }

            if (!string.IsNullOrWhiteSpace(model.PerformanceCoaching))
            {
                builder.AppendLine($"- **Coaching needed:** {FormatValue(model.PerformanceCoaching)}");
            }

            if (!string.IsNullOrWhiteSpace(model.OperationalChallenges))
            {
                builder.AppendLine($"- **Main operational challenges:** {FormatValue(model.OperationalChallenges)}");
            }

            if (!string.IsNullOrWhiteSpace(model.TomorrowPlan))
            {
                builder.AppendLine($"- **Plan for tomorrow:** {FormatValue(model.TomorrowPlan)}");
            }

            builder.AppendLine();
        }

        private static void AppendAdditionalNotes(DailyRecapViewModel model, StringBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(model.AdditionalNotes))
            {
                return;
            }

            builder.AppendLine("## Additional Notes");
            builder.AppendLine(FormatValue(model.AdditionalNotes));
            builder.AppendLine();
        }

        private static bool HasAnyValue(params string?[] values)
        {
            return values?.Any(value => !string.IsNullOrWhiteSpace(value)) ?? false;
        }

        private static string FormatValue(string? value, string placeholder = "—")
        {
            return string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();
        }

        private static string EscapeTableValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();

            return normalized.Replace("|", "\\|", StringComparison.Ordinal);
        }

        private static DateTime NormalizeToUtcDate(DateTime value)
        {
            var dateOnly = value.Date;
            return dateOnly.Kind == DateTimeKind.Utc
                ? dateOnly
                : DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
        }
    }
}

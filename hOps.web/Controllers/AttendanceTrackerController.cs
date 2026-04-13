using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AttendanceTrackerController : BaseController
    {
        private static readonly AttendanceRecordType[] _attendanceTypeOrder =
        {
            AttendanceRecordType.Tardy,
            AttendanceRecordType.LeftEarly,
            AttendanceRecordType.CallOff,
            AttendanceRecordType.NoCallNoShow,
            AttendanceRecordType.Sick,
            AttendanceRecordType.Vacation,
            AttendanceRecordType.Personal,
            AttendanceRecordType.Bereavement
        };
        private static readonly IReadOnlyDictionary<AttendanceRecordType, AttendanceCodeMetadata> _attendanceCodeMap =
            new Dictionary<AttendanceRecordType, AttendanceCodeMetadata>
            {
                [AttendanceRecordType.Tardy] = new("T", "Tardy", false),
                [AttendanceRecordType.LeftEarly] = new("LE", "Left Early", false),
                [AttendanceRecordType.CallOff] = new("C", "Call Off", false),
                [AttendanceRecordType.NoCallNoShow] = new("NS", "No Call/No Show", false),
                [AttendanceRecordType.Sick] = new("S", "Sick", true),
                [AttendanceRecordType.Vacation] = new("V", "Vacation", true),
                [AttendanceRecordType.Personal] = new("P", "Personal", true),
                [AttendanceRecordType.Bereavement] = new("B", "Bereavement", true)
            };

        public AttendanceTrackerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? filterMode, string? month, DateTime? startDate, DateTime? endDate, int? selectedEmployeeId, string? gridMonth, DateTime? detailDate)
        {
            var property = ViewBag.CurrentProperty as Property;
            var status = TempData["AttendanceStatus"] as string;
            var error = TempData["AttendanceError"] as string;
            var filter = new AttendanceHistoryFilterViewModel
            {
                Mode = filterMode ?? AttendanceHistoryFilterModes.Month,
                MonthValue = month ?? string.Empty,
                CustomStartDate = startDate,
                CustomEndDate = endDate
            };
            var viewModel = await BuildTrackerViewModelAsync(property, null, filter, selectedEmployeeId, status, error, gridMonth, detailDate);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Form")] AttendanceRecordFormViewModel model)
        {
            if (!TryGetCurrentProperty(out var property, out var redirect))
            {
                return redirect!;
            }

            var currentProperty = property!;

            if (!ModelState.IsValid)
            {
                var invalidViewModel = await BuildTrackerViewModelAsync(currentProperty, model, null, null, null, null, null, null);
                return View(nameof(Index), invalidViewModel);
            }

            var employee = await _context.MasterEmployees
                .Where(e => e.PropertyId == currentProperty.Id && e.Id == model.MasterEmployeeId!.Value)
                .Select(e => new { e.Id, e.FirstName, e.LastName })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.MasterEmployeeId), "Select a valid employee for this property.");
                var invalidViewModel = await BuildTrackerViewModelAsync(currentProperty, model, null, null, null, null, null, null);
                return View(nameof(Index), invalidViewModel);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Forbid();
            }

            var record = new AttendanceRecord
            {
                PropertyId = currentProperty.Id,
                MasterEmployeeId = employee.Id,
                AttendanceDate = model.AttendanceDate!.Value.Date,
                AttendanceType = model.AttendanceType!.Value,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = currentUser.Id
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            TempData["AttendanceStatus"] = "Attendance record saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!TryGetCurrentProperty(out var property, out var redirect))
            {
                return redirect!;
            }

            var currentProperty = property!;

            var record = await _context.AttendanceRecords
                .Where(r => r.PropertyId == currentProperty.Id && r.Id == id)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                return NotFound();
            }

            var form = new AttendanceRecordFormViewModel
            {
                Id = record.Id,
                MasterEmployeeId = record.MasterEmployeeId,
                AttendanceDate = record.AttendanceDate,
                AttendanceType = record.AttendanceType
            };

            await PopulateEmployeeOptionsAsync(form, currentProperty.Id, form.MasterEmployeeId);
            form.AttendanceTypeOptions = BuildAttendanceTypeOptions(form.AttendanceType);

            ViewBag.PropertyName = currentProperty.Name;
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AttendanceRecordFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!TryGetCurrentProperty(out var property, out var redirect))
            {
                return redirect!;
            }

            var currentProperty = property!;

            var record = await _context.AttendanceRecords
                .Where(r => r.PropertyId == currentProperty.Id && r.Id == id)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                TempData["AttendanceError"] = "The attendance record could not be found.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                await PopulateEmployeeOptionsAsync(model, currentProperty.Id, model.MasterEmployeeId);
                model.AttendanceTypeOptions = BuildAttendanceTypeOptions(model.AttendanceType);
                ViewBag.PropertyName = currentProperty.Name;
                return View(model);
            }

            var employee = await _context.MasterEmployees
                .Where(e => e.PropertyId == currentProperty.Id && e.Id == model.MasterEmployeeId!.Value)
                .Select(e => new { e.Id })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.MasterEmployeeId), "Select a valid employee for this property.");
                await PopulateEmployeeOptionsAsync(model, currentProperty.Id, model.MasterEmployeeId);
                model.AttendanceTypeOptions = BuildAttendanceTypeOptions(model.AttendanceType);
                ViewBag.PropertyName = currentProperty.Name;
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Forbid();
            }

            record.MasterEmployeeId = employee.Id;
            record.AttendanceDate = model.AttendanceDate!.Value.Date;
            record.AttendanceType = model.AttendanceType!.Value;
            record.UpdatedAtUtc = DateTime.UtcNow;
            record.UpdatedByUserId = currentUser.Id;

            await _context.SaveChangesAsync();

            TempData["AttendanceStatus"] = "Attendance record updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!TryGetCurrentProperty(out var property, out var redirect))
            {
                return redirect!;
            }

            var currentProperty = property!;

            var record = await _context.AttendanceRecords
                .Where(r => r.PropertyId == currentProperty.Id && r.Id == id)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                TempData["AttendanceError"] = "The selected attendance record could not be found.";
                return RedirectToAction(nameof(Index));
            }

            _context.AttendanceRecords.Remove(record);
            await _context.SaveChangesAsync();

            TempData["AttendanceStatus"] = "Attendance record deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<AttendanceTrackerViewModel> BuildTrackerViewModelAsync(
            Property? property,
            AttendanceRecordFormViewModel? formOverride,
            AttendanceHistoryFilterViewModel? filterOverride,
            int? selectedEmployeeId,
            string? statusMessage,
            string? errorMessage,
            string? gridMonth,
            DateTime? detailDate)
        {
            var form = formOverride ?? new AttendanceRecordFormViewModel();
            if (!form.AttendanceDate.HasValue)
            {
                form.AttendanceDate = DateTime.Today;
            }

            var filter = PrepareFilter(filterOverride);

            var viewModel = new AttendanceTrackerViewModel
            {
                HasPropertySelected = property != null,
                PropertyName = property?.Name,
                StatusMessage = statusMessage,
                ErrorMessage = errorMessage,
                Form = form,
                Filter = filter
            };

            form.AttendanceTypeOptions = BuildAttendanceTypeOptions(form.AttendanceType);

            if (property == null)
            {
                return viewModel;
            }

            await PopulateEmployeeOptionsAsync(form, property.Id, form.MasterEmployeeId);
            form.AttendanceTypeOptions = BuildAttendanceTypeOptions(form.AttendanceType);

            viewModel.SummaryRows = await LoadAttendanceSummaryAsync(property.Id, filter.RangeStartDate, filter.RangeEndDate);
            viewModel.MonthlyGrid = await BuildMonthlyGridAsync(property.Id, gridMonth);
            viewModel.SelectedEmployeeDetailDate = detailDate?.Date;

            if (selectedEmployeeId.HasValue)
            {
                var selectedSummary = viewModel.SummaryRows.FirstOrDefault(r => r.MasterEmployeeId == selectedEmployeeId.Value);
                if (selectedSummary != null)
                {
                    viewModel.SelectedEmployeeId = selectedSummary.MasterEmployeeId;
                    viewModel.SelectedEmployeeDisplayName = selectedSummary.EmployeeName;
                    var detailRangeStart = detailDate?.Date ?? filter.RangeStartDate;
                    var detailRangeEnd = detailDate?.Date ?? filter.RangeEndDate;
                    viewModel.SelectedEmployeeDetails = await LoadAttendanceDetailsAsync(
                        property.Id,
                        selectedSummary.MasterEmployeeId,
                        detailRangeStart,
                        detailRangeEnd);
                }
            }

            return viewModel;
        }

        private async Task<AttendanceMonthlyGridViewModel> BuildMonthlyGridAsync(int propertyId, string? monthValue)
        {
            var (monthStart, monthEnd, normalizedValue) = ResolveGridMonth(monthValue);
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var days = Enumerable.Range(0, daysInMonth)
                .Select(offset => monthStart.AddDays(offset))
                .ToList();
            var dayTotals = days
                .Select(day => new AttendanceGridDayTotalViewModel { Date = day, TotalCount = 0 })
                .ToList();

            var employees = await _context.MasterEmployees
                .Where(e => e.PropertyId == propertyId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new
                {
                    e.Id,
                    e.FirstName,
                    e.LastName
                })
                .ToListAsync();

            var rows = employees
                .Select(emp =>
                {
                    var displayName = $"{emp.FirstName} {emp.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = "Employee";
                    }
                    return new AttendanceGridRowViewModel
                    {
                        MasterEmployeeId = emp.Id,
                        EmployeeName = displayName,
                        Cells = days.Select(day => new AttendanceGridCellViewModel
                        {
                            Date = day
                        }).ToList(),
                        TotalCount = 0
                    };
                })
                .ToList();

            if (!rows.Any())
            {
                return new AttendanceMonthlyGridViewModel
                {
                    MonthStart = monthStart,
                    MonthEnd = monthEnd,
                    MonthValue = normalizedValue,
                    Days = days,
                    Rows = rows,
                    DayTotals = dayTotals,
                    LegendItems = BuildLegendItems(),
                    GrandTotal = 0
                };
            }

            var records = await _context.AttendanceRecords
                .Where(r => r.PropertyId == propertyId &&
                            r.AttendanceDate >= monthStart &&
                            r.AttendanceDate <= monthEnd)
                .Select(r => new
                {
                    r.Id,
                    r.MasterEmployeeId,
                    AttendanceDate = r.AttendanceDate,
                    r.AttendanceType
                })
                .ToListAsync();

            var rowLookup = rows.ToDictionary(r => r.MasterEmployeeId);
            var dayIndexLookup = days
                .Select((day, index) => new { day, index })
                .ToDictionary(entry => entry.day.Date, entry => entry.index);

            var dayTotalsArray = dayTotals.ToArray();
            var grandTotal = 0;

            foreach (var record in records)
            {
                if (!rowLookup.TryGetValue(record.MasterEmployeeId, out var row))
                {
                    continue;
                }

                var recordDate = record.AttendanceDate.Date;
                if (!dayIndexLookup.TryGetValue(recordDate, out var dayIndex))
                {
                    continue;
                }

                if (!_attendanceCodeMap.TryGetValue(record.AttendanceType, out var metadata))
                {
                    continue;
                }

                var entry = new AttendanceGridEntryViewModel
                {
                    RecordId = record.Id,
                    AttendanceType = record.AttendanceType,
                    Code = metadata.Code,
                    Label = metadata.Label,
                    IsExcused = metadata.IsExcused
                };

                var cell = row.Cells[dayIndex];
                cell.Entries.Add(entry);
                row.TotalCount++;
                dayTotalsArray[dayIndex].TotalCount++;
                grandTotal++;
            }

            foreach (var row in rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (!cell.HasEntries)
                    {
                        continue;
                    }

                    var summary = cell.Entries
                        .GroupBy(entry => entry.Label)
                        .Select(group => $"{group.Key} ({group.Count()})");
                    cell.Tooltip = string.Join(", ", summary);
                }
            }

            return new AttendanceMonthlyGridViewModel
            {
                MonthStart = monthStart,
                MonthEnd = monthEnd,
                MonthValue = normalizedValue,
                Days = days,
                Rows = rows,
                DayTotals = dayTotalsArray.ToList(),
                LegendItems = BuildLegendItems(),
                GrandTotal = grandTotal
            };
        }

        private static List<AttendanceCodeLegendItemViewModel> BuildLegendItems()
        {
            var items = new List<AttendanceCodeLegendItemViewModel>();
            foreach (var type in _attendanceTypeOrder)
            {
                if (_attendanceCodeMap.TryGetValue(type, out var metadata))
                {
                    items.Add(new AttendanceCodeLegendItemViewModel
                    {
                        AttendanceType = type,
                        Code = metadata.Code,
                        Label = metadata.Label,
                        IsExcused = metadata.IsExcused
                    });
                }
            }
            return items;
        }

        private static (DateTime Start, DateTime End, string Value) ResolveGridMonth(string? monthValue)
        {
            DateTime monthStart;
            if (!string.IsNullOrWhiteSpace(monthValue) &&
                DateTime.TryParseExact($"{monthValue}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
            {
                monthStart = parsedMonth;
            }
            else
            {
                var today = DateTime.Today;
                monthStart = new DateTime(today.Year, today.Month, 1);
            }

            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var normalizedValue = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return (monthStart, monthEnd, normalizedValue);
        }

        private async Task PopulateEmployeeOptionsAsync(AttendanceRecordFormViewModel model, int propertyId, int? selectedEmployeeId)
        {
            var employees = await _context.MasterEmployees
                .Where(e => e.PropertyId == propertyId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new
                {
                    e.Id,
                    e.FirstName,
                    e.LastName
                })
                .ToListAsync();

            var options = employees
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = $"{e.LastName}, {e.FirstName}",
                    Selected = selectedEmployeeId.HasValue && selectedEmployeeId.Value == e.Id
                })
                .ToList();

            model.EmployeeOptions = options;
        }

        private AttendanceHistoryFilterViewModel PrepareFilter(AttendanceHistoryFilterViewModel? filterOverride)
        {
            var filter = filterOverride ?? new AttendanceHistoryFilterViewModel();
            var normalizedMode = (filter.Mode ?? AttendanceHistoryFilterModes.Month).Trim().ToLowerInvariant();
            if (normalizedMode != AttendanceHistoryFilterModes.Custom)
            {
                normalizedMode = AttendanceHistoryFilterModes.Month;
            }

            if (normalizedMode == AttendanceHistoryFilterModes.Month)
            {
                DateTime monthReference;
                if (!string.IsNullOrWhiteSpace(filter.MonthValue) &&
                    DateTime.TryParseExact(
                        $"{filter.MonthValue}-01",
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedMonth))
                {
                    monthReference = parsedMonth;
                }
                else
                {
                    monthReference = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    filter.MonthValue = monthReference.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                }

                filter.RangeStartDate = new DateTime(monthReference.Year, monthReference.Month, 1);
                filter.RangeEndDate = filter.RangeStartDate.AddMonths(1).AddDays(-1);
                filter.CustomStartDate = filter.RangeStartDate;
                filter.CustomEndDate = filter.RangeEndDate;
            }
            else
            {
                var start = (filter.CustomStartDate ?? DateTime.Today.AddDays(-30)).Date;
                var end = (filter.CustomEndDate ?? DateTime.Today).Date;
                if (end < start)
                {
                    (start, end) = (end, start);
                }

                filter.RangeStartDate = start;
                filter.RangeEndDate = end;
                if (string.IsNullOrWhiteSpace(filter.MonthValue))
                {
                    filter.MonthValue = start.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                }
            }

            filter.Mode = normalizedMode;
            return filter;
        }

        private async Task<List<AttendanceSummaryRowViewModel>> LoadAttendanceSummaryAsync(int propertyId, DateTime startDate, DateTime endDate)
        {
            var attendanceQuery = _context.AttendanceRecords
                .Where(r => r.PropertyId == propertyId && r.AttendanceDate >= startDate && r.AttendanceDate <= endDate)
                .GroupBy(r => r.MasterEmployeeId)
                .Select(g => new
                {
                    MasterEmployeeId = g.Key,
                    Tardy = g.Count(r => r.AttendanceType == AttendanceRecordType.Tardy),
                    LeftEarly = g.Count(r => r.AttendanceType == AttendanceRecordType.LeftEarly),
                    CallOff = g.Count(r => r.AttendanceType == AttendanceRecordType.CallOff),
                    NoCallNoShow = g.Count(r => r.AttendanceType == AttendanceRecordType.NoCallNoShow),
                    Sick = g.Count(r => r.AttendanceType == AttendanceRecordType.Sick),
                    Vacation = g.Count(r => r.AttendanceType == AttendanceRecordType.Vacation),
                    Personal = g.Count(r => r.AttendanceType == AttendanceRecordType.Personal),
                    Bereavement = g.Count(r => r.AttendanceType == AttendanceRecordType.Bereavement)
                });

            var data = await _context.MasterEmployees
                .Where(e => e.PropertyId == propertyId)
                .Select(e => new
                {
                    e.Id,
                    e.FirstName,
                    e.LastName,
                    DepartmentName = e.Department.Name ?? "Unassigned",
                    e.Position
                })
                .Join(
                    attendanceQuery,
                    e => e.Id,
                    a => a.MasterEmployeeId,
                    (e, a) => new
                    {
                        e.Id,
                        e.FirstName,
                        e.LastName,
                        e.DepartmentName,
                        Position = e.Position ?? "Unassigned",
                        Counts = a
                    })
                .ToListAsync();

            return data
                .Select(item =>
                {
                    var displayName = $"{item.FirstName} {item.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = "Employee";
                    }

                    return new AttendanceSummaryRowViewModel
                    {
                        MasterEmployeeId = item.Id,
                        EmployeeName = displayName,
                        DepartmentName = item.DepartmentName,
                        Position = string.IsNullOrWhiteSpace(item.Position) ? "Unassigned" : item.Position,
                        TardyCount = item.Counts.Tardy,
                        LeftEarlyCount = item.Counts.LeftEarly,
                        CallOffCount = item.Counts.CallOff,
                        NoCallNoShowCount = item.Counts.NoCallNoShow,
                        SickCount = item.Counts.Sick,
                        VacationCount = item.Counts.Vacation,
                        PersonalCount = item.Counts.Personal,
                        BereavementCount = item.Counts.Bereavement
                    };
                })
                .OrderByDescending(r => r.TotalCount)
                .ThenBy(r => r.EmployeeName)
                .ToList();
        }

        private async Task<List<AttendanceDetailEntryViewModel>> LoadAttendanceDetailsAsync(int propertyId, int employeeId, DateTime startDate, DateTime endDate)
        {
            var records = await _context.AttendanceRecords
                .Where(r => r.PropertyId == propertyId &&
                            r.MasterEmployeeId == employeeId &&
                            r.AttendanceDate >= startDate &&
                            r.AttendanceDate <= endDate)
                .Include(r => r.MasterEmployee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.CreatedByUser)
                .OrderByDescending(r => r.AttendanceDate)
                .ThenByDescending(r => r.CreatedAtUtc)
                .ToListAsync();

            return records.Select(r =>
            {
                var employee = r.MasterEmployee;
                var creator = r.CreatedByUser;
                var creatorName = creator != null
                    ? $"{creator.FirstName} {creator.LastName}".Trim()
                    : "Unknown";
                if (string.IsNullOrWhiteSpace(creatorName))
                {
                    creatorName = creator?.Email ?? "Unknown";
                }

                return new AttendanceDetailEntryViewModel
                {
                    RecordId = r.Id,
                    MasterEmployeeId = r.MasterEmployeeId,
                    AttendanceDate = r.AttendanceDate,
                    AttendanceType = r.AttendanceType,
                    AttendanceTypeDisplay = GetAttendanceTypeLabel(r.AttendanceType),
                    CreatedAtUtc = r.CreatedAtUtc,
                    UpdatedAtUtc = r.UpdatedAtUtc,
                    CreatedByDisplay = creatorName,
                    DepartmentName = employee?.Department?.Name ?? "Unassigned",
                    Position = employee?.Position ?? "Unassigned"
                };
            }).ToList();
        }

        private static List<SelectListItem> BuildAttendanceTypeOptions(AttendanceRecordType? selectedType)
        {
            var options = new List<SelectListItem>(_attendanceTypeOrder.Length);
            foreach (var type in _attendanceTypeOrder)
            {
                options.Add(new SelectListItem
                {
                    Value = type.ToString(),
                    Text = GetAttendanceTypeLabel(type),
                    Selected = selectedType.HasValue && selectedType.Value == type
                });
            }

            return options;
        }

        private static string GetAttendanceTypeLabel(AttendanceRecordType type)
        {
            return type switch
            {
                AttendanceRecordType.LeftEarly => "Left Early",
                AttendanceRecordType.CallOff => "Call Off",
                AttendanceRecordType.NoCallNoShow => "No Call/No Show",
                AttendanceRecordType.Sick => "Sick",
                AttendanceRecordType.Vacation => "Vacation",
                AttendanceRecordType.Personal => "Personal",
                AttendanceRecordType.Bereavement => "Bereavement",
                _ => "Tardy"
            };
        }

        private bool TryGetCurrentProperty(out Property? property, out IActionResult? redirect)
        {
            property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["AttendanceError"] = "Select a property to use the Attendance Tracker.";
                redirect = RedirectToAction(nameof(Index));
                return false;
            }

            redirect = null;
            return true;
        }

        private sealed record AttendanceCodeMetadata(string Code, string Label, bool IsExcused);
    }
}

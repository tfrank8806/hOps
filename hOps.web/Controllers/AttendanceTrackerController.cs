using System;
using System.Collections.Generic;
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

        public AttendanceTrackerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var property = ViewBag.CurrentProperty as Property;
            var status = TempData["AttendanceStatus"] as string;
            var error = TempData["AttendanceError"] as string;
            var viewModel = await BuildTrackerViewModelAsync(property, null, status, error);
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
                var invalidViewModel = await BuildTrackerViewModelAsync(currentProperty, model, null, null);
                return View(nameof(Index), invalidViewModel);
            }

            var employee = await _context.MasterEmployees
                .Where(e => e.PropertyId == currentProperty.Id && e.Id == model.MasterEmployeeId!.Value)
                .Select(e => new { e.Id, e.FirstName, e.LastName })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.MasterEmployeeId), "Select a valid employee for this property.");
                var invalidViewModel = await BuildTrackerViewModelAsync(currentProperty, model, null, null);
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
            string? statusMessage,
            string? errorMessage)
        {
            var form = formOverride ?? new AttendanceRecordFormViewModel();
            if (!form.AttendanceDate.HasValue)
            {
                form.AttendanceDate = DateTime.Today;
            }

            var viewModel = new AttendanceTrackerViewModel
            {
                HasPropertySelected = property != null,
                PropertyName = property?.Name,
                StatusMessage = statusMessage,
                ErrorMessage = errorMessage,
                Form = form
            };

            if (property == null)
            {
                form.AttendanceTypeOptions = BuildAttendanceTypeOptions(form.AttendanceType);
                return viewModel;
            }

            await PopulateEmployeeOptionsAsync(form, property.Id, form.MasterEmployeeId);
            form.AttendanceTypeOptions = BuildAttendanceTypeOptions(form.AttendanceType);
            viewModel.Records = await LoadAttendanceRowsAsync(property.Id);

            return viewModel;
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

        private async Task<List<AttendanceRecordRowViewModel>> LoadAttendanceRowsAsync(int propertyId)
        {
            var records = await _context.AttendanceRecords
                .Where(r => r.PropertyId == propertyId)
                .Include(r => r.MasterEmployee)
                    .ThenInclude(e => e.Department)
                .OrderByDescending(r => r.AttendanceDate)
                .ThenByDescending(r => r.CreatedAtUtc)
                .ToListAsync();

            return records.Select(r =>
            {
                var employee = r.MasterEmployee;
                var employeeName = employee != null
                    ? $"{employee.FirstName} {employee.LastName}".Trim()
                    : "Employee Removed";

                return new AttendanceRecordRowViewModel
                {
                    Id = r.Id,
                    EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "Employee" : employeeName,
                    DepartmentName = employee?.Department?.Name ?? "Unassigned",
                    Position = employee?.Position ?? "Unassigned",
                    AttendanceDate = r.AttendanceDate,
                    AttendanceType = r.AttendanceType,
                    AttendanceTypeDisplay = GetAttendanceTypeLabel(r.AttendanceType),
                    CreatedAtUtc = r.CreatedAtUtc,
                    UpdatedAtUtc = r.UpdatedAtUtc
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
    }
}

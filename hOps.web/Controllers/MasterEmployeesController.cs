using System;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.MasterEmployees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class MasterEmployeesController : BaseController
    {
        public MasterEmployeesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            Property? property = ViewBag.CurrentProperty as Property;
            var viewModel = new MasterEmployeeListViewModel
            {
                HasPropertySelected = property != null,
                PropertyName = property?.Name,
                StatusMessage = TempData["MasterEmployeeStatus"] as string,
                ErrorMessage = TempData["MasterEmployeeError"] as string
            };

            if (property == null)
            {
                return View(viewModel);
            }

            viewModel.Employees = await _context.MasterEmployees
                .Where(e => e.PropertyId == property.Id)
                .Include(e => e.Department)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new MasterEmployeeRowViewModel
                {
                    Id = e.Id,
                    DepartmentId = e.DepartmentId,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    DepartmentName = e.Department.Name ?? "Unassigned",
                    Position = e.Position
                })
                .ToListAsync();

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!TryGetCurrentProperty(out var property, out var redirect))
            {
                return redirect!;
            }

            var currentProperty = property!;
            var form = new MasterEmployeeFormViewModel();
            await PopulateDepartmentOptionsAsync(form, currentProperty.Id);
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MasterEmployeeFormViewModel model)
        {
            if (!TryGetCurrentProperty(out var property, out var redirect))
            {
                return redirect!;
            }

            var currentProperty = property!;
            await PopulateDepartmentOptionsAsync(model, currentProperty.Id);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == model.DepartmentId && d.PropertyId == currentProperty.Id);

            if (department == null)
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Select a valid department.");
                return View(model);
            }

            var employee = new MasterEmployee
            {
                PropertyId = currentProperty.Id,
                DepartmentId = department.Id,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Position = model.Position.Trim()
            };

            _context.MasterEmployees.Add(employee);
            await _context.SaveChangesAsync();

            TempData["MasterEmployeeStatus"] = $"{employee.FirstName} {employee.LastName} added to {currentProperty.Name}.";
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

            var employee = await _context.MasterEmployees
                .Where(e => e.PropertyId == currentProperty.Id && e.Id == id)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound();
            }

            var form = new MasterEmployeeFormViewModel
            {
                Id = employee.Id,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                DepartmentId = employee.DepartmentId,
                Position = employee.Position
            };

            await PopulateDepartmentOptionsAsync(form, currentProperty.Id);
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MasterEmployeeFormViewModel model)
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

            var employee = await _context.MasterEmployees
                .FirstOrDefaultAsync(e => e.PropertyId == currentProperty.Id && e.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            await PopulateDepartmentOptionsAsync(model, currentProperty.Id);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == model.DepartmentId && d.PropertyId == currentProperty.Id);

            if (department == null)
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Select a valid department.");
                return View(model);
            }

            employee.FirstName = model.FirstName.Trim();
            employee.LastName = model.LastName.Trim();
            employee.DepartmentId = department.Id;
            employee.Position = model.Position.Trim();
            employee.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["MasterEmployeeStatus"] = $"{employee.FirstName} {employee.LastName} was updated.";
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

            var employee = await _context.MasterEmployees
                .FirstOrDefaultAsync(e => e.PropertyId == currentProperty.Id && e.Id == id);

            if (employee == null)
            {
                TempData["MasterEmployeeError"] = "The selected employee could not be found.";
                return RedirectToAction(nameof(Index));
            }

            _context.MasterEmployees.Remove(employee);
            await _context.SaveChangesAsync();

            TempData["MasterEmployeeStatus"] = $"{employee.FirstName} {employee.LastName} was removed.";
            return RedirectToAction(nameof(Index));
        }

        private bool TryGetCurrentProperty(out Property? property, out IActionResult? redirect)
        {
            property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MasterEmployeeError"] = "Select a property to manage the master employee list.";
                redirect = RedirectToAction(nameof(Index));
                return false;
            }

            redirect = null;
            return true;
        }

        private async Task PopulateDepartmentOptionsAsync(MasterEmployeeFormViewModel model, int propertyId)
        {
            var departments = await _context.Departments
                .Where(d => d.PropertyId == propertyId)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem
                {
                    Text = d.Name ?? "Unnamed Department",
                    Value = d.Id.ToString(),
                    Selected = model.DepartmentId.HasValue && model.DepartmentId.Value == d.Id
                })
                .ToListAsync();

            model.DepartmentOptions = departments;
        }
    }
}

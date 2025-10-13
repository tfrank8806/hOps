using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SettingsController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Departments()
        {
            var departments = await _db.Departments.ToListAsync();
            return View(departments);
        }

        public IActionResult CreateDepartment()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateDepartment(Department model)
        {
            if (!ModelState.IsValid) return View(model);

            _db.Departments.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("Departments");
        }

        public async Task<IActionResult> EditDepartment(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();
            return View(dept);
        }

        [HttpPost]
        public async Task<IActionResult> EditDepartment(Department model)
        {
            if (!ModelState.IsValid) return View(model);

            _db.Departments.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("Departments");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();

            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
            return RedirectToAction("Departments");
        }
    }
}

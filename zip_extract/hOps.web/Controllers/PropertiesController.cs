using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hOps.web.Data;
using hOps.web.Models;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin")]  // only Admins can manage properties
    public class PropertiesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public PropertiesController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Properties
        public async Task<IActionResult> Index()
        {
            var list = await _db.Properties.ToListAsync();
            return View(list);
        }

        // GET: /Properties/Create
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Property property)
        {
            if (!ModelState.IsValid)
                return View(property);

            // Optionally: check for duplicate Code or Name
            bool exists = await _db.Properties.AnyAsync(p => p.Code == property.Code);
            if (exists)
            {
                ModelState.AddModelError(nameof(property.Code), "A property with this code already exists.");
                return View(property);
            }

            _db.Properties.Add(property);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Properties/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var prop = await _db.Properties.FindAsync(id);
            if (prop == null)
                return NotFound();

            return View(prop);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Property property)
        {
            if (id != property.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(property);

            // Optionally: check duplicate code only for other records
            bool exists = await _db.Properties
                .AnyAsync(p => p.Code == property.Code && p.Id != property.Id);
            if (exists)
            {
                ModelState.AddModelError(nameof(property.Code), "Another property with this code already exists.");
                return View(property);
            }

            _db.Properties.Update(property);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Properties/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var prop = await _db.Properties.FindAsync(id);
            if (prop == null)
                return NotFound();

            return View(prop);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prop = await _db.Properties.FindAsync(id);
            if (prop != null)
            {
                _db.Properties.Remove(prop);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}

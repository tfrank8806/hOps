using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PropertiesController : BaseController
    {
        public PropertiesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
            : base(db, userManager)
        {
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.Properties.ToListAsync();
            return View(list);
        }

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

            bool exists = await _context.Properties.AnyAsync(p => p.Code == property.Code);
            if (exists)
            {
                ModelState.AddModelError(nameof(property.Code), "A property with this code already exists.");
                return View(property);
            }

            _context.Properties.Add(property);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var prop = await _context.Properties.FindAsync(id);
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

            bool exists = await _context.Properties
                .AnyAsync(p => p.Code == property.Code && p.Id != property.Id);
            if (exists)
            {
                ModelState.AddModelError(nameof(property.Code), "Another property with this code already exists.");
                return View(property);
            }

            _context.Properties.Update(property);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var prop = await _context.Properties.FindAsync(id);
            if (prop == null)
                return NotFound();

            return View(prop);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prop = await _context.Properties.FindAsync(id);
            if (prop != null)
            {
                _context.Properties.Remove(prop);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}

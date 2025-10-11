using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> AccessRequests()
        {
            // Show pending requests
            var pending = await _db.UserAccessRequests
                .Where(r => !r.IsApproved && !r.IsRejected)
                .ToListAsync();
            return View(pending);
        }

        public async Task<IActionResult> Details(int id)
        {
            var req = await _db.UserAccessRequests.FindAsync(id);
            if (req == null) return NotFound();
            return View(req);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var req = await _db.UserAccessRequests.FindAsync(id);
            if (req == null) return NotFound();

            // Create the user
            var user = new ApplicationUser
            {
                UserName = req.Email,
                Email = req.Email,
                FirstName = req.FirstName,
                LastName = req.LastName,
                MobilePhone = req.MobilePhone
            };

            // Create user without password (we already have hash)
            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                // handle errors
                TempData["Error"] = "Error creating user: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Details), new { id = id });
            }

            // Set the password hash (directly) — risky but acceptable in this flow
            user.PasswordHash = req.PasswordHash;
            await _db.SaveChangesAsync();

            // Assign role (ensure role exists)
            var defaultRole = "User";
            if (!await _roleManager.RoleExistsAsync(defaultRole))
                await _roleManager.CreateAsync(new IdentityRole(defaultRole));
            await _userManager.AddToRoleAsync(user, defaultRole);

            // Map property access (if you have UserPropertyAccess)
            // For this, we need to find the property by code
            var prop = await _db.Properties.FirstOrDefaultAsync(p => p.Code == req.PropertyCode);
            if (prop != null)
            {
                var upa = new UserPropertyAccess
                {
                    ApplicationUserId = user.Id,
                    PropertyId = prop.Id
                };
                _db.UserPropertyAccesses.Add(upa);
                await _db.SaveChangesAsync();
            }

            // Mark request as approved
            req.IsApproved = true;
            _db.UserAccessRequests.Update(req);
            await _db.SaveChangesAsync();

            TempData["Success"] = "User approved and account created.";
            return RedirectToAction(nameof(AccessRequests));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var req = await _db.UserAccessRequests.FindAsync(id);
            if (req == null) return NotFound();

            req.IsRejected = true;
            _db.UserAccessRequests.Update(req);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Request rejected.";
            return RedirectToAction(nameof(AccessRequests));
        }
    }
}

using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, IEmailSender emailSender)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }

        // Admin Landing Page
        public IActionResult Dashboard()
        {
            return View();
        }

        // View all users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        // Settings placeholder
        public IActionResult Settings()
        {
            return View();
        }

        // View all pending access requests
        public async Task<IActionResult> AccessRequests()
        {
            var requests = await _db.UserAccessRequests
                .Where(r => !r.IsRejected && !_db.Users.Any(u => u.Email == r.Email))
                .ToListAsync();

            return View(requests);
        }

        // API endpoint for badge counter
        [HttpGet]
        public async Task<JsonResult> GetPendingAccessRequestCount()
        {
            var count = await _db.UserAccessRequests
                .CountAsync(r => !r.IsRejected && !_db.Users.Any(u => u.Email == r.Email));

            return Json(count);
        }

        // Approve access request
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _db.UserAccessRequests.FindAsync(id);
            if (request == null) return NotFound();

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                MobilePhone = request.MobilePhone
            };

            var result = await _userManager.CreateAsync(user, "TempPassword@123");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");

                var message = $@"
Hi {user.FirstName},<br/><br/>
Your access request to HotelOps has been <strong>approved</strong>.<br/>
You can now log in using your email and temporary password:<br/>
<strong>Password:</strong> TempPassword@123<br/><br/>
Please change your password after login.<br/><br/>
HotelOps Admin Team
";

                await _emailSender.SendEmailAsync(user.Email, "HotelOps Access Approved", message);

                TempData["Success"] = "Access granted and user created.";
            }
            else
            {
                TempData["Error"] = "Failed to create user: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(AccessRequests));
        }

        // Reject access request
        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var req = await _db.UserAccessRequests.FindAsync(id);
            if (req == null) return NotFound();

            req.IsRejected = true;
            _db.UserAccessRequests.Update(req);
            await _db.SaveChangesAsync();

            var message = $@"
Hi {req.FirstName},<br/><br/>
Your access request to HotelOps was <strong>not approved</strong>.<br/>
If you believe this was an error, please contact your property manager.<br/><br/>
Thank you,<br/>
HotelOps Admin Team
";
            await _emailSender.SendEmailAsync(req.Email, "Access Request Denied", message);

            TempData["Success"] = "Request rejected and user notified.";
            return RedirectToAction(nameof(AccessRequests));
        }
    }
}

using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }

        // Admin landing
        public IActionResult Dashboard()
        {
            return View();
        }

        // List all users + roles + property access
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var vmList = new List<UserWithAccessViewModel>();

            foreach (var u in users)
            {
                var vm = new UserWithAccessViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? "",
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Roles = (await _userManager.GetRolesAsync(u)).ToList()
                };

                vm.PropertyIds = await _db.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == u.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();

                vmList.Add(vm);
            }

            return View(vmList);
        }

        [HttpGet]
        public async Task<IActionResult> EditUserProperties(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var allProperties = await _db.Properties.ToListAsync();
            var userProps = await _db.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            var currentUser = await _userManager.GetUserAsync(User);
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            List<Property> assignableProps = allProperties;
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var managerPropIds = await _db.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();

                assignableProps = allProperties.Where(p => managerPropIds.Contains(p.Id)).ToList();
            }

            var allRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            var userRoles = (await _userManager.GetRolesAsync(user)).ToList();

            var vm = new EditUserPropertiesViewModel
            {
                UserId = userId,
                Email = user.Email,
                PropertyList = assignableProps,
                SelectedPropertyIds = userProps,
                AllRoles = allRoles,
                SelectedRoles = userRoles
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserProperties(EditUserPropertiesViewModel vm)
        {
            if (vm == null || string.IsNullOrEmpty(vm.UserId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null)
                return NotFound();

            var allProperties = await _db.Properties.ToListAsync();
            var currentUser = await _userManager.GetUserAsync(User);
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            HashSet<int> allowedPropIds = allProperties.Select(p => p.Id).ToHashSet();
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var mgrPropIds = await _db.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();
                allowedPropIds = mgrPropIds.ToHashSet();
            }

            // Remove existing accesses that are within allowed set
            var existingAccesses = await _db.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id && allowedPropIds.Contains(upa.PropertyId))
                .ToListAsync();
            _db.UserPropertyAccesses.RemoveRange(existingAccesses);

            if (vm.SelectedPropertyIds != null)
            {
                foreach (var pid in vm.SelectedPropertyIds)
                {
                    if (!allowedPropIds.Contains(pid))
                        continue;

                    _db.UserPropertyAccesses.Add(new UserPropertyAccess
                    {
                        ApplicationUserId = user.Id,
                        PropertyId = pid
                    });
                }
            }

            await _db.SaveChangesAsync();

            // Handle role assignments
            var currentUserRoles = await _userManager.GetRolesAsync(user);
            var desiredRoles = vm.SelectedRoles ?? new List<string>();

            // Add new roles
            foreach (var role in desiredRoles.Except(currentUserRoles))
            {
                // If manager editing, disallow assigning Admin
                if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin") && role == "Admin")
                {
                    continue;
                }
                await _userManager.AddToRoleAsync(user, role);
            }

            // Remove roles not in desired
            foreach (var role in currentUserRoles.Except(desiredRoles))
            {
                // Prevent self-removal of Admin role optionally
                if (role == "Admin" && user.Id == currentUser.Id)
                {
                    continue;  // skip removing own admin
                }
                await _userManager.RemoveFromRoleAsync(user, role);
            }

            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> AccessRequests()
        {
            var requests = await _db.UserAccessRequests
                .Where(r => !r.IsRejected && !_db.Users.Any(u => u.Email == r.Email))
                .ToListAsync();

            return View(requests);
        }

        [HttpGet]
        public async Task<JsonResult> GetPendingAccessRequestCount()
        {
            var count = await _db.UserAccessRequests
                .CountAsync(r => !r.IsRejected && !_db.Users.Any(u => u.Email == r.Email));
            return Json(count);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _db.UserAccessRequests.FindAsync(id);
            if (request == null)
                return NotFound();

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
Your access request for HotelOps has been <strong>approved</strong>.<br/>
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

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var req = await _db.UserAccessRequests.FindAsync(id);
            if (req == null)
                return NotFound();

            req.IsRejected = true;
            _db.UserAccessRequests.Update(req);
            await _db.SaveChangesAsync();

            var message = $@"
Hi {req.FirstName},<br/><br/>
Your access request for HotelOps was <strong>not approved</strong>.<br/>
If you believe this is in error, please contact your property manager.<br/><br/>
Thank you,<br/>
HotelOps Admin Team
";
            await _emailSender.SendEmailAsync(req.Email, "Access Request Denied", message);
            TempData["Success"] = "Request rejected and user notified.";

            return RedirectToAction(nameof(AccessRequests));
        }

        public IActionResult Settings()
        {
            return View();
        }
    }
}

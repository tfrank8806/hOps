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
using System.Security.Cryptography;


namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AdminController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender)
            : base(db, userManager)
        {
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

                vm.PropertyIds = await _context.UserPropertyAccesses
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

            // All properties
            var allProperties = await _context.Properties.ToListAsync();

            // Properties user currently has
            var userProps = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            // Determine which properties current editor is allowed to assign
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            List<Property> assignableProps = allProperties;
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var managerPropIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();

                assignableProps = allProperties.Where(p => managerPropIds.Contains(p.Id)).ToList();
            }

            // Roles data
            var allRoles = await _roleManager.Roles
                .Where(r => r.Name != null)
                .Select(r => r.Name!)
                .ToListAsync();
            var userRoles = (await _userManager.GetRolesAsync(user))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!)
                .ToList();

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

            var allProperties = await _context.Properties.ToListAsync();
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            // Determine allowed properties
            HashSet<int> allowedPropIds = allProperties.Select(p => p.Id).ToHashSet();
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var mgrPropIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();
                allowedPropIds = mgrPropIds.ToHashSet();
            }

            // Remove existing accesses that are within allowed set
            var existingAccesses = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id && allowedPropIds.Contains(upa.PropertyId))
                .ToListAsync();
            _context.UserPropertyAccesses.RemoveRange(existingAccesses);

            // Add new ones from posted selections
            if (vm.SelectedPropertyIds != null)
            {
                foreach (var pid in vm.SelectedPropertyIds)
                {
                    if (!allowedPropIds.Contains(pid))
                        continue;

                    _context.UserPropertyAccesses.Add(new UserPropertyAccess
                    {
                        ApplicationUserId = user.Id,
                        PropertyId = pid
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Handle role assignments
            var currentUserRoles = (await _userManager.GetRolesAsync(user))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!)
                .ToList();
            var desiredRoles = (vm.SelectedRoles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!)
                .ToList();

            // Add roles not currently assigned
            foreach (var role in desiredRoles.Except(currentUserRoles))
            {
                if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin") && role == "Admin")
                    continue; // Managers can’t assign Admin role
                await _userManager.AddToRoleAsync(user, role);
            }

            // Remove roles not desired
            foreach (var role in currentUserRoles.Except(desiredRoles))
            {
                if (role == "Admin" && user.Id == currentUser.Id)
                    continue; // prevent self-demotion
                await _userManager.RemoveFromRoleAsync(user, role);
            }

            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> AccessRequests()
        {
            var requests = await _context.UserAccessRequests
                .Where(r => !r.IsRejected && !_context.Users.Any(u => u.Email == r.Email))
                .ToListAsync();

            return View(requests);
        }

        [HttpGet]
        public async Task<JsonResult> GetPendingAccessRequestCount()
        {
            var count = await _context.UserAccessRequests
                .CountAsync(r => !r.IsRejected && !_context.Users.Any(u => u.Email == r.Email));
            return Json(count);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.UserAccessRequests.FindAsync(id);
            if (request == null)
                return NotFound();

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                MobilePhone = request.MobilePhone,
                MustChangePassword = true
            };

            var tempPassword = GenerateTemporaryPassword();
            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Failed to create user: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(AccessRequests));
            }

            await _userManager.AddToRoleAsync(user, "User");

            var message = $@"
Hi {user.FirstName},<br/><br/>
Your access request for HotelOps has been <strong>approved</strong>.<br/>
You can now log in using your email and temporary password:<br/>
<strong>Password:</strong> {tempPassword}<br/><br/>
Please change your password after login.<br/><br/>
HotelOps Admin Team
";
            await _emailSender.SendEmailAsync(user.Email, "HotelOps Access Approved", message);

            TempData["Success"] = "Access granted and a temporary password email has been sent. Assign properties for the user below.";
            return RedirectToAction(nameof(EditUserProperties), new { userId = user.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var req = await _context.UserAccessRequests.FindAsync(id);
            if (req == null)
                return NotFound();

            req.IsRejected = true;
            _context.UserAccessRequests.Update(req);
            await _context.SaveChangesAsync();

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

        private static string GenerateTemporaryPassword()
        {
            var options = new PasswordOptions
            {
                RequiredLength = 12,
                RequiredUniqueChars = 4,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = true
            };

            string[] randomChars = new[]
            {
                "ABCDEFGHJKLMNPQRSTUVWXYZ",
                "abcdefghijkmnopqrstuvwxyz",
                "0123456789",
                "!@$?_-"
            };

            var chars = new List<char>();

            if (options.RequireUppercase)
            {
                chars.Add(GetRandomChar(randomChars[0]));
            }

            if (options.RequireLowercase)
            {
                chars.Add(GetRandomChar(randomChars[1]));
            }

            if (options.RequireDigit)
            {
                chars.Add(GetRandomChar(randomChars[2]));
            }

            if (options.RequireNonAlphanumeric)
            {
                chars.Add(GetRandomChar(randomChars[3]));
            }

            while (chars.Count < options.RequiredLength || chars.Distinct().Count() < options.RequiredUniqueChars)
            {
                var set = randomChars[RandomNumberGenerator.GetInt32(randomChars.Length)];
                chars.Add(GetRandomChar(set));
            }

            for (var i = chars.Count - 1; i > 0; i--)
            {
                var swapIndex = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[swapIndex]) = (chars[swapIndex], chars[i]);
            }

            return new string(chars.ToArray());
        }

        private static char GetRandomChar(string characterSet)
        {
            return characterSet[RandomNumberGenerator.GetInt32(characterSet.Length)];
        }
        public IActionResult Settings()
        {
            return View();
        }
    }
}














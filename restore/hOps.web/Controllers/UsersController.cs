using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

#nullable enable
namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: /Users
        public async Task<IActionResult> Index()
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

        // GET: /Users/EditPropertiesRoles?userId=...
        [HttpGet]
        public async Task<IActionResult> EditPropertiesRoles(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // All properties
            var allProps = await _db.Properties.ToListAsync();
            // Properties that the user currently has
            var userPropIds = await _db.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            // Determine assignable properties depending on current editor role
            var currentUser = await _userManager.GetUserAsync(User);
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            List<Property> assignableProps = allProps;
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var mgrPropIds = await _db.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();
                assignableProps = allProps.Where(p => mgrPropIds.Contains(p.Id)).ToList();
            }

            // Roles
            var allRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            var userRoles = (await _userManager.GetRolesAsync(user)).ToList();

            var vm = new EditUserPropertiesViewModel
            {
                UserId = userId,
                Email = user.Email,
                PropertyList = assignableProps,
                SelectedPropertyIds = userPropIds,
                AllRoles = allRoles,
                SelectedRoles = userRoles
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPropertiesRoles(EditUserPropertiesViewModel vm)
        {
            if (vm == null || string.IsNullOrEmpty(vm.UserId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null)
                return NotFound();

            var allProps = await _db.Properties.ToListAsync();
            var currentUser = await _userManager.GetUserAsync(User);
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            // Determine which property IDs this editor is allowed to manage
            HashSet<int> allowedPropIds = allProps.Select(p => p.Id).ToHashSet();
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var mgrPropIds = await _db.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();
                allowedPropIds = mgrPropIds.ToHashSet();
            }

            // Remove existing accesses (within allowed set)
            var existing = await _db.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id && allowedPropIds.Contains(upa.PropertyId))
                .ToListAsync();
            _db.UserPropertyAccesses.RemoveRange(existing);

            // Add new ones from the posted model
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

            // Roles: assign & remove
            var currentUserRoles = await _userManager.GetRolesAsync(user);
            var desiredRoles = vm.SelectedRoles ?? new List<string>();

            // Add missing roles
            foreach (var role in desiredRoles.Except(currentUserRoles))
            {
                // If manager editing, disallow assigning Admin
                if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin") && role == "Admin")
                {
                    continue;
                }
                await _userManager.AddToRoleAsync(user, role);
            }

            // Remove roles no longer desired
            foreach (var role in currentUserRoles.Except(desiredRoles))
            {
                // Prevent self-removal of Admin
                if (role == "Admin" && user.Id == currentUser.Id)
                {
                    continue;
                }
                await _userManager.RemoveFromRoleAsync(user, role);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

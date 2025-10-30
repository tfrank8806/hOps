using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class UsersController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
            : base(db, userManager)
        {
            _roleManager = roleManager;
        }

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

                vm.PropertyIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == u.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();

                vmList.Add(vm);
            }

            return View(vmList);
        }

        [HttpGet]
        public async Task<IActionResult> EditPropertiesRoles(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var allProps = await _context.Properties.ToListAsync();
            var userPropIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            List<Property> assignableProps = allProps;
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var mgrPropIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();

                assignableProps = allProps.Where(p => mgrPropIds.Contains(p.Id)).ToList();
            }

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
            if (vm == null || string.IsNullOrEmpty(vm.UserId)) return BadRequest();

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            var allProps = await _context.Properties.ToListAsync();
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            HashSet<int> allowedPropIds = allProps.Select(p => p.Id).ToHashSet();
            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var mgrPropIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.PropertyId)
                    .ToListAsync();

                allowedPropIds = mgrPropIds.ToHashSet();
            }

            var existing = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id && allowedPropIds.Contains(upa.PropertyId))
                .ToListAsync();

            _context.UserPropertyAccesses.RemoveRange(existing);

            if (vm.SelectedPropertyIds != null)
            {
                foreach (var pid in vm.SelectedPropertyIds)
                {
                    if (!allowedPropIds.Contains(pid)) continue;

                    _context.UserPropertyAccesses.Add(new UserPropertyAccess
                    {
                        ApplicationUserId = user.Id,
                        PropertyId = pid
                    });
                }
            }

            await _context.SaveChangesAsync();

            var currentUserRoles = (await _userManager.GetRolesAsync(user))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!)
                .ToList();
            var desiredRoles = (vm.SelectedRoles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!)
                .ToList();

            foreach (var role in desiredRoles.Except(currentUserRoles))
            {
                if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin") && role == "Admin")
                    continue;

                await _userManager.AddToRoleAsync(user, role);
            }

            foreach (var role in currentUserRoles.Except(desiredRoles))
            {
                if (role == "Admin" && user.Id == currentUser.Id)
                    continue;

                await _userManager.RemoveFromRoleAsync(user, role);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

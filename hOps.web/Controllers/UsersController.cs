using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);

            IQueryable<ApplicationUser> query = _userManager.Users;

            // If a Manager (not Admin), restrict by property access
            if (currentUserRoles.Contains("Manager") && !currentUserRoles.Contains("Admin"))
            {
                var myProps = _db.UserPropertyAccesses
                                 .Where(upa => upa.ApplicationUserId == currentUser.Id)
                                 .Select(upa => upa.PropertyId)
                                 .ToList();

                query = from u in _userManager.Users
                        join upa in _db.UserPropertyAccesses on u.Id equals upa.ApplicationUserId
                        where myProps.Contains(upa.PropertyId)
                        select u;
            }

            var users = await query
                .Include(u => u.UserPropertyAccesses)
                    .ThenInclude(upa => upa.Property)
                .ToListAsync();

            var userRolesMap = new Dictionary<string, string>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                userRolesMap[u.Id] = roles.FirstOrDefault() ?? "";
            }

            var vm = new UserIndexViewModel
            {
                Users = users,
                CurrentUserRoles = currentUserRoles,
                UserRoles = userRolesMap
            };

            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.AllRoles = await _roleManager.Roles
                .Select(r => r.Name)
                .ToListAsync();
            ViewBag.AllProperties = await _db.Properties.ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                MobilePhone = vm.MobilePhone
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError("", err.Description);
                }
                ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                ViewBag.AllProperties = await _db.Properties.ToListAsync();
                return View(vm);
            }

            if (!string.IsNullOrEmpty(vm.Role))
            {
                if (!await _roleManager.RoleExistsAsync(vm.Role))
                    await _roleManager.CreateAsync(new IdentityRole(vm.Role));
                await _userManager.AddToRoleAsync(user, vm.Role);
            }

            if (vm.PropertyIds != null)
            {
                foreach (var pid in vm.PropertyIds)
                {
                    _db.UserPropertyAccesses.Add(new UserPropertyAccess
                    {
                        ApplicationUserId = user.Id,
                        PropertyId = pid
                    });
                }
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();
            var currentRoles = await _userManager.GetRolesAsync(currentUser);

            if (currentRoles.Contains("Manager") && !currentRoles.Contains("Admin"))
            {
                var myProps = _db.UserPropertyAccesses
                    .Where(x => x.ApplicationUserId == currentUser.Id)
                    .Select(x => x.PropertyId)
                    .ToList();
                var theirProps = _db.UserPropertyAccesses
                    .Where(x => x.ApplicationUserId == id)
                    .Select(x => x.PropertyId)
                    .ToList();

                if (!theirProps.Any(pid => myProps.Contains(pid)))
                    return Forbid();
            }

            var userProps = _db.UserPropertyAccesses
                .Where(x => x.ApplicationUserId == id)
                .Select(x => x.PropertyId)
                .ToList();

            var roles = await _userManager.GetRolesAsync(user);

            var vm = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MobilePhone = user.MobilePhone,
                Role = roles.FirstOrDefault(),
                PropertyIds = userProps
            };

            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            ViewBag.AllProperties = await _db.Properties.ToListAsync();

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EditUserViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByIdAsync(vm.Id);
            if (user == null) return NotFound();

            user.FirstName = vm.FirstName;
            user.LastName = vm.LastName;
            user.MobilePhone = vm.MobilePhone;
            user.Email = vm.Email;
            user.UserName = vm.Email;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                    ModelState.AddModelError("", err.Description);

                ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                ViewBag.AllProperties = await _db.Properties.ToListAsync();
                return View(vm);
            }

            var existingRoles = await _userManager.GetRolesAsync(user);
            if (existingRoles.FirstOrDefault() != vm.Role)
            {
                await _userManager.RemoveFromRolesAsync(user, existingRoles);
                if (!string.IsNullOrEmpty(vm.Role))
                {
                    if (!await _roleManager.RoleExistsAsync(vm.Role))
                        await _roleManager.CreateAsync(new IdentityRole(vm.Role));
                    await _userManager.AddToRoleAsync(user, vm.Role);
                }
            }

            var existingAccess = _db.UserPropertyAccesses
                .Where(x => x.ApplicationUserId == vm.Id);
            _db.UserPropertyAccesses.RemoveRange(existingAccess);

            if (vm.PropertyIds != null)
            {
                foreach (var pid in vm.PropertyIds)
                {
                    _db.UserPropertyAccesses.Add(new UserPropertyAccess
                    {
                        ApplicationUserId = vm.Id,
                        PropertyId = pid
                    });
                }
            }

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                var myProps = _db.UserPropertyAccesses
                    .Where(x => x.ApplicationUserId == currentUser.Id)
                    .Select(x => x.PropertyId)
                    .ToList();
                var theirProps = _db.UserPropertyAccesses
                    .Where(x => x.ApplicationUserId == id)
                    .Select(x => x.PropertyId)
                    .ToList();

                if (!theirProps.Any(pid => myProps.Contains(pid)))
                    return Forbid();
            }

            var accessEntries = _db.UserPropertyAccesses
                .Where(x => x.ApplicationUserId == id);
            _db.UserPropertyAccesses.RemoveRange(accessEntries);

            await _userManager.DeleteAsync(user);

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}

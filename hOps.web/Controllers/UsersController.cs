using hOps.web.Data;
using hOps.web.Models;
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

        public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);

            IQueryable<ApplicationUser> query = _userManager.Users;

            if (currentUserRoles.Contains("Manager") && !currentUserRoles.Contains("Admin"))
            {
                var myProps = _db.UserPropertyAccesses
                                 .Where(upa => upa.ApplicationUserId == currentUser.Id)
                                 .Select(upa => upa.PropertyId);

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
                var role = (await _userManager.GetRolesAsync(u)).FirstOrDefault();
                userRolesMap[u.Id] = role ?? "";
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
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
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
                    ModelState.AddModelError("", err.Description);
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
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                var myProps = _db.UserPropertyAccesses.Where(x => x.ApplicationUserId == currentUser.Id).Select(x => x.PropertyId);
                var theirProps = _db.UserPropertyAccesses.Where(x => x.ApplicationUserId == id).Select(x => x.PropertyId);

                if (!theirProps.Any(pid => myProps.Contains(pid)))
                    return Forbid();
            }

            var vm = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MobilePhone = user.MobilePhone,
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault(),
                PropertyIds = _db.UserPropertyAccesses.Where(x => x.ApplicationUserId == id).Select(x => x.PropertyId).ToList()
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

            await _userManager.UpdateAsync(user);

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

            var existing = _db.UserPropertyAccesses.Where(x => x.ApplicationUserId == vm.Id);
            _db.UserPropertyAccesses.RemoveRange(existing);
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
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                var myProps = _db.UserPropertyAccesses.Where(x => x.ApplicationUserId == currentUser.Id).Select(x => x.PropertyId);
                var theirProps = _db.UserPropertyAccesses.Where(x => x.ApplicationUserId == id).Select(x => x.PropertyId);
                if (!theirProps.Any(pid => myProps.Contains(pid)))
                    return Forbid();
            }

            var access = _db.UserPropertyAccesses.Where(x => x.ApplicationUserId == id);
            _db.UserPropertyAccesses.RemoveRange(access);

            await _userManager.DeleteAsync(user);

            return RedirectToAction(nameof(Index));
        }
    }
}

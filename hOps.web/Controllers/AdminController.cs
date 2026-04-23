using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Net;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using hOps.web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AdminController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AdminController> _logger;
        private const string ExternalLoginUrl = "https://www.guestquest.net/Identity/Account/Login";
        private const int AccessRequestsPageSize = 100;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            ILogger<AdminController> logger)
            : base(db, userManager)
        {
            _roleManager = roleManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        // Admin landing
        public IActionResult Dashboard()
        {
            return View();
        }

        // List all users + roles + property access
        public async Task<IActionResult> Users(string? sortBy = null, string? sortDirection = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();

            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var vm = await BuildAdminUsersViewModelAsync(currentUser, currentRoles, null, sortBy, sortDirection);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser([Bind(Prefix = "CreateUser")] AdminCreateUserInputModel? input)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var isAdmin = currentRoles.Contains("Admin");
            input ??= new AdminCreateUserInputModel();

            input.Email = input.Email?.Trim();
            input.FirstName = input.FirstName?.Trim();
            input.LastName = input.LastName?.Trim();

            if (string.IsNullOrWhiteSpace(input.Email))
            {
                ModelState.AddModelError("CreateUser.Email", "Email is required.");
            }
            if (string.IsNullOrWhiteSpace(input.FirstName))
            {
                ModelState.AddModelError("CreateUser.FirstName", "First name is required.");
            }
            if (string.IsNullOrWhiteSpace(input.LastName))
            {
                ModelState.AddModelError("CreateUser.LastName", "Last name is required.");
            }

            if (!isAdmin && (input.SelectedRoles?.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)) ?? false))
            {
                ModelState.AddModelError("CreateUser.SelectedRoles", "Managers cannot assign the Admin role.");
            }

            if (!ModelState.IsValid)
            {
                var invalidVm = await BuildAdminUsersViewModelAsync(currentUser, currentRoles, input);
                return View(nameof(Users), invalidVm);
            }

            var existingUser = await _userManager.FindByEmailAsync(input.Email!);
            if (existingUser != null)
            {
                ModelState.AddModelError("CreateUser.Email", "An account with this email already exists.");
                var duplicateVm = await BuildAdminUsersViewModelAsync(currentUser, currentRoles, input);
                return View(nameof(Users), duplicateVm);
            }

            var requestedRoles = (input.SelectedRoles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validRoles = await _roleManager.Roles
                .Where(r => r.Name != null)
                .Select(r => r.Name!)
                .ToListAsync();
            var validRoleSet = new HashSet<string>(validRoles, StringComparer.OrdinalIgnoreCase);

            requestedRoles = requestedRoles
                .Where(r => validRoleSet.Contains(r))
                .ToList();

            if (!requestedRoles.Any())
            {
                requestedRoles.Add("User");
            }

            if (!isAdmin && requestedRoles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("CreateUser.SelectedRoles", "Managers cannot assign the Admin role.");
            }

            var selectedPropertyIds = (input.PropertyIds ?? new List<int>())
                .Distinct()
                .ToList();

            if (selectedPropertyIds.Any())
            {
                var validPropertyIds = await _context.Properties
                    .Where(p => selectedPropertyIds.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();
                var validPropertySet = validPropertyIds.ToHashSet();
                var invalidSelection = selectedPropertyIds.Count != validPropertySet.Count;
                selectedPropertyIds = selectedPropertyIds
                    .Where(validPropertySet.Contains)
                    .ToList();

                if (invalidSelection)
                {
                    ModelState.AddModelError("CreateUser.PropertyIds", "One or more selected properties were invalid.");
                }
            }

            if (!isAdmin)
            {
                var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(currentUser.Id);
                var unauthorizedSelection = selectedPropertyIds.Any(pid => !accessiblePropertyIds.Contains(pid));
                selectedPropertyIds = selectedPropertyIds
                    .Where(accessiblePropertyIds.Contains)
                    .ToList();

                if (unauthorizedSelection)
                {
                    ModelState.AddModelError("CreateUser.PropertyIds", "You can only assign properties that you manage.");
                }
            }

            input.PropertyIds = selectedPropertyIds;
            input.SelectedRoles = requestedRoles;

            if (!ModelState.IsValid)
            {
                var invalidVm = await BuildAdminUsersViewModelAsync(currentUser, currentRoles, input);
                return View(nameof(Users), invalidVm);
            }

            var user = new ApplicationUser
            {
                UserName = input.Email!,
                Email = input.Email!,
                FirstName = input.FirstName!,
                LastName = input.LastName!,
                MobilePhone = input.MobilePhone,
                PhoneNumber = input.MobilePhone,
                MustChangePassword = true,
                IsActive = true,
                DeactivatedAtUtc = null
            };

            var tempPassword = GenerateTemporaryPassword();
            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                var failedVm = await BuildAdminUsersViewModelAsync(currentUser, currentRoles, input);
                return View(nameof(Users), failedVm);
            }

            foreach (var role in requestedRoles)
            {
                await _userManager.AddToRoleAsync(user, role);
            }

            if (selectedPropertyIds.Any())
            {
                var newAccesses = selectedPropertyIds.Select(pid => new UserPropertyAccess
                {
                    ApplicationUserId = user.Id,
                    PropertyId = pid
                });
                _context.UserPropertyAccesses.AddRange(newAccesses);
                await _context.SaveChangesAsync();
            }

            var loginUrl = ExternalLoginUrl;
            var message = $@"
Hi {user.FirstName},<br/><br/>
An account has been created for you on GuestQuest.<br/>
Use your email and the temporary password below to log in:<br/>
<strong>Password:</strong> {tempPassword}<br/><br/>
<a href=""{loginUrl}"">Log in to GuestQuest</a><br/><br/>
Please change your password after logging in.<br/><br/>
GuestQuest Admin Team
";
            await _emailSender.SendEmailAsync(user.Email!, "Your GuestQuest account is ready", message);

            TempData["AdminUsersMessage"] = $"Created user {user.Email} and sent a temporary password.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["AdminUsersError"] = "Invalid user.";
                return RedirectToAction(nameof(Users));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (currentUser.Id == userId)
            {
                TempData["AdminUsersError"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null)
            {
                TempData["AdminUsersError"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var isAdmin = currentRoles.Contains("Admin");
            var targetRoles = await _userManager.GetRolesAsync(targetUser);

            if (!isAdmin)
            {
                if (targetRoles.Contains("Admin"))
                {
                    TempData["AdminUsersError"] = "You do not have permission to delete this user.";
                    return RedirectToAction(nameof(Users));
                }

                var accessiblePropertyIds = (await _context.UserPropertyAccesses
                        .Where(upa => upa.ApplicationUserId == currentUser.Id)
                        .Select(upa => upa.PropertyId)
                        .Distinct()
                        .ToListAsync())
                    .ToHashSet();

                var targetPropertyIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == targetUser.Id)
                    .Select(upa => upa.PropertyId)
                    .Distinct()
                    .ToListAsync();

                if (!targetPropertyIds.Any() || !targetPropertyIds.All(accessiblePropertyIds.Contains))
                {
                    TempData["AdminUsersError"] = "You do not have permission to delete this user.";
                    return RedirectToAction(nameof(Users));
                }
            }

            try
            {
                var deleteResult = await _userManager.DeleteAsync(targetUser);
                if (!deleteResult.Succeeded)
                {
                    var error = deleteResult.Errors.Select(e => e.Description).FirstOrDefault() ?? "Unable to delete user.";
                    TempData["AdminUsersError"] = error;
                }
                else
                {
                    var label = targetUser.Email ?? targetUser.UserName ?? "user";
                    TempData["AdminUsersMessage"] = $"Deleted user {label}.";
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Unable to delete user {UserId} due to related records.", userId);
                TempData["AdminUsersError"] = "This user has existing activity (logs, work orders, etc.) and cannot be deleted without removing those references. Consider revoking access instead.";
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DeactivateUser(string userId) => ChangeUserActivationAsync(userId, deactivate: true);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ReactivateUser(string userId) => ChangeUserActivationAsync(userId, deactivate: false);

        private async Task<IActionResult> ChangeUserActivationAsync(string userId, bool deactivate)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["AdminUsersError"] = "Invalid user.";
                return RedirectToAction(nameof(Users));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (string.Equals(currentUser.Id, userId, StringComparison.OrdinalIgnoreCase))
            {
                TempData["AdminUsersError"] = deactivate
                    ? "You cannot deactivate your own account."
                    : "You cannot reactivate your own account.";
                return RedirectToAction(nameof(Users));
            }

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null)
            {
                TempData["AdminUsersError"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var targetRoles = await _userManager.GetRolesAsync(targetUser);
            var isAdmin = currentRoles.Contains("Admin");

            if (!isAdmin)
            {
                if (targetRoles.Contains("Admin"))
                {
                    TempData["AdminUsersError"] = "You do not have permission to manage this user.";
                    return RedirectToAction(nameof(Users));
                }

                var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(currentUser.Id);
                var targetPropertyIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == targetUser.Id)
                    .Select(upa => upa.PropertyId)
                    .Distinct()
                    .ToListAsync();

                if (!targetPropertyIds.Any() || !targetPropertyIds.All(accessiblePropertyIds.Contains))
                {
                    TempData["AdminUsersError"] = "You do not have permission to manage this user.";
                    return RedirectToAction(nameof(Users));
                }
            }

            if (deactivate && !targetUser.IsActive)
            {
                TempData["AdminUsersError"] = "User is already deactivated.";
                return RedirectToAction(nameof(Users));
            }

            if (!deactivate && targetUser.IsActive)
            {
                TempData["AdminUsersError"] = "User is already active.";
                return RedirectToAction(nameof(Users));
            }

            targetUser.IsActive = !deactivate;
            targetUser.DeactivatedAtUtc = deactivate ? DateTime.UtcNow : null;

            var updateResult = await _userManager.UpdateAsync(targetUser);
            if (!updateResult.Succeeded)
            {
                TempData["AdminUsersError"] = updateResult.Errors.Select(e => e.Description).FirstOrDefault() ?? "Unable to update user.";
            }
            else
            {
                var label = targetUser.Email ?? targetUser.UserName ?? "user";
                TempData["AdminUsersMessage"] = deactivate
                    ? $"User {label} has been deactivated."
                    : $"User {label} has been reactivated.";
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["AdminUsersError"] = "Invalid user.";
                return RedirectToAction(nameof(Users));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (string.Equals(currentUser.Id, userId, StringComparison.OrdinalIgnoreCase))
            {
                TempData["AdminUsersError"] = "Use the Forgot Password option to reset your own password.";
                return RedirectToAction(nameof(Users));
            }

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null)
            {
                TempData["AdminUsersError"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var isAdmin = currentRoles.Contains("Admin");
            var targetRoles = await _userManager.GetRolesAsync(targetUser);

            if (!isAdmin)
            {
                if (targetRoles.Contains("Admin"))
                {
                    TempData["AdminUsersError"] = "You do not have permission to reset this user's password.";
                    return RedirectToAction(nameof(Users));
                }

                var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(currentUser.Id);
                var targetPropertyIds = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == targetUser.Id)
                    .Select(upa => upa.PropertyId)
                    .Distinct()
                    .ToListAsync();

                var canReset = targetPropertyIds.Count == 0 || accessiblePropertyIds.Overlaps(targetPropertyIds);
                if (!canReset)
                {
                    TempData["AdminUsersError"] = "You do not have permission to reset this user's password.";
                    return RedirectToAction(nameof(Users));
                }
            }

            var tempPassword = GenerateTemporaryPassword();
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(targetUser);
            var resetResult = await _userManager.ResetPasswordAsync(targetUser, resetToken, tempPassword);
            if (!resetResult.Succeeded)
            {
                var error = resetResult.Errors.Select(e => e.Description).FirstOrDefault() ?? "Unable to reset password.";
                TempData["AdminUsersError"] = error;
                return RedirectToAction(nameof(Users));
            }

            targetUser.MustChangePassword = true;
            await _userManager.UpdateAsync(targetUser);

            var loginUrl = ExternalLoginUrl;
            var message = $@"
Hi {targetUser.FirstName},<br/><br/>
Your HotelOps password was reset by an administrator.<br/>
Use this temporary password to sign in:<br/>
<strong>Password:</strong> {tempPassword}<br/><br/>
<a href=""{loginUrl}"">Log in to HotelOps</a><br/><br/>
Please change your password after logging in. If you did not expect this update, contact your administrator immediately.<br/><br/>
HotelOps Admin Team
";
            if (!string.IsNullOrWhiteSpace(targetUser.Email))
            {
                await _emailSender.SendEmailAsync(targetUser.Email, "HotelOps password reset", message);
            }

            var targetLabel = targetUser.Email ?? targetUser.UserName ?? "user";
            TempData["AdminUsersMessage"] = $"Password reset and email sent to {targetLabel}.";
            return RedirectToAction(nameof(Users));
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
            var isAdmin = currentRoles.Contains("Admin");
            var targetRoles = (await _userManager.GetRolesAsync(user)).ToList();

            if (!isAdmin && targetRoles.Contains("Admin"))
            {
                return Forbid();
            }

            HashSet<int> accessiblePropertyIds;
            if (isAdmin)
            {
                accessiblePropertyIds = allProperties.Select(p => p.Id).ToHashSet();
            }
            else
            {
                accessiblePropertyIds = (await _context.UserPropertyAccesses
                        .Where(upa => upa.ApplicationUserId == currentUser.Id)
                        .Select(upa => upa.PropertyId)
                        .Distinct()
                        .ToListAsync())
                    .ToHashSet();

                var canEdit = user.Id == currentUser.Id
                              || userProps.Count == 0
                              || accessiblePropertyIds.Overlaps(userProps);

                if (!canEdit)
                {
                    return Forbid();
                }
            }

            List<Property> assignableProps = allProperties;
            if (!isAdmin)
            {
                assignableProps = allProperties
                    .Where(p => accessiblePropertyIds.Contains(p.Id))
                    .ToList();
            }

            // Roles data
            var allRoles = await _roleManager.Roles
                .Where(r => r.Name != null)
                .Select(r => r.Name!)
                .ToListAsync();
            var userRoles = targetRoles
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
            var isAdmin = currentRoles.Contains("Admin");

            var targetRoles = (await _userManager.GetRolesAsync(user)).ToList();
            if (!isAdmin && targetRoles.Contains("Admin"))
            {
                return Forbid();
            }

            var targetPropertyIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            HashSet<int> accessiblePropertyIds;
            if (isAdmin)
            {
                accessiblePropertyIds = allProperties.Select(p => p.Id).ToHashSet();
            }
            else
            {
                accessiblePropertyIds = (await _context.UserPropertyAccesses
                        .Where(upa => upa.ApplicationUserId == currentUser.Id)
                        .Select(upa => upa.PropertyId)
                        .Distinct()
                        .ToListAsync())
                    .ToHashSet();

                var canEdit = user.Id == currentUser.Id
                              || targetPropertyIds.Count == 0
                              || accessiblePropertyIds.Overlaps(targetPropertyIds);

                if (!canEdit)
                {
                    return Forbid();
                }
            }

            // Determine allowed properties
            var allowedPropIds = accessiblePropertyIds;

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
            var currentUserRoles = targetRoles
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

        private async Task<AdminUsersPageViewModel> BuildAdminUsersViewModelAsync(
            ApplicationUser currentUser,
            IList<string> currentRoles,
            AdminCreateUserInputModel? formInput,
            string? sortBy = null,
            string? sortDirection = null)
        {
            var normalizedSortBy = NormalizeAdminUsersSortBy(sortBy);
            var normalizedSortDirection = NormalizeSortDirection(sortDirection);
            var isAdmin = currentRoles.Contains("Admin");
            HashSet<int> accessiblePropertyIds = new();
            if (!isAdmin)
            {
                accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(currentUser.Id);
            }

            var users = await _userManager.Users
                .OrderBy(u => u.Email)
                .ToListAsync();
            var userIds = users.Select(u => u.Id).ToList();

            var propertyAccesses = await _context.UserPropertyAccesses
                .Where(upa => userIds.Contains(upa.ApplicationUserId))
                .ToListAsync();

            var propertiesByUser = propertyAccesses
                .GroupBy(upa => upa.ApplicationUserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.PropertyId).ToList());

            var userViewModels = new List<UserWithAccessViewModel>();

            foreach (var u in users)
            {
                var propertyIds = propertiesByUser.TryGetValue(u.Id, out var ids)
                    ? ids
                    : new List<int>();
                var roles = (await _userManager.GetRolesAsync(u)).ToList();

                if (!isAdmin)
                {
                    if (roles.Contains("Admin"))
                    {
                        continue;
                    }

                    var canView = u.Id == currentUser.Id
                                  || propertyIds.Count == 0
                                  || accessiblePropertyIds.Overlaps(propertyIds);
                    if (!canView)
                    {
                        continue;
                    }
                }

                var canDelete = false;
                if (isAdmin)
                {
                    canDelete = u.Id != currentUser.Id;
                }
                else if (u.Id != currentUser.Id && propertyIds.Any())
                {
                    canDelete = propertyIds.All(pid => accessiblePropertyIds.Contains(pid));
                }

                var canReset = false;
                if (isAdmin)
                {
                    canReset = u.Id != currentUser.Id;
                }
                else if (u.Id != currentUser.Id && !roles.Contains("Admin"))
                {
                    canReset = propertyIds.Count == 0 || accessiblePropertyIds.Overlaps(propertyIds);
                }

                var canManageActivation = false;
                if (isAdmin)
                {
                    canManageActivation = u.Id != currentUser.Id;
                }
                else if (u.Id != currentUser.Id && !roles.Contains("Admin"))
                {
                    canManageActivation = propertyIds.Count == 0 || accessiblePropertyIds.Overlaps(propertyIds);
                }

                userViewModels.Add(new UserWithAccessViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Roles = roles,
                    PropertyIds = propertyIds,
                    CanDelete = canDelete,
                    CanResetPassword = canReset,
                    CanDeactivate = canManageActivation && u.IsActive,
                    CanReactivate = canManageActivation && !u.IsActive,
                    IsActive = u.IsActive,
                    LastLoginAtUtc = u.LastLoginAtUtc
                });
            }

            var propertyQuery = _context.Properties.AsQueryable();
            if (!isAdmin)
            {
                if (accessiblePropertyIds.Count == 0)
                {
                    propertyQuery = propertyQuery.Where(p => false);
                }
                else
                {
                    propertyQuery = propertyQuery.Where(p => accessiblePropertyIds.Contains(p.Id));
                }
            }

            var propertyOptions = await propertyQuery
                .OrderBy(p => p.Name)
                .Select(p => new AdminPropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code
                })
                .ToListAsync();

            var propertyNameLookup = propertyOptions.ToDictionary(p => p.Id, p => p.DisplayLabel);
            var sortedUsers = SortAdminUsers(userViewModels, propertyNameLookup, normalizedSortBy, normalizedSortDirection);

            var availableRoles = await _roleManager.Roles
                .Where(r => r.Name != null)
                .Select(r => r.Name!)
                .OrderBy(r => r)
                .ToListAsync();

            if (!isAdmin)
            {
                availableRoles = availableRoles
                    .Where(r => !string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new AdminUsersPageViewModel
            {
                Users = sortedUsers,
                CreateUser = formInput ?? new AdminCreateUserInputModel(),
                AvailableProperties = propertyOptions,
                AvailableRoles = availableRoles,
                PropertyNameLookup = propertyNameLookup,
                CanManageRoles = isAdmin || currentRoles.Contains("Manager"),
                SortBy = normalizedSortBy,
                SortDirection = normalizedSortDirection
            };
        }

        private static string NormalizeAdminUsersSortBy(string? sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return "email";
            }

            return sortBy.Trim().ToLowerInvariant() switch
            {
                "firstname" => "firstname",
                "lastname" => "lastname",
                "status" => "status",
                "roles" => "roles",
                "properties" => "properties",
                "lastlogin" => "lastlogin",
                _ => "email"
            };
        }

        private static string NormalizeSortDirection(string? sortDirection)
        {
            return string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }

        private static List<UserWithAccessViewModel> SortAdminUsers(
            IEnumerable<UserWithAccessViewModel> users,
            IReadOnlyDictionary<int, string> propertyLookup,
            string sortBy,
            string sortDirection)
        {
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

            string PropertyDisplay(UserWithAccessViewModel user)
            {
                if (user.PropertyIds == null || user.PropertyIds.Count == 0)
                {
                    return string.Empty;
                }

                var names = user.PropertyIds
                    .Select(id => propertyLookup.TryGetValue(id, out var name) ? name : id.ToString())
                    .ToList();

                return string.Join(", ", names);
            }

            string RolesDisplay(UserWithAccessViewModel user)
            {
                if (user.Roles == null || user.Roles.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join(", ", user.Roles);
            }

            IOrderedEnumerable<UserWithAccessViewModel> ordered;
            switch (sortBy)
            {
                case "firstname":
                    ordered = descending
                        ? users.OrderByDescending(u => u.FirstName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        : users.OrderBy(u => u.FirstName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    ordered = ordered.ThenBy(u => u.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                case "lastname":
                    ordered = descending
                        ? users.OrderByDescending(u => u.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        : users.OrderBy(u => u.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    ordered = ordered.ThenBy(u => u.FirstName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                case "status":
                    ordered = descending
                        ? users.OrderByDescending(u => u.IsActive ? 0 : 1)
                        : users.OrderBy(u => u.IsActive ? 0 : 1);
                    ordered = ordered.ThenBy(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                case "roles":
                    ordered = descending
                        ? users.OrderByDescending(RolesDisplay, StringComparer.OrdinalIgnoreCase)
                        : users.OrderBy(RolesDisplay, StringComparer.OrdinalIgnoreCase);
                    ordered = ordered.ThenBy(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                case "properties":
                    ordered = descending
                        ? users.OrderByDescending(PropertyDisplay, StringComparer.OrdinalIgnoreCase)
                        : users.OrderBy(PropertyDisplay, StringComparer.OrdinalIgnoreCase);
                    ordered = ordered.ThenBy(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                case "lastlogin":
                    ordered = descending
                        ? users.OrderByDescending(u => u.LastLoginAtUtc ?? DateTime.MinValue)
                        : users.OrderBy(u => u.LastLoginAtUtc ?? DateTime.MinValue);
                    ordered = ordered.ThenBy(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    ordered = descending
                        ? users.OrderByDescending(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        : users.OrderBy(u => u.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    ordered = ordered.ThenBy(u => u.FirstName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(u => u.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return ordered.ToList();
        }

        private async Task<HashSet<int>> GetAccessiblePropertyIdsAsync(string applicationUserId)
        {
            return (await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == applicationUserId)
                    .Select(upa => upa.PropertyId)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();
        }

        public async Task<IActionResult> AccessRequests(int page = 1)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var requests = await LoadVisibleAccessRequestsAsync(currentUser, roles);

            var totalCount = requests.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)AccessRequestsPageSize));
            var currentPage = Math.Clamp(page, 1, totalPages);
            var pagedRequests = requests
                .Skip((currentPage - 1) * AccessRequestsPageSize)
                .Take(AccessRequestsPageSize)
                .ToList();

            var viewModel = new AccessRequestsPageViewModel
            {
                Requests = pagedRequests,
                PageNumber = currentPage,
                TotalPages = totalPages,
                PageSize = AccessRequestsPageSize,
                TotalCount = totalCount
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<JsonResult> GetPendingAccessRequestCount()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(0);
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var requests = await LoadVisibleAccessRequestsAsync(currentUser, roles);
            return Json(requests.Count);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id, string? comments)
        {
            var request = await _context.UserAccessRequests.FindAsync(id);
            if (request == null)
                return NotFound();

            var (success, user, error) = await ApproveAccessRequestAsync(request, comments);
            if (!success || user == null)
            {
                TempData["Error"] = $"Failed to approve request: {error ?? "Unknown error."}";
                return RedirectToAction(nameof(AccessRequests));
            }

            TempData["Success"] = "Access granted and a temporary password email has been sent. Assign properties for the user below.";
            return RedirectToAction(nameof(EditUserProperties), new { userId = user.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string? comments)
        {
            var req = await _context.UserAccessRequests.FindAsync(id);
            if (req == null)
                return NotFound();

            await RejectAccessRequestAsync(req, comments);
            TempData["Success"] = "Request rejected and user notified.";

            return RedirectToAction(nameof(AccessRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessAccessRequests(string actionType, List<int> selectedRequestIds, string? comments, int? page)
        {
            if (selectedRequestIds == null || selectedRequestIds.Count == 0)
            {
                TempData["Error"] = "Select at least one request before performing a bulk action.";
                return RedirectToAction(nameof(AccessRequests), new { page = page ?? 1 });
            }

            var normalizedIds = selectedRequestIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                TempData["Error"] = "Unable to determine which requests were selected.";
                return RedirectToAction(nameof(AccessRequests));
            }

            var requests = new List<UserAccessRequest>();
            const int batchSize = 500;

            foreach (var batch in normalizedIds.Chunk(batchSize))
            {
                var batchResults = await _context.UserAccessRequests
                    .Where(r => batch.Contains(r.Id))
                    .ToListAsync();
                requests.AddRange(batchResults);
            }

            if (requests.Count == 0)
            {
                TempData["Error"] = "The selected access requests could not be found.";
                return RedirectToAction(nameof(AccessRequests), new { page = page ?? 1 });
            }

            var failures = new List<string>();
            var successCount = 0;
            var action = actionType?.Trim().ToLowerInvariant();

            if (action == "approve")
            {
                foreach (var request in requests)
                {
                    var (success, _, error) = await ApproveAccessRequestAsync(request, comments);
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failures.Add($"{request.Email}: {error ?? "Failed to approve."}");
                    }
                }

                if (successCount > 0)
                {
                    TempData["Success"] = $"Approved {successCount} access request(s). Assign properties for new users from the Users page.";
                }
            }
            else if (action == "reject")
            {
                foreach (var request in requests)
                {
                    try
                    {
                        await RejectAccessRequestAsync(request, comments);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{request.Email}: {ex.Message}");
                    }
                }

                if (successCount > 0)
                {
                    TempData["Success"] = $"Rejected {successCount} access request(s) and notified each user.";
                }
            }
            else
            {
                TempData["Error"] = "Invalid bulk action.";
                return RedirectToAction(nameof(AccessRequests), new { page = page ?? 1 });
            }

            if (failures.Count > 0)
            {
                TempData["Error"] = string.Join(" ", failures);
            }

            return RedirectToAction(nameof(AccessRequests), new { page = page ?? 1 });
        }

        private static string BuildCommentsHtml(string? comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                return string.Empty;
            }

            var encoded = WebUtility.HtmlEncode(comments.Trim());
            encoded = encoded
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "<br/>");

            return $"<br/><strong>Comments:</strong><br/>{encoded}<br/>";
        }

        private async Task<(bool Success, ApplicationUser? CreatedUser, string? ErrorMessage)> ApproveAccessRequestAsync(UserAccessRequest request, string? comments)
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                MobilePhone = request.MobilePhone,
                MustChangePassword = true,
                IsActive = true,
                DeactivatedAtUtc = null
            };

            var tempPassword = GenerateTemporaryPassword();
            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                return (false, null, string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, "User");

            var commentsHtml = BuildCommentsHtml(comments);
            var message = $@"
Hi {user.FirstName},<br/><br/>
Your access request for HotelOps has been <strong>approved</strong>.<br/>
You can now log in using your email and temporary password:<br/>
<strong>Password:</strong> {tempPassword}<br/><br/>
Please change your password after login.<br/>{commentsHtml}<br/>
HotelOps Admin Team
";
            await _emailSender.SendEmailAsync(user.Email, "HotelOps Access Approved", message);

            return (true, user, null);
        }

        private async Task RejectAccessRequestAsync(UserAccessRequest request, string? comments)
        {
            if (request.IsRejected)
            {
                return;
            }

            request.IsRejected = true;
            _context.UserAccessRequests.Update(request);
            await _context.SaveChangesAsync();

            var message = $@"
Hi {request.FirstName},<br/><br/>
Your access request for HotelOps was <strong>not approved</strong>.<br/>
If you believe this is in error, please contact your property manager.<br/>{BuildCommentsHtml(comments)}<br/>
Thank you,<br/>
HotelOps Admin Team
";
            await _emailSender.SendEmailAsync(request.Email, "Access Request Denied", message);
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

        private async Task<List<UserAccessRequest>> LoadVisibleAccessRequestsAsync(ApplicationUser currentUser, IList<string> currentRoles)
        {
            var pendingRequests = await _context.UserAccessRequests
                .Where(r => !r.IsRejected && !_context.Users.Any(u => u.Email == r.Email))
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();

            if (currentRoles.Contains("Admin"))
            {
                return pendingRequests;
            }

            var accessiblePropertyIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == currentUser.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();

            if (!accessiblePropertyIds.Any())
            {
                return new List<UserAccessRequest>();
            }

            var accessibleCodes = await _context.Properties
                .Where(p => accessiblePropertyIds.Contains(p.Id) && !string.IsNullOrWhiteSpace(p.Code))
                .Select(p => p.Code!)
                .ToListAsync();

            var normalizedCodes = new HashSet<string>(
                accessibleCodes
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code => code.Trim().ToUpperInvariant()));

            if (normalizedCodes.Count == 0)
            {
                return new List<UserAccessRequest>();
            }

            return pendingRequests
                .Where(r =>
                    !string.IsNullOrWhiteSpace(r.PropertyCode) &&
                    normalizedCodes.Contains(r.PropertyCode.Trim().ToUpperInvariant()))
                .ToList();
        }

        public IActionResult Settings()
        {
            return View();
        }
    }
}














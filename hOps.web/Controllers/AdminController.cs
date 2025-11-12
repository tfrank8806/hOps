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
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();

            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var vm = await BuildAdminUsersViewModelAsync(currentUser, currentRoles, null);

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
                MustChangePassword = true
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

            var loginUrl = Url.Page("/Account/Login", pageHandler: null, values: null, protocol: Request.Scheme) ?? string.Empty;
            var message = $@"
Hi {user.FirstName},<br/><br/>
An account has been created for you on HotelOps.<br/>
Use your email and the temporary password below to log in:<br/>
<strong>Password:</strong> {tempPassword}<br/><br/>
<a href=""{loginUrl}"">Log in to HotelOps</a><br/><br/>
Please change your password after logging in.<br/><br/>
HotelOps Admin Team
";
            await _emailSender.SendEmailAsync(user.Email!, "Your HotelOps account is ready", message);

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

            var loginUrl = Url.Page("/Account/Login", pageHandler: null, values: null, protocol: Request.Scheme) ?? string.Empty;
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
            AdminCreateUserInputModel? formInput)
        {
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

                userViewModels.Add(new UserWithAccessViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Roles = roles,
                    PropertyIds = propertyIds,
                    CanDelete = canDelete,
                    CanResetPassword = canReset
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
                Users = userViewModels,
                CreateUser = formInput ?? new AdminCreateUserInputModel(),
                AvailableProperties = propertyOptions,
                AvailableRoles = availableRoles,
                PropertyNameLookup = propertyNameLookup,
                CanManageRoles = isAdmin || currentRoles.Contains("Manager")
            };
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

        public async Task<IActionResult> AccessRequests()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var requests = await LoadVisibleAccessRequestsAsync(currentUser, roles);

            return View(requests);
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

            TempData["Success"] = "Access granted and a temporary password email has been sent. Assign properties for the user below.";
            return RedirectToAction(nameof(EditUserProperties), new { userId = user.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string? comments)
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
If you believe this is in error, please contact your property manager.<br/>{BuildCommentsHtml(comments)}<br/>
Thank you,<br/>
HotelOps Admin Team
";
            await _emailSender.SendEmailAsync(req.Email, "Access Request Denied", message);
            TempData["Success"] = "Request rejected and user notified.";

            return RedirectToAction(nameof(AccessRequests));
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














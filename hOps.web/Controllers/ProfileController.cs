using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    public class ProfileController : BaseController
    {
        private readonly IWebHostEnvironment _environment;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ProfileController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment environment)
            : base(context, userManager)
        {
            _signInManager = signInManager;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            return View(await BuildViewModel(user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileFormViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var accessiblePropertyIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            if (model.DefaultPropertyId.HasValue && !accessiblePropertyIds.Contains(model.DefaultPropertyId.Value))
            {
                ModelState.AddModelError("Profile.DefaultPropertyId", "You do not have access to the selected property.");
            }

            string? resolvedTimeZoneId = null;
            if (!string.IsNullOrWhiteSpace(model.TimeZoneId))
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(model.TimeZoneId.Trim());
                    resolvedTimeZoneId = tz.Id;
                }
                catch (TimeZoneNotFoundException)
                {
                    ModelState.AddModelError("Profile.TimeZoneId", "Please select a valid time zone.");
                }
                catch (InvalidTimeZoneException)
                {
                    ModelState.AddModelError("Profile.TimeZoneId", "Please select a valid time zone.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.ProfilePhotoPath = user.ProfilePhotoPath;
                if (model.DefaultPropertyId == null)
                {
                    model.DefaultPropertyId = user.DefaultPropertyId;
                }
                if (string.IsNullOrWhiteSpace(model.TimeZoneId))
                {
                    model.TimeZoneId = user.TimeZoneId;
                }

                return View("Index", await BuildViewModel(user, model));
            }

            var previousPhotoPath = user.ProfilePhotoPath;
            var previousDefaultPropertyId = user.DefaultPropertyId;
            string? newPhotoPhysicalPath = null;

            user.FirstName = model.FirstName ?? string.Empty;
            user.LastName = model.LastName ?? string.Empty;
            user.MobilePhone = model.PhoneNumber;
            user.PhoneNumber = model.PhoneNumber;

            var email = model.Email?.Trim();
            model.Email = email;
            user.Email = email;
            user.UserName = email;
            user.NormalizedEmail = email?.ToUpperInvariant();
            user.NormalizedUserName = email?.ToUpperInvariant();

            if (model.ProfilePhoto != null && model.ProfilePhoto.Length > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profile-photos");
                Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(model.ProfilePhoto.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await model.ProfilePhoto.CopyToAsync(stream);
                }

                user.ProfilePhotoPath = $"/uploads/profile-photos/{fileName}";
                model.ProfilePhotoPath = user.ProfilePhotoPath;
                newPhotoPhysicalPath = filePath;
            }
            else
            {
                model.ProfilePhotoPath = user.ProfilePhotoPath;
            }

            var selectedDefaultPropertyId = model.DefaultPropertyId.HasValue && accessiblePropertyIds.Contains(model.DefaultPropertyId.Value)
                ? model.DefaultPropertyId.Value
                : (int?)null;

            user.DefaultPropertyId = selectedDefaultPropertyId;
            user.TimeZoneId = resolvedTimeZoneId ?? user.TimeZoneId ?? TimeZoneInfo.Utc.Id;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    var key = error.Code switch
                    {
                        "DuplicateUserName" => "Profile.Email",
                        "DuplicateEmail" => "Profile.Email",
                        "InvalidUserName" => "Profile.Email",
                        _ => string.Empty
                    };

                    ModelState.AddModelError(key, error.Description);
                }

                if (newPhotoPhysicalPath != null && System.IO.File.Exists(newPhotoPhysicalPath))
                {
                    System.IO.File.Delete(newPhotoPhysicalPath);
                    user.ProfilePhotoPath = previousPhotoPath;
                    model.ProfilePhotoPath = previousPhotoPath;
                }

                user.DefaultPropertyId = previousDefaultPropertyId;

                return View("Index", await BuildViewModel(user, model));
            }

            if (!string.IsNullOrWhiteSpace(previousPhotoPath) &&
                !string.Equals(previousPhotoPath, user.ProfilePhotoPath, StringComparison.OrdinalIgnoreCase))
            {
                var existingPath = Path.Combine(_environment.WebRootPath, previousPhotoPath.TrimStart('/')
                    .Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(existingPath))
                {
                    System.IO.File.Delete(existingPath);
                }
            }

            if (user.DefaultPropertyId.HasValue)
            {
                HttpContext.Session.SetInt32("CurrentPropertyId", user.DefaultPropertyId.Value);
            }
            else if (HttpContext.Session.GetInt32("CurrentPropertyId") is int currentId && !accessiblePropertyIds.Contains(currentId))
            {
                HttpContext.Session.Remove("CurrentPropertyId");
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["ProfileUpdated"] = true;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmailPreferences([Bind(Prefix = "EmailPreferences")] EmailPreferencesViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var allDepartmentIds = await _context.Departments
                .OrderBy(d => d.Name)
                .Select(d => d.Id)
                .ToListAsync();

            var validDepartmentIds = new HashSet<int>(allDepartmentIds);

            var selectedDepartmentIds = model.SelectedDepartmentIds?
                .Where(id => validDepartmentIds.Contains(id))
                .Distinct()
                .ToList() ?? new List<int>();

            user.EmailOnMessage = model.EmailOnMessage;
            user.EmailOnMention = model.EmailOnMention;
            user.EmailOnWorkOrderDepartment = model.EmailOnWorkOrderDepartment;
            user.EmailOnLogEntry = model.EmailOnLogEntry;
            user.EmailDailySummary = model.EmailDailySummary;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                model.SelectedDepartmentIds = selectedDepartmentIds;
                return View("Index", await BuildViewModel(user, emailPreferences: model));
            }

            var existingSubscriptions = await _context.UserDepartmentSubscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();

            var toRemove = existingSubscriptions
                .Where(s => !selectedDepartmentIds.Contains(s.DepartmentId))
                .ToList();

            if (toRemove.Any())
            {
                _context.UserDepartmentSubscriptions.RemoveRange(toRemove);
            }

            var existingDepartmentIds = existingSubscriptions
                .Select(s => s.DepartmentId)
                .ToHashSet();

            foreach (var departmentId in selectedDepartmentIds)
            {
                if (!existingDepartmentIds.Contains(departmentId))
                {
                    _context.UserDepartmentSubscriptions.Add(new UserDepartmentSubscription
                    {
                        UserId = user.Id,
                        DepartmentId = departmentId
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["EmailPreferencesUpdated"] = true;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordFormViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            if (!ModelState.IsValid)
            {
                return View("Index", await BuildViewModel(user, changePassword: model));
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword!, model.NewPassword!);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    var targetKey = error.Code switch
                    {
                        "PasswordMismatch" => "ChangePassword.CurrentPassword",
                        _ => "ChangePassword.NewPassword"
                    };

                    ModelState.AddModelError(targetKey, error.Description);
                }

                return View("Index", await BuildViewModel(user, changePassword: model));
            }

            if (user.MustChangePassword)
            {
                user.MustChangePassword = false;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return View("Index", await BuildViewModel(user, changePassword: model));
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["PasswordChanged"] = true;
            return RedirectToAction(nameof(Index));
        }

        private async Task<MyProfileViewModel> BuildViewModel(
            ApplicationUser user,
            ProfileFormViewModel? profile = null,
            ChangePasswordFormViewModel? changePassword = null,
            EmailPreferencesViewModel? emailPreferences = null)
        {
            var profileVm = profile ?? new ProfileFormViewModel
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.MobilePhone ?? user.PhoneNumber,
                ProfilePhotoPath = user.ProfilePhotoPath
            };

            profileVm.ProfilePhotoPath ??= user.ProfilePhotoPath;
            profileVm.DefaultPropertyId ??= user.DefaultPropertyId;
            var userTimeZoneId = string.IsNullOrWhiteSpace(user.TimeZoneId) ? TimeZoneInfo.Utc.Id : user.TimeZoneId;
            profileVm.TimeZoneId = string.IsNullOrWhiteSpace(profileVm.TimeZoneId)
                ? userTimeZoneId
                : profileVm.TimeZoneId;

            var accessibleProperties = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.Property)
                .OrderBy(p => p.Name)
                .ToListAsync();

            if (profileVm.DefaultPropertyId.HasValue && accessibleProperties.All(p => p.Id != profileVm.DefaultPropertyId.Value))
            {
                profileVm.DefaultPropertyId = null;
            }

            var propertyOptions = accessibleProperties
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} ({p.Code})",
                    Selected = profileVm.DefaultPropertyId == p.Id
                })
                .ToList();

            var selectedTimeZoneId = profileVm.TimeZoneId?.Trim();
            var timeZoneOptions = TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => new SelectListItem
                {
                    Value = tz.Id,
                    Text = $"{tz.DisplayName} ({tz.Id})",
                    Selected = string.Equals(tz.Id, selectedTimeZoneId, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            var changePasswordVm = changePassword ?? new ChangePasswordFormViewModel();

            var departments = await _context.Departments
                .OrderBy(d => d.Name)
                .Select(d => new { d.Id, d.Name })
                .ToListAsync();

            List<int> subscriptionIds;
            if (emailPreferences != null)
            {
                subscriptionIds = emailPreferences.SelectedDepartmentIds?
                    .Distinct()
                    .ToList() ?? new List<int>();
            }
            else
            {
                subscriptionIds = await _context.UserDepartmentSubscriptions
                    .Where(s => s.UserId == user.Id)
                    .Select(s => s.DepartmentId)
                    .ToListAsync();
            }

            var validDepartmentIds = new HashSet<int>(departments.Select(d => d.Id));
            subscriptionIds = subscriptionIds
                .Where(validDepartmentIds.Contains)
                .ToList();

            var emailPreferencesVm = emailPreferences ?? new EmailPreferencesViewModel
            {
                EmailOnMessage = user.EmailOnMessage,
                EmailOnMention = user.EmailOnMention,
                EmailOnWorkOrderDepartment = user.EmailOnWorkOrderDepartment,
                EmailOnLogEntry = user.EmailOnLogEntry,
                EmailDailySummary = user.EmailDailySummary,
                SelectedDepartmentIds = subscriptionIds.ToList()
            };

            emailPreferencesVm.SelectedDepartmentIds = subscriptionIds.ToList();
            emailPreferencesVm.DepartmentOptions = departments
                .Select(d => new EmailPreferenceDepartmentOption
                {
                    Id = d.Id,
                    Name = d.Name ?? string.Empty,
                    Selected = subscriptionIds.Contains(d.Id)
                })
                .ToList();

            return new MyProfileViewModel
            {
                Profile = profileVm,
                ChangePassword = changePasswordVm,
                PropertyOptions = propertyOptions,
                TimeZoneOptions = timeZoneOptions,
                EmailPreferences = emailPreferencesVm
            };
        }
    }
}

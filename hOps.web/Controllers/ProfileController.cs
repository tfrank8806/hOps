using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
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
                    resolvedTimeZoneId = DefaultTimeZoneProvider.NormalizeForStorage(tz.Id);
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
                    model.TimeZoneId = DefaultTimeZoneProvider.GetEffectiveTimeZoneId(user.TimeZoneId);
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
            var desiredTimeZoneId = resolvedTimeZoneId ?? user.TimeZoneId;
            user.TimeZoneId = DefaultTimeZoneProvider.NormalizeForStorage(desiredTimeZoneId);

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

            var accessiblePropertyIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();
            var accessiblePropertySet = new HashSet<int>(accessiblePropertyIds);

            var departmentInfo = await _context.Departments
                .Where(d => d.PropertyId.HasValue && accessiblePropertySet.Contains(d.PropertyId.Value))
                .Select(d => new { d.Id, PropertyId = d.PropertyId!.Value, d.Name })
                .ToListAsync();
            var validDepartmentIds = new HashSet<int>(departmentInfo.Select(d => d.Id));
            var departmentLookup = departmentInfo
                .GroupBy(d => d.PropertyId)
                .ToDictionary(g => g.Key, g => g
                    .Select(x => new EmailPreferenceDepartmentOption
                    {
                        Id = x.Id,
                        Name = x.Name ?? string.Empty
                    })
                    .ToList());

            var propertyOptionModels = model.PropertyOptions ?? new List<EmailPreferencePropertyOption>();
            var validPropertyOptions = propertyOptionModels
                .Where(po => accessiblePropertySet.Contains(po.Id))
                .Select(po => new EmailPreferencePropertyOption
                {
                    Id = po.Id,
                    Name = po.Name ?? string.Empty,
                    IncludeInLogAlerts = po.IncludeInLogAlerts,
                    IncludeInDailySummary = po.IncludeInDailySummary,
                    IncludeInWorkOrderAlerts = po.IncludeInWorkOrderAlerts,
                    Departments = po.Departments ?? new List<EmailPreferenceDepartmentOption>()
                })
                .ToList();

            foreach (var propertyOption in validPropertyOptions)
            {
                var allowedDepartments = departmentLookup.TryGetValue(propertyOption.Id, out var deptOptions)
                    ? deptOptions
                    : new List<EmailPreferenceDepartmentOption>();

                var selectedDepartmentIds = new HashSet<int>(propertyOption.Departments?
                    .Where(d => d.Selected && validDepartmentIds.Contains(d.Id))
                    .Select(d => d.Id) ?? Enumerable.Empty<int>());

                propertyOption.Departments = allowedDepartments
                    .Select(d => new EmailPreferenceDepartmentOption
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Selected = selectedDepartmentIds.Contains(d.Id)
                    })
                    .ToList();

                propertyOption.SelectedDepartmentIds = propertyOption.Departments
                    .Where(d => d.Selected)
                    .Select(d => d.Id)
                    .Distinct()
                    .ToList();
            }

            model.PropertyOptions = validPropertyOptions;
            model.SelectedDepartmentIds = validPropertyOptions
                .SelectMany(po => po.SelectedDepartmentIds)
                .Distinct()
                .ToList();
            model.SelectedPropertyIds = validPropertyOptions
                .Select(po => po.Id)
                .Distinct()
                .ToList();

            if (!ModelState.IsValid)
            {
                return View("Index", await BuildViewModel(user, emailPreferences: model));
            }

            user.EmailOnMessage = model.EmailOnMessage;
            user.EmailOnMention = model.EmailOnMention;
            user.EmailOnWorkOrderDepartment = model.EmailOnWorkOrderDepartment;
            user.EmailOnLogEntry = model.EmailOnLogEntry;
            user.EmailDailySummary = model.EmailDailySummary;
            user.EmailOnSchedulePosted = model.EmailOnSchedulePosted;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("Index", await BuildViewModel(user, emailPreferences: model));
            }

            var existingDepartmentSubscriptions = await _context.UserDepartmentSubscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();

            var aggregatedDepartmentIds = model.PropertyOptions
                .SelectMany(po => po.SelectedDepartmentIds)
                .Distinct()
                .ToList();

            var deptSubscriptionsToRemove = existingDepartmentSubscriptions
                .Where(s => !aggregatedDepartmentIds.Contains(s.DepartmentId))
                .ToList();
            if (deptSubscriptionsToRemove.Any())
            {
                _context.UserDepartmentSubscriptions.RemoveRange(deptSubscriptionsToRemove);
            }

            var existingDepartmentIds = existingDepartmentSubscriptions
                .Select(s => s.DepartmentId)
                .ToHashSet();
            foreach (var departmentId in aggregatedDepartmentIds)
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

            var existingPropertyPrefs = await _context.UserPropertyEmailSubscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();
            var existingPrefMap = existingPropertyPrefs.ToDictionary(s => s.PropertyId);
            var propertyIdsToKeep = new HashSet<int>();

            foreach (var propertyOption in model.PropertyOptions)
            {
                propertyIdsToKeep.Add(propertyOption.Id);
                if (!existingPrefMap.TryGetValue(propertyOption.Id, out var pref))
                {
                    pref = new UserPropertyEmailSubscription
                    {
                        UserId = user.Id,
                        PropertyId = propertyOption.Id
                    };
                    _context.UserPropertyEmailSubscriptions.Add(pref);
                    existingPrefMap[propertyOption.Id] = pref;
                }

                pref.IncludeInLogAlerts = propertyOption.IncludeInLogAlerts;
                pref.IncludeInDailySummary = propertyOption.IncludeInDailySummary;
                pref.IncludeInWorkOrderAlerts = propertyOption.IncludeInWorkOrderAlerts;
            }

            var propertyPrefsToRemove = existingPropertyPrefs
                .Where(pref => !propertyIdsToKeep.Contains(pref.PropertyId))
                .ToList();
            if (propertyPrefsToRemove.Any())
            {
                _context.UserPropertyEmailSubscriptions.RemoveRange(propertyPrefsToRemove);
            }

            await _context.SaveChangesAsync();
            TempData["EmailPreferencesUpdated"] = true;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDepartments([Bind(Prefix = "DepartmentAssignments")] DepartmentAssignmentsViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var accessiblePropertyIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();

            var accessibleDepartments = await _context.Departments
                .Where(d => d.PropertyId.HasValue && accessiblePropertyIds.Contains(d.PropertyId.Value))
                .OrderBy(d => d.Name)
                .ToListAsync();

            var validDepartmentIds = accessibleDepartments
                .Select(d => d.Id)
                .ToHashSet();

            var selectedIds = model.Options?
                .Where(o => o.Selected && validDepartmentIds.Contains(o.Id))
                .Select(o => o.Id)
                .Distinct()
                .ToList() ?? new List<int>();

            var existingSubscriptions = await _context.UserDepartmentSubscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();

            var toRemove = existingSubscriptions
                .Where(s => !selectedIds.Contains(s.DepartmentId))
                .ToList();

            if (toRemove.Any())
            {
                _context.UserDepartmentSubscriptions.RemoveRange(toRemove);
            }

            var existingIds = existingSubscriptions
                .Select(s => s.DepartmentId)
                .ToHashSet();

            foreach (var departmentId in selectedIds)
            {
                if (!existingIds.Contains(departmentId))
                {
                    _context.UserDepartmentSubscriptions.Add(new UserDepartmentSubscription
                    {
                        UserId = user.Id,
                        DepartmentId = departmentId
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["DepartmentAssignmentsUpdated"] = true;

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
            EmailPreferencesViewModel? emailPreferences = null,
            DepartmentAssignmentsViewModel? departmentAssignments = null)
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
            var userTimeZoneId = DefaultTimeZoneProvider.NormalizeForStorage(user.TimeZoneId);
            var preferredTimeZoneId = string.IsNullOrWhiteSpace(profileVm.TimeZoneId)
                ? userTimeZoneId
                : profileVm.TimeZoneId!.Trim();
            profileVm.TimeZoneId = DefaultTimeZoneProvider.NormalizeForStorage(preferredTimeZoneId);

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

            var accessiblePropertyIdSet = new HashSet<int>(accessibleProperties.Select(p => p.Id));

            var propertyPreferences = await _context.UserPropertyEmailSubscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();
            var propertyPreferenceMap = propertyPreferences.ToDictionary(s => s.PropertyId);

            var userDepartmentIds = await _context.UserDepartmentSubscriptions
                .Where(s => s.UserId == user.Id)
                .Select(s => s.DepartmentId)
                .ToListAsync();
            var userDepartmentSet = new HashSet<int>(userDepartmentIds);

            var departmentEntities = await _context.Departments
                .Where(d => d.PropertyId.HasValue && accessiblePropertyIdSet.Contains(d.PropertyId.Value))
                .OrderBy(d => d.Name)
                .ToListAsync();
            var departmentsByProperty = departmentEntities
                .GroupBy(d => d.PropertyId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var assignmentOptions = departmentEntities
                .Select(d =>
                {
                    var property = accessibleProperties.FirstOrDefault(p => p.Id == d.PropertyId);
                    var propertyLabel = property == null
                        ? "General"
                        : string.IsNullOrWhiteSpace(property.Code)
                            ? property.Name
                            : $"{property.Name} ({property.Code})";

                    return new DepartmentAssignmentOption
                    {
                        Id = d.Id,
                        Name = d.Name ?? "Unnamed Department",
                        PropertyLabel = propertyLabel,
                        Selected = userDepartmentSet.Contains(d.Id)
                    };
                })
                .OrderBy(o => o.PropertyLabel)
                .ThenBy(o => o.Name)
                .ToList();

            if (departmentAssignments?.Options?.Any() == true)
            {
                var postedSelections = departmentAssignments.Options
                    .Where(o => o.Selected)
                    .Select(o => o.Id)
                    .ToHashSet();

                foreach (var option in assignmentOptions)
                {
                    option.Selected = postedSelections.Contains(option.Id);
                }
            }

            var assignmentsVm = departmentAssignments ?? new DepartmentAssignmentsViewModel();
            assignmentsVm.Options = assignmentOptions;

            var propertyPreferenceOptions = new List<EmailPreferencePropertyOption>();
            foreach (var property in accessibleProperties)
            {
                propertyPreferenceMap.TryGetValue(property.Id, out var preference);

                var departmentOptions = departmentsByProperty.TryGetValue(property.Id, out var deptList)
                    ? deptList
                    : new List<Department>();

                var departmentSelections = departmentOptions
                    .Select(d => new EmailPreferenceDepartmentOption
                    {
                        Id = d.Id,
                        Name = d.Name ?? string.Empty,
                        Selected = userDepartmentSet.Contains(d.Id)
                    })
                    .ToList();

                propertyPreferenceOptions.Add(new EmailPreferencePropertyOption
                {
                    Id = property.Id,
                    Name = string.IsNullOrWhiteSpace(property.Code) ? property.Name : $"{property.Name} ({property.Code})",
                    IncludeInLogAlerts = preference?.IncludeInLogAlerts ?? true,
                    IncludeInDailySummary = preference?.IncludeInDailySummary ?? true,
                    IncludeInWorkOrderAlerts = preference?.IncludeInWorkOrderAlerts ?? true,
                    Departments = departmentSelections,
                    SelectedDepartmentIds = departmentSelections.Where(d => d.Selected).Select(d => d.Id).ToList()
                });
            }

            if (emailPreferences?.PropertyOptions != null && emailPreferences.PropertyOptions.Any())
            {
                var postedOptions = emailPreferences.PropertyOptions.ToDictionary(po => po.Id);
                foreach (var option in propertyPreferenceOptions)
                {
                    if (postedOptions.TryGetValue(option.Id, out var posted))
                    {
                        option.IncludeInLogAlerts = posted.IncludeInLogAlerts;
                        option.IncludeInDailySummary = posted.IncludeInDailySummary;
                        option.IncludeInWorkOrderAlerts = posted.IncludeInWorkOrderAlerts;

                        var postedDepartmentIds = posted.Departments?
                            .Where(d => d.Selected)
                            .Select(d => d.Id)
                            .ToHashSet() ?? new HashSet<int>();

                        foreach (var deptOption in option.Departments)
                        {
                            deptOption.Selected = postedDepartmentIds.Contains(deptOption.Id);
                        }

                        option.SelectedDepartmentIds = option.Departments
                            .Where(d => d.Selected)
                            .Select(d => d.Id)
                            .ToList();
                    }
                }
            }

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

            var emailPreferencesVm = emailPreferences ?? new EmailPreferencesViewModel
            {
                EmailOnMessage = user.EmailOnMessage,
                EmailOnMention = user.EmailOnMention,
                EmailOnWorkOrderDepartment = user.EmailOnWorkOrderDepartment,
                EmailOnLogEntry = user.EmailOnLogEntry,
                EmailDailySummary = user.EmailDailySummary,
                EmailOnSchedulePosted = user.EmailOnSchedulePosted
            };

            emailPreferencesVm.PropertyOptions = propertyPreferenceOptions;
            emailPreferencesVm.SelectedDepartmentIds = propertyPreferenceOptions
                .SelectMany(p => p.SelectedDepartmentIds)
                .Distinct()
                .ToList();
            emailPreferencesVm.SelectedPropertyIds = propertyPreferenceOptions
                .Where(p => p.IncludeInLogAlerts || p.IncludeInDailySummary || p.IncludeInWorkOrderAlerts)
                .Select(p => p.Id)
                .Distinct()
                .ToList();
            emailPreferencesVm.DepartmentOptions = new List<EmailPreferenceDepartmentOption>();

            return new MyProfileViewModel
            {
                Profile = profileVm,
                ChangePassword = changePasswordVm,
                PropertyOptions = propertyOptions,
                TimeZoneOptions = timeZoneOptions,
                EmailPreferences = emailPreferencesVm,
                DepartmentAssignments = assignmentsVm
            };
        }
    }
}

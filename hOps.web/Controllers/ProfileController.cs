using System;
using System.IO;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

            return View(BuildViewModel(user));
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

            if (!ModelState.IsValid)
            {
                model.ProfilePhotoPath = user.ProfilePhotoPath;
                return View("Index", BuildViewModel(user, model));
            }

            var previousPhotoPath = user.ProfilePhotoPath;
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

                return View("Index", BuildViewModel(user, model));
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

            await _signInManager.RefreshSignInAsync(user);
            TempData["ProfileUpdated"] = true;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordFormViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            if (!ModelState.IsValid)
            {
                return View("Index", BuildViewModel(user, changePassword: model));
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

                return View("Index", BuildViewModel(user, changePassword: model));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["PasswordChanged"] = true;
            return RedirectToAction(nameof(Index));
        }

        private MyProfileViewModel BuildViewModel(
            ApplicationUser user,
            ProfileFormViewModel? profile = null,
            ChangePasswordFormViewModel? changePassword = null)
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
            var changePasswordVm = changePassword ?? new ChangePasswordFormViewModel();

            return new MyProfileViewModel
            {
                Profile = profileVm,
                ChangePassword = changePasswordVm
            };
        }
    }
}

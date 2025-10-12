#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _dbContext;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _dbContext = dbContext;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            [Display(Name = "Mobile Phone")]
            [Phone]
            public string MobilePhone { get; set; }

            [Required]
            [Display(Name = "Property Code")]
            public string PropertyCode { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var hasher = new PasswordHasher<ApplicationUser>();
                var hashedPassword = hasher.HashPassword(new ApplicationUser(), Input.Password);

                var request = new UserAccessRequest
                {
                    FirstName = Input.FirstName,
                    LastName = Input.LastName,
                    Email = Input.Email,
                    MobilePhone = Input.MobilePhone,
                    PropertyCode = Input.PropertyCode,
                    PasswordHash = hashedPassword,
                    RequestedAt = DateTime.UtcNow,
                    IsApproved = false,
                    IsRejected = false
                };

                _dbContext.UserAccessRequests.Add(request);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Access request submitted for approval.");

                // Send email to all managers/admins
                var adminRoleIds = _dbContext.Roles
                    .Where(r => r.Name == "Manager" || r.Name == "Admin")
                    .Select(r => r.Id)
                    .ToList();

                var managerUsers = await (from ur in _dbContext.UserRoles
                                          join u in _dbContext.Users on ur.UserId equals u.Id
                                          where adminRoleIds.Contains(ur.RoleId)
                                          select u).ToListAsync();

                foreach (var mgr in managerUsers)
                {
                    var approveUrl = Url.Page("/Admin/AccessRequests", null, null, Request.Scheme);
                    var message = $@"
Hello {mgr.UserName},<br/><br/>
A new user has requested access:<br/>
Name: {request.FirstName} {request.LastName}<br/>
Email: {request.Email}<br/>
Property Code: {request.PropertyCode}<br/><br/>
Please <a href='{HtmlEncoder.Default.Encode(approveUrl)}'>review pending requests</a>.
";
                    await _emailSender.SendEmailAsync(mgr.Email, "New Access Request", message);
                }

                return RedirectToPage("./RegisterConfirmation", new { email = Input.Email });
            }

            return Page();
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new InvalidOperationException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}

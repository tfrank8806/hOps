#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Controllers;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using hOps.web.Options;
using hOps.web.Services;
using Microsoft.Extensions.Options;

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
        private readonly ICaptchaValidator _captchaValidator;
        private readonly CaptchaOptions _captchaOptions;
        private readonly IDataProtector _captchaProtector;
        private const string SimpleCaptchaProtectorPurpose = "RegisterSimpleCaptchaToken";
        private const int SimpleCaptchaExpiryMinutes = 10;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext dbContext,
            ICaptchaValidator captchaValidator,
            IOptions<CaptchaOptions> captchaOptions,
            IDataProtectionProvider dataProtectionProvider)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _dbContext = dbContext;
            _captchaValidator = captchaValidator;
            _captchaOptions = captchaOptions.Value ?? new CaptchaOptions();
            _captchaProtector = dataProtectionProvider.CreateProtector(SimpleCaptchaProtectorPurpose);
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }
        public bool IsCaptchaEnabled { get; private set; }
        public string CaptchaSiteKey { get; private set; } = string.Empty;
        public string SimpleCaptchaQuestion { get; private set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Security question")]
        public string SimpleCaptchaAnswer { get; set; } = string.Empty;

        [BindProperty]
        public string SimpleCaptchaToken { get; set; } = string.Empty;

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

        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            RefreshCaptchaSettings();
            PrepareCaptchaForDisplay();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            var postedSimpleCaptchaAnswer = SimpleCaptchaAnswer;
            var postedSimpleCaptchaToken = SimpleCaptchaToken;

            ReturnUrl = returnUrl ?? Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            RefreshCaptchaSettings();
            PrepareCaptchaForDisplay();

            if (IsCaptchaEnabled)
            {
                var captchaToken = ExtractCaptchaToken();
                if (string.IsNullOrWhiteSpace(captchaToken))
                {
                    ModelState.AddModelError(string.Empty, "Please complete the CAPTCHA challenge.");
                }
                else
                {
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var captchaValid = await _captchaValidator.ValidateAsync(
                        captchaToken,
                        remoteIp,
                        HttpContext.RequestAborted);

                    if (!captchaValid)
                    {
                        ModelState.AddModelError(string.Empty, "CAPTCHA validation failed. Please try again.");
                    }
                }
            }
            else
            {
                if (!ValidateSimpleCaptcha(postedSimpleCaptchaAnswer, postedSimpleCaptchaToken, out var captchaError))
                {
                    ModelState.AddModelError(string.Empty, captchaError);
                }
            }

            if (ModelState.IsValid)
            {
                var hasher = new PasswordHasher<ApplicationUser>();
                var hashedPassword = hasher.HashPassword(new ApplicationUser(), "TempPassword@123");

                var normalizedPropertyCode = Input.PropertyCode?.Trim();

                var request = new UserAccessRequest
                {
                    FirstName = Input.FirstName,
                    LastName = Input.LastName,
                    Email = Input.Email,
                    MobilePhone = Input.MobilePhone,
                    PropertyCode = normalizedPropertyCode ?? Input.PropertyCode,
                    PasswordHash = hashedPassword,
                    RequestedAt = DateTime.UtcNow,
                    IsApproved = false,
                    IsRejected = false
                };

                _dbContext.UserAccessRequests.Add(request);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Access request submitted for approval.");

                var (managerUsers, propertyFound) = await GetManagersForPropertyCodeAsync(normalizedPropertyCode);
                if (!propertyFound && !string.IsNullOrWhiteSpace(normalizedPropertyCode))
                {
                    _logger.LogWarning("No property found for code {PropertyCode}. Managers will not be notified for this request.", normalizedPropertyCode);
                }

                var adminUsers = await GetRoleMembersAsync("Admin");

                _logger.LogInformation("Routing access request for property {PropertyCode}: {AdminCount} admins, {ManagerCount} managers.", request.PropertyCode ?? string.Empty, adminUsers.Count, managerUsers.Count);

                var recipients = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);
                foreach (var admin in adminUsers)
                {
                    recipients.TryAdd(admin.Id, admin);
                }

                foreach (var manager in managerUsers)
                {
                    recipients.TryAdd(manager.Id, manager);
                }

                foreach (var recipient in recipients.Values)
                {
                    var approveUrl = Url.Action(nameof(AdminController.AccessRequests), "Admin", null, Request.Scheme);
                    if (string.IsNullOrEmpty(approveUrl))
                    {
                        _logger.LogWarning("Unable to generate approval URL for user {UserId}. Email not sent.", recipient.Id);
                        continue;
                    }

                    _logger.LogInformation("Dispatching access request email to {RecipientId} ({RecipientEmail}).", recipient.Id, recipient.Email ?? "null");

                    var encoder = HtmlEncoder.Default;
                    var managerDisplayName = encoder.Encode(string.IsNullOrWhiteSpace(recipient.UserName)
                        ? (recipient.Email ?? "Manager")
                        : recipient.UserName);
                    var requestFirstName = encoder.Encode(request.FirstName ?? string.Empty);
                    var requestLastName = encoder.Encode(request.LastName ?? string.Empty);
                    var requestEmail = encoder.Encode(request.Email ?? string.Empty);
                    var requestPropertyCode = encoder.Encode(request.PropertyCode ?? string.Empty);
                    var encodedApproveUrl = encoder.Encode(approveUrl);

                    var message = $@"
Hello {managerDisplayName},<br/><br/>
A new user has requested access:<br/>
Name: {requestFirstName} {requestLastName}<br/>
Email: {requestEmail}<br/>
Property Code: {requestPropertyCode}<br/><br/>
Please <a href='{encodedApproveUrl}'>review pending requests</a>.
";
                    await _emailSender.SendEmailAsync(recipient.Email, "New Access Request", message);
                }

                return RedirectToPage("./RegisterConfirmation", new { email = Input.Email });
            }

            return Page();
        }

        private void RefreshCaptchaSettings()
        {
            var hasKeys = !string.IsNullOrWhiteSpace(_captchaOptions.SiteKey)
                && !string.IsNullOrWhiteSpace(_captchaOptions.SecretKey);

            IsCaptchaEnabled = _captchaOptions.Enabled && hasKeys;
            CaptchaSiteKey = _captchaOptions.SiteKey ?? string.Empty;
        }

        private void PrepareCaptchaForDisplay()
        {
            if (IsCaptchaEnabled)
            {
                SimpleCaptchaQuestion = string.Empty;
                SimpleCaptchaToken = string.Empty;
                SimpleCaptchaAnswer = string.Empty;
                return;
            }

            var (question, token) = GenerateSimpleCaptchaChallenge();
            SimpleCaptchaQuestion = question;
            SimpleCaptchaToken = token;
            SimpleCaptchaAnswer = string.Empty;
        }

        private (string Question, string Token) GenerateSimpleCaptchaChallenge()
        {
            var firstOperand = RandomNumberGenerator.GetInt32(2, 10);
            var secondOperand = RandomNumberGenerator.GetInt32(2, 10);
            var answer = firstOperand + secondOperand;
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(SimpleCaptchaExpiryMinutes).ToUnixTimeSeconds();
            var protectedPayload = _captchaProtector.Protect($"{answer}|{expiresAt}");
            var question = $"What is {firstOperand} + {secondOperand}?";
            return (question, protectedPayload);
        }

        private bool ValidateSimpleCaptcha(string submittedAnswer, string submittedToken, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(submittedAnswer))
            {
                errorMessage = "Please answer the security question to continue.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(submittedToken))
            {
                errorMessage = "The security question expired. Please try again.";
                return false;
            }

            string payload;
            try
            {
                payload = _captchaProtector.Unprotect(submittedToken);
            }
            catch (CryptographicException)
            {
                errorMessage = "The security question expired. Please try again.";
                return false;
            }

            var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !long.TryParse(parts[1], out var expiresUnix))
            {
                errorMessage = "The security question expired. Please try again.";
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > expiresUnix)
            {
                errorMessage = "The security question expired. Please try again.";
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expectedAnswer))
            {
                errorMessage = "The security question expired. Please try again.";
                return false;
            }

            if (!int.TryParse(submittedAnswer.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var providedAnswer))
            {
                errorMessage = "Incorrect answer to the security question.";
                return false;
            }

            if (expectedAnswer != providedAnswer)
            {
                errorMessage = "Incorrect answer to the security question.";
                return false;
            }

            return true;
        }

        private static bool IsFormPostRequest(HttpRequest request)
        {
            if (request == null || !HttpMethods.IsPost(request.Method))
            {
                return false;
            }

            var contentType = request.ContentType ?? string.Empty;
            return contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase);
        }

        private string ExtractCaptchaToken()
        {
            if (HttpContext.Features.Get<IFormFeature>() is { Form: { } form })
            {
                var value = form["g-recaptcha-response"].ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            if (!IsFormPostRequest(Request))
            {
                return string.Empty;
            }

            try
            {
                return Request.Form["g-recaptcha-response"].ToString();
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
            catch (NullReferenceException)
            {
                return string.Empty;
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new InvalidOperationException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }

        private async Task<List<ApplicationUser>> GetRoleMembersAsync(string roleName)
        {
            var roleIds = await _dbContext.Roles
                .Where(r => r.Name == roleName)
                .Select(r => r.Id)
                .ToListAsync();

            if (roleIds.Count == 0)
            {
                return new List<ApplicationUser>();
            }

            return await (from ur in _dbContext.UserRoles
                          join u in _dbContext.Users on ur.UserId equals u.Id
                          where roleIds.Contains(ur.RoleId)
                          select u).ToListAsync();
        }

        private async Task<(List<ApplicationUser> Managers, bool PropertyFound)> GetManagersForPropertyCodeAsync(string propertyCode)
        {
            if (string.IsNullOrWhiteSpace(propertyCode))
            {
                return (new List<ApplicationUser>(), false);
            }

            var normalizedCode = propertyCode.Trim();
            var property = await _dbContext.Properties
                .AsNoTracking()
                .Where(p => p.Code == normalizedCode)
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync();

            if (property == null)
            {
                return (new List<ApplicationUser>(), false);
            }

            var managerRoleIds = await _dbContext.Roles
                .Where(r => r.Name == "Manager")
                .Select(r => r.Id)
                .ToListAsync();

            if (managerRoleIds.Count == 0)
            {
                return (new List<ApplicationUser>(), true);
            }

            var managers = await (from ur in _dbContext.UserRoles
                                  join u in _dbContext.Users on ur.UserId equals u.Id
                                  join access in _dbContext.UserPropertyAccesses on u.Id equals access.ApplicationUserId
                                  where managerRoleIds.Contains(ur.RoleId)
                                        && access.PropertyId == property.Id
                                  select u).ToListAsync();

            return (managers, true);
        }
    }
}

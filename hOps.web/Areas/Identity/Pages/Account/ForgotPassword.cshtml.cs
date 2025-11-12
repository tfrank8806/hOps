using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace hOps.web.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var normalizedEmail = Input.Email.Trim();
            var user = await _userManager.FindByEmailAsync(normalizedEmail);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                // Don't reveal that the user does not exist or doesn't have an email.
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encodedToken, email = user.Email },
                protocol: Request.Scheme);

            if (!string.IsNullOrEmpty(callbackUrl))
            {
                var bodyBuilder = new StringBuilder();
                bodyBuilder.AppendLine("<p>You recently requested to reset your password.</p>");
                bodyBuilder.AppendLine("<p>");
                var encodedUrl = WebUtility.HtmlEncode(callbackUrl);
                bodyBuilder.AppendLine($@"<a href=""{encodedUrl}"">Reset your password</a>");
                bodyBuilder.AppendLine("</p>");
                bodyBuilder.AppendLine("<p>If you did not make this request, you can safely ignore this email.</p>");

                try
                {
                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Reset your password",
                        bodyBuilder.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send password reset email to {UserId}", user.Id);
                }
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}

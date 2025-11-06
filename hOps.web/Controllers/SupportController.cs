using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class SupportController : BaseController
    {
        private readonly IExtendedEmailSender _emailSender;
        private readonly HtmlEncoder _htmlEncoder;
        private readonly ILogger<SupportController> _logger;

        public SupportController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IExtendedEmailSender emailSender,
            HtmlEncoder htmlEncoder,
            ILogger<SupportController> logger)
            : base(context, userManager)
        {
            _emailSender = emailSender;
            _htmlEncoder = htmlEncoder;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Support";
            ViewData["MainContainerClass"] = "main-inner--wide";

            var user = await _userManager.GetUserAsync(User);
            var model = CreateFormModel(user);

            if (TempData.ContainsKey(SuccessTempDataKey))
            {
                model.SubmittedSuccessfully = true;
                TempData.Remove(SuccessTempDataKey);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SupportTicketViewModel model)
        {
            ViewData["Title"] = "Support";
            ViewData["MainContainerClass"] = "main-inner--wide";

            model.CategoryOptions = SupportTicketViewModel.BuildCategoryOptions();
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser != null)
            {
                if (string.IsNullOrWhiteSpace(model.ContactEmail))
                {
                    model.ContactEmail = currentUser.Email;
                }

                if (string.IsNullOrWhiteSpace(model.ContactName))
                {
                    model.ContactName = BuildDisplayName(currentUser);
                }
            }

            var attachments = await ProcessAttachmentsAsync(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins == null || admins.Count == 0)
            {
                _logger.LogWarning("Support ticket submission failed because no administrators were found.");
                ModelState.AddModelError(string.Empty, "We could not find an administrator to receive your request. Please reach out to your system administrator directly.");
                return View(model);
            }

            var recipients = admins
                .Select(a => new { a.Email, Name = BuildDisplayName(a) })
                .Where(a => !string.IsNullOrWhiteSpace(a.Email))
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogWarning("Support ticket submission failed because administrator emails are not configured.");
                ModelState.AddModelError(string.Empty, "We could not find an administrator email address. Please reach out to your system administrator directly.");
                return View(model);
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            var body = BuildSupportEmailBody(model, currentUser, currentProperty, attachments);
            var emailSubject = $"[Support:{model.Category}] {model.Subject}".Trim();

            foreach (var recipient in recipients)
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient.Email!, emailSubject, body, attachments);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send support ticket email to {Recipient}", recipient.Email);
                }
            }

            _logger.LogInformation("Support ticket submitted by {UserId} ({ContactEmail}) with subject {Subject}",
                currentUser?.Id ?? "UnknownUser",
                model.ContactEmail ?? currentUser?.Email ?? "no-email",
                model.Subject);

            TempData[SuccessTempDataKey] = true;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Tips()
        {
            ViewData["Title"] = "Tips & Tricks";
            ViewData["MainContainerClass"] = "main-inner--wide";
            return View();
        }

        private SupportTicketViewModel CreateFormModel(ApplicationUser? user)
        {
            return new SupportTicketViewModel
            {
                CategoryOptions = SupportTicketViewModel.BuildCategoryOptions(),
                ContactEmail = user?.Email,
                ContactName = BuildDisplayName(user)
            };
        }

        private static string? BuildDisplayName(ApplicationUser? user)
        {
            if (user == null)
            {
                return null;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName))
            {
                parts.Add(user.FirstName.Trim());
            }
            if (!string.IsNullOrWhiteSpace(user.LastName))
            {
                parts.Add(user.LastName.Trim());
            }

            var name = string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(name) ? user.Email ?? user.UserName : name;
        }

        private string BuildSupportEmailBody(SupportTicketViewModel model, ApplicationUser? user, Property? property, IReadOnlyCollection<EmailAttachment> attachments)
        {
            var builder = new StringBuilder();
            builder.Append("<p>A new support request has been submitted from hOps.</p>");
            builder.Append("<table style=\"border-collapse:collapse; width:100%; max-width:640px;\">");
            AppendRow(builder, "Submitted by", _htmlEncoder.Encode(model.ContactName ?? BuildDisplayName(user) ?? "Unknown user"));
            AppendRow(builder, "Contact email", _htmlEncoder.Encode(model.ContactEmail ?? user?.Email ?? "Not provided"));
            AppendRow(builder, "Category", _htmlEncoder.Encode(model.Category));
            AppendRow(builder, "Subject", _htmlEncoder.Encode(model.Subject));
            if (property != null)
            {
                AppendRow(builder, "Current property", _htmlEncoder.Encode($"{property.Name} ({property.Code})"));
            }
            if (attachments.Count > 0)
            {
                var attachmentList = string.Join("<br />", attachments.Select(a => _htmlEncoder.Encode(a.FileName)));
                AppendRow(builder, "Attachments", attachmentList);
            }
            else
            {
                AppendRow(builder, "Attachments", "None");
            }
            builder.Append("</table>");
            builder.Append("<hr style=\"margin:1.5rem 0;\" />");
            builder.Append("<p style=\"margin-bottom:0.5rem;\"><strong>Message</strong></p>");
            var encodedMessage = _htmlEncoder.Encode(model.Message ?? string.Empty)
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal)
                .Replace("\r", "<br />", StringComparison.Ordinal);
            builder.Append("<div style=\"white-space:pre-wrap;\">").Append(encodedMessage).Append("</div>");

            return builder.ToString();
        }

        private void AppendRow(StringBuilder builder, string label, string value)
        {
            builder.Append("<tr>")
                .Append("<td style=\"padding:4px 8px; font-weight:600; width:160px; vertical-align:top;\">")
                .Append(_htmlEncoder.Encode(label))
                .Append("</td>")
                .Append("<td style=\"padding:4px 8px;\">")
                .Append(value)
                .Append("</td>")
                .Append("</tr>");
        }

        private const string SuccessTempDataKey = "SupportTicketSubmitted";
        private const long MaxAttachmentSizeBytes = 5 * 1024 * 1024; // 5 MB
        private const int MaxAttachmentCount = 5;
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/jpg",
            "image/heic",
            "image/heif",
            "image/gif",
            "image/webp",
            "image/bmp",
            "application/pdf"
        };

        private async Task<IReadOnlyCollection<EmailAttachment>> ProcessAttachmentsAsync(SupportTicketViewModel model)
        {
            var attachments = new List<EmailAttachment>();
            if (model.Attachments == null || model.Attachments.Count == 0)
            {
                return attachments;
            }

            if (model.Attachments.Count > MaxAttachmentCount)
            {
                ModelState.AddModelError(nameof(model.Attachments), $"Please upload up to {MaxAttachmentCount} files per ticket.");
                return attachments;
            }

            foreach (var file in model.Attachments)
            {
                if (file == null)
                {
                    continue;
                }

                if (file.Length == 0)
                {
                    continue;
                }

                if (file.Length > MaxAttachmentSizeBytes)
                {
                    ModelState.AddModelError(nameof(model.Attachments), $"'{file.FileName}' exceeds the {MaxAttachmentSizeBytes / (1024 * 1024)} MB limit.");
                    continue;
                }

                var contentType = file.ContentType;
                if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
                {
                    ModelState.AddModelError(nameof(model.Attachments), $"'{file.FileName}' is not an allowed file type. Please upload images (PNG, JPG, GIF, WEBP, BMP) or PDF files.");
                    continue;
                }

                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);
                    attachments.Add(new EmailAttachment(Path.GetFileName(file.FileName), memoryStream.ToArray(), contentType));
            }

            return attachments;
        }
    }
}

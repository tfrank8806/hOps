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
using Microsoft.Net.Http.Headers;

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
                (model.ContactEmail ?? currentUser?.Email ?? "no-email")
                    ?.Replace(Environment.NewLine, "")
                    ?.Replace("\n", "")
                    ?.Replace("\r", ""),
                model.Subject?.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", ""));

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportIssue([FromBody] IssueReportRequest request)
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Please provide details about the issue.";
                return BadRequest(new { message = firstError });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var trimmedDetails = request.Details?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedDetails))
            {
                return BadRequest(new { message = "Please describe the issue you encountered." });
            }

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins == null || admins.Count == 0)
            {
                _logger.LogWarning("Issue report could not be delivered because no administrators are configured.");
                return StatusCode(500, new { message = "Unable to deliver your report. Administrators are not configured." });
            }

            var recipients = admins
                .Select(a => new { a.Email, Name = BuildDisplayName(a) })
                .Where(a => !string.IsNullOrWhiteSpace(a.Email))
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogWarning("Issue report could not be delivered because administrator emails are missing.");
                return StatusCode(500, new { message = "Unable to deliver your report. Administrator emails are not available." });
            }

            var attachments = new List<EmailAttachment>();
            var screenshotWarning = default(string?);
            if (TryCreateScreenshotAttachment(request.ScreenshotDataUrl, out var screenshotAttachment, out var warning))
            {
                if (screenshotAttachment != null)
                {
                    attachments.Add(screenshotAttachment);
                }
            }
            screenshotWarning = warning;

            var property = ViewBag.CurrentProperty as Property;
            var pageUrl = string.IsNullOrWhiteSpace(request.PageUrl)
                ? $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}"
                : request.PageUrl.Trim();

            var body = BuildIssueEmailBody(trimmedDetails!, user, property, pageUrl, attachments.Count > 0, screenshotWarning);
            var subject = $"[Issue Report] {BuildDisplayName(user) ?? user.Email ?? user.UserName ?? "User"} @ {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

            foreach (var recipient in recipients)
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient.Email!, subject, body, attachments);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send issue report email to {Recipient}", recipient.Email);
                }
            }

            _logger.LogInformation("Issue report submitted by {UserId} for {Page}", user.Id, pageUrl);

            return Ok(new { message = "Thanks for letting us know. Our team has been notified." });
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

        private string BuildIssueEmailBody(string details, ApplicationUser user, Property? property, string? pageUrl, bool hasScreenshot, string? screenshotWarning)
        {
            var builder = new StringBuilder();
            builder.Append("<p>An in-app issue report was submitted.</p>");
            builder.Append("<table style=\"border-collapse:collapse; width:100%; max-width:640px;\">");
            AppendRow(builder, "Reported by", _htmlEncoder.Encode(BuildDisplayName(user) ?? user.Email ?? "App User"));
            AppendRow(builder, "Email", _htmlEncoder.Encode(user.Email ?? "Not provided"));
            if (property != null)
            {
                AppendRow(builder, "Current property", _htmlEncoder.Encode($"{property.Name} ({property.Code})"));
            }
            AppendRow(builder, "Page URL", _htmlEncoder.Encode(string.IsNullOrWhiteSpace(pageUrl) ? "Unknown" : pageUrl));
            var userAgent = Request.Headers[HeaderNames.UserAgent].ToString();
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                AppendRow(builder, "Browser", _htmlEncoder.Encode(userAgent));
            }
            AppendRow(builder, "Screenshot", hasScreenshot ? "Included as attachment" : "Not captured");
            if (!string.IsNullOrWhiteSpace(screenshotWarning))
            {
                AppendRow(builder, "Screenshot status", _htmlEncoder.Encode(screenshotWarning));
            }
            builder.Append("</table>");
            builder.Append("<hr style=\"margin:1.5rem 0;\" />");
            builder.Append("<p style=\"margin-bottom:0.5rem;\"><strong>Details</strong></p>");
            var encodedDetails = _htmlEncoder.Encode(details)
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal)
                .Replace("\r", "<br />", StringComparison.Ordinal);
            builder.Append("<div style=\"white-space:pre-wrap;\">").Append(encodedDetails).Append("</div>");

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

        private const int ScreenshotAttachmentMaxBytes = 3 * 1024 * 1024;

        private bool TryCreateScreenshotAttachment(string? dataUrl, out EmailAttachment? attachment, out string? warning)
        {
            attachment = null;
            warning = null;

            if (string.IsNullOrWhiteSpace(dataUrl))
            {
                return false;
            }

            var commaIndex = dataUrl.IndexOf(',', StringComparison.Ordinal);
            if (commaIndex < 0 || commaIndex >= dataUrl.Length - 1)
            {
                warning = "Screenshot data was invalid.";
                return false;
            }

            var metadata = dataUrl[..commaIndex];
            var base64 = dataUrl[(commaIndex + 1)..];

            string contentType = "image/png";
            if (metadata.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var semicolonIndex = metadata.IndexOf(';');
                if (semicolonIndex > 5)
                {
                    contentType = metadata.Substring(5, semicolonIndex - 5);
                }
            }

            try
            {
                var bytes = Convert.FromBase64String(base64);
                if (bytes.Length == 0)
                {
                    return false;
                }

                if (bytes.Length > ScreenshotAttachmentMaxBytes)
                {
                    warning = $"Screenshot exceeded the {ScreenshotAttachmentMaxBytes / (1024 * 1024)} MB limit and was skipped.";
                    return false;
                }

                var extension = contentType switch
                {
                    "image/jpeg" => "jpg",
                    "image/jpg" => "jpg",
                    "image/gif" => "gif",
                    "image/webp" => "webp",
                    "image/bmp" => "bmp",
                    _ => "png"
                };

                attachment = new EmailAttachment($"issue-screenshot-{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}", bytes, contentType);
                return true;
            }
            catch (FormatException)
            {
                warning = "Screenshot data could not be decoded.";
                return false;
            }
        }

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

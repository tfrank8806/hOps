using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.SiteVisit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class SiteVisitController : BaseController
    {
        private readonly IExtendedEmailSender _emailSender;
        private readonly HtmlEncoder _htmlEncoder;
        private readonly ILogger<SiteVisitController> _logger;

        private static readonly string[] DefaultChecklistItems =
        {
            "Arrival experience & curb appeal",
            "Lobby and public space presentation",
            "Front desk engagement & service",
            "Guest room readiness and housekeeping",
            "Back-of-house cleanliness & organization",
            "Maintenance and safety equipment checks",
            "Brand standards / marketing compliance",
            "Team engagement & training conversations"
        };

        private const string SuccessTempDataKey = "SiteVisitSuccessMessage";

        public SiteVisitController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IExtendedEmailSender emailSender,
            HtmlEncoder htmlEncoder,
            ILogger<SiteVisitController> logger)
            : base(context, userManager)
        {
            _emailSender = emailSender;
            _htmlEncoder = htmlEncoder;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "Site Visit";
            ViewData["MainContainerClass"] = "main-inner--wide";

            var model = BuildInitialModel();

            if (TempData.TryGetValue(SuccessTempDataKey, out var message) && message is string successMessage)
            {
                model.SubmittedSuccessfully = true;
                model.SuccessMessage = successMessage;
                TempData.Remove(SuccessTempDataKey);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(SiteVisitPageViewModel model)
        {
            ViewData["Title"] = "Site Visit";
            ViewData["MainContainerClass"] = "main-inner--wide";

            NormalizeModel(model);

            ModelState.Clear();
            var isValid = TryValidateModel(model);

            var recipients = ParseRecipients(model);
            if (recipients.Count == 0)
            {
                ModelState.AddModelError(nameof(model.RecipientEmails), "Enter at least one valid email address.");
                isValid = false;
            }

            if (model.Items.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Items), "Please add at least one checklist item.");
                isValid = false;
            }

            if (!isValid)
            {
                EnsurePlaceholderRow(model);
                return View("Index", model);
            }

            var attachment = CreateWorkbookAttachment(model);
            var attachmentList = new[] { attachment };
            var subject = BuildEmailSubject(model);
            var body = BuildEmailBody(model);

            var successCount = 0;
            foreach (var recipient in recipients)
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient, subject, body, attachmentList);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send site visit checklist to {Recipient}", recipient);
                }
            }

            if (successCount == 0)
            {
                ModelState.AddModelError(string.Empty, "We could not send the site visit email. Please verify the addresses and try again.");
                EnsurePlaceholderRow(model);
                return View("Index", model);
            }

            var successText = $"Site visit checklist emailed to {successCount} recipient{(successCount == 1 ? string.Empty : "s")}.";
            TempData[SuccessTempDataKey] = successText;

            _logger.LogInformation("Site visit checklist emailed to {RecipientCount} recipient(s) for property {Property}", successCount, model.PropertyName ?? "(unspecified)");

            return RedirectToAction(nameof(Index));
        }

        private SiteVisitPageViewModel BuildInitialModel()
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            var model = new SiteVisitPageViewModel
            {
                PropertyName = currentProperty?.Name,
                VisitDate = DateTime.Today,
                Items = BuildDefaultItems()
            };

            EnsurePlaceholderRow(model);
            return model;
        }

        private static List<SiteVisitChecklistItemViewModel> BuildDefaultItems()
        {
            return DefaultChecklistItems
                .Select(title => new SiteVisitChecklistItemViewModel
                {
                    Title = title,
                    Status = SiteVisitChecklistStatus.Compliant
                })
                .ToList();
        }

        private static void EnsurePlaceholderRow(SiteVisitPageViewModel model)
        {
            if (model.Items.Count == 0)
            {
                model.Items.Add(new SiteVisitChecklistItemViewModel());
            }
        }

        private void NormalizeModel(SiteVisitPageViewModel model)
        {
            model.PropertyName = model.PropertyName?.Trim();
            if (string.IsNullOrWhiteSpace(model.PropertyName))
            {
                var currentProperty = ViewBag.CurrentProperty as Property;
                if (currentProperty != null)
                {
                    model.PropertyName = currentProperty.Name;
                }
            }
            model.LeaderName = model.LeaderName?.Trim();
            model.SummaryNotes = string.IsNullOrWhiteSpace(model.SummaryNotes) ? null : model.SummaryNotes.Trim();
            model.RecipientEmails = model.RecipientEmails?.Trim() ?? string.Empty;

            if (model.VisitDate == DateTime.MinValue)
            {
                model.VisitDate = DateTime.Today;
            }

            model.Items = (model.Items ?? new List<SiteVisitChecklistItemViewModel>())
                .Select(item => new SiteVisitChecklistItemViewModel
                {
                    Title = (item.Title ?? string.Empty).Trim(),
                    Status = item.Status,
                    Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Title) || !string.IsNullOrWhiteSpace(item.Notes))
                .ToList();
        }

        private IReadOnlyList<string> ParseRecipients(SiteVisitPageViewModel model)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(model.RecipientEmails))
            {
                return recipients.ToList();
            }

            var validator = new EmailAddressAttribute();
            var tokens = model.RecipientEmails
                .Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (!validator.IsValid(trimmed))
                {
                    ModelState.AddModelError(nameof(model.RecipientEmails), $"'{trimmed}' is not a valid email address.");
                    continue;
                }

                recipients.Add(trimmed);
            }

            return recipients.ToList();
        }

        private EmailAttachment CreateWorkbookAttachment(SiteVisitPageViewModel model)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Site Visit");

            worksheet.Cell(1, 1).Value = "Site Visit Checklist";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Range(1, 1, 1, 3).Merge();

            worksheet.Cell(2, 1).Value = "Property";
            worksheet.Cell(2, 2).Value = model.PropertyName ?? "Not specified";
            worksheet.Cell(3, 1).Value = "Visit date";
            worksheet.Cell(3, 2).Value = model.VisitDate.ToString("D");
            worksheet.Cell(4, 1).Value = "Visit lead";
            worksheet.Cell(4, 2).Value = string.IsNullOrWhiteSpace(model.LeaderName) ? "Not specified" : model.LeaderName;
            worksheet.Cell(5, 1).Value = "Summary notes";
            worksheet.Cell(5, 2).Value = model.SummaryNotes ?? "—";
            worksheet.Range(5, 2, 5, 3).Merge();

            var headerRow = 7;
            worksheet.Cell(headerRow, 1).Value = "Checklist Item";
            worksheet.Cell(headerRow, 2).Value = "Status";
            worksheet.Cell(headerRow, 3).Value = "Notes";
            worksheet.Range(headerRow, 1, headerRow, 3).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#e9ecef"));

            var row = headerRow + 1;
            foreach (var item in model.Items)
            {
                worksheet.Cell(row, 1).Value = item.Title;
                worksheet.Cell(row, 2).Value = StatusLabel(item.Status);
                worksheet.Cell(row, 3).Value = item.Notes ?? string.Empty;

                var statusColor = item.Status switch
                {
                    SiteVisitChecklistStatus.Compliant => XLColor.FromHtml("#d1e7dd"),
                    SiteVisitChecklistStatus.NeedsReview => XLColor.FromHtml("#fff3cd"),
                    SiteVisitChecklistStatus.NotCompliant => XLColor.FromHtml("#f8d7da"),
                    _ => XLColor.White
                };

                worksheet.Cell(row, 2).Style.Fill.SetBackgroundColor(statusColor);
                worksheet.Cell(row, 3).Style.Alignment.WrapText = true;
                row++;
            }

            worksheet.Columns(1, 3).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"SiteVisit-{BuildSafeFileSegment(model.PropertyName)}-{model.VisitDate:yyyyMMdd}.xlsx";
            return new EmailAttachment(fileName, stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        private static string StatusLabel(SiteVisitChecklistStatus status)
        {
            return status switch
            {
                SiteVisitChecklistStatus.Compliant => "Compliant",
                SiteVisitChecklistStatus.NeedsReview => "Needs Review",
                SiteVisitChecklistStatus.NotCompliant => "Not Compliant",
                _ => status.ToString()
            };
        }

        private string BuildEmailSubject(SiteVisitPageViewModel model)
        {
            var property = string.IsNullOrWhiteSpace(model.PropertyName) ? "Property" : model.PropertyName;
            return $"Site Visit Checklist - {property} ({model.VisitDate:MMM d, yyyy})";
        }

        private string BuildEmailBody(SiteVisitPageViewModel model)
        {
            var builder = new StringBuilder();
            var property = Encode(model.PropertyName ?? "Not specified");
            var leader = Encode(string.IsNullOrWhiteSpace(model.LeaderName) ? "Not specified" : model.LeaderName!);
            var summary = EncodeWithBreaks(model.SummaryNotes);

            builder.Append("<p>");
            builder.Append($"Site visit summary for <strong>{property}</strong> on <strong>{model.VisitDate:D}</strong>.");
            builder.Append("</p>");
            builder.Append("<ul>");
            builder.Append($"<li><strong>Visit lead:</strong> {leader}</li>");
            if (!string.IsNullOrWhiteSpace(summary))
            {
                builder.Append($"<li><strong>Summary:</strong> {summary}</li>");
            }
            builder.Append("</ul>");

            builder.Append("<table style=\"border-collapse:collapse;width:100%;max-width:800px;\">");
            builder.Append("<thead><tr>");
            builder.Append("<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;\">Item</th>");
            builder.Append("<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;width:150px;\">Status</th>");
            builder.Append("<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;\">Notes</th>");
            builder.Append("</tr></thead><tbody>");

            foreach (var item in model.Items)
            {
                var notes = EncodeWithBreaks(item.Notes);
                builder.Append("<tr>");
                builder.Append($"<td style=\"border-bottom:1px solid #f1f3f5;padding:8px;\">{Encode(item.Title)}</td>");
                builder.Append($"<td style=\"border-bottom:1px solid #f1f3f5;padding:8px;\">{StatusLabel(item.Status)}</td>");
                builder.Append($"<td style=\"border-bottom:1px solid #f1f3f5;padding:8px;\">{(string.IsNullOrWhiteSpace(notes) ? "&nbsp;" : notes)}</td>");
                builder.Append("</tr>");
            }

            builder.Append("</tbody></table>");
            builder.Append("<p>The full checklist is attached as an Excel file.</p>");

            return builder.ToString();
        }

        private string Encode(string? value)
        {
            return _htmlEncoder.Encode(value ?? string.Empty);
        }

        private string EncodeWithBreaks(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var lines = value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var encoded = lines.Select(line => _htmlEncoder.Encode(line));
            return string.Join("<br />", encoded);
        }

        private static string BuildSafeFileSegment(string? value)
        {
            var input = string.IsNullOrWhiteSpace(value) ? "Property" : value.Trim();
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(input.Length);

            foreach (var character in input)
            {
                builder.Append(invalidChars.Contains(character) ? '-' : character);
            }

            var cleaned = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(cleaned) ? "Property" : cleaned;
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Models.SiteVisit;
using hOps.web.ViewModels.SiteVisit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
        private const string LogSuccessTempDataKey = "SiteVisitLogMessage";
        private const string TemplateSuccessTempDataKey = "SiteVisitTemplateSuccessMessage";
        private const int TemplateRowLimit = 200;
        private const int TemplateFileSizeLimitBytes = 2 * 1024 * 1024;

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
        public async Task<IActionResult> Index(int? templateId = null)
        {
            ViewData["Title"] = "Site Visit";
            ViewData["MainContainerClass"] = "main-inner--wide";

            var templates = await LoadTemplatesAsync();
            var currentProperty = ViewBag.CurrentProperty as Property;
            var templateRequested = Request.Query.ContainsKey("templateId");
            var model = BuildInitialModel(currentProperty, templates, templateId, !templateRequested);

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

            var recipients = ParseRecipients(model.RecipientEmails, nameof(model.RecipientEmails), requireAtLeastOne: true);
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
                await PopulateTemplateOptionsAsync(model);
                EnsurePlaceholderRow(model);
                return View("Index", model);
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            var currentUser = await _userManager.GetUserAsync(User);
            var report = BuildReportEntity(model, recipients, currentProperty, currentUser);

            var attachment = CreateWorkbookAttachment(report);
            var attachmentList = new[] { attachment };
            var subject = BuildEmailSubject(report);
            var body = BuildEmailBody(report);

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
                await PopulateTemplateOptionsAsync(model);
                EnsurePlaceholderRow(model);
                return View("Index", model);
            }

            try
            {
                await _context.SiteVisitReports.AddAsync(report);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist site visit report for {Property}", report.PropertyName);
                ModelState.AddModelError(string.Empty, "Your email was sent but we could not save the visit to the log.");
                await PopulateTemplateOptionsAsync(model);
                EnsurePlaceholderRow(model);
                return View("Index", model);
            }

            var successText = $"Site visit checklist emailed to {successCount} recipient{(successCount == 1 ? string.Empty : "s")}.";
            TempData[SuccessTempDataKey] = successText;

            _logger.LogInformation("Site visit checklist emailed to {RecipientCount} recipient(s) for property {Property}", successCount, model.PropertyName ?? "(unspecified)");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Templates()
        {
            ViewData["Title"] = "Site Visit Templates";
            ViewData["MainContainerClass"] = "main-inner--wide";

            var viewModel = await BuildTemplateManagerViewModelAsync();
            if (TempData.TryGetValue(TemplateSuccessTempDataKey, out var message) && message is string successMessage)
            {
                viewModel.SuccessMessage = successMessage;
                TempData.Remove(TemplateSuccessTempDataKey);
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadTemplate(SiteVisitTemplateUploadViewModel form)
        {
            ViewData["Title"] = "Site Visit Templates";
            ViewData["MainContainerClass"] = "main-inner--wide";

            var rowTitles = new List<string>();
            if (form.CsvFile == null || form.CsvFile.Length == 0)
            {
                ModelState.AddModelError(nameof(form.CsvFile), "Select a CSV file to upload.");
            }
            else if (form.CsvFile.Length > TemplateFileSizeLimitBytes)
            {
                ModelState.AddModelError(nameof(form.CsvFile), "The CSV file is too large. Limit uploads to 2 MB.");
            }

            if (ModelState.IsValid && form.CsvFile != null)
            {
                try
                {
                    rowTitles = await ParseTemplateCsvAsync(form.CsvFile);
                    if (rowTitles.Count == 0)
                    {
                        ModelState.AddModelError(nameof(form.CsvFile), "The file did not contain any checklist rows.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(form.CsvFile), ex.Message);
                }
            }

            if (!ModelState.IsValid)
            {
                var invalidViewModel = await BuildTemplateManagerViewModelAsync();
                invalidViewModel.Upload = form;
                return View("Templates", invalidViewModel);
            }

            var user = await _userManager.GetUserAsync(User);
            var template = new SiteVisitTemplate
            {
                Name = form.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
                CreatedByUserId = user?.Id,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            for (var index = 0; index < rowTitles.Count; index++)
            {
                template.Items.Add(new SiteVisitTemplateItem
                {
                    Title = rowTitles[index],
                    SortOrder = index
                });
            }

            await _context.SiteVisitTemplates.AddAsync(template);
            await _context.SaveChangesAsync();

            var suffix = rowTitles.Count == 1 ? "item" : "items";
            TempData[TemplateSuccessTempDataKey] = $"Template '{template.Name}' created with {rowTitles.Count} {suffix}.";

            return RedirectToAction(nameof(Templates));
        }

        [HttpGet]
        public IActionResult DownloadBlankTemplate()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Title");
            foreach (var entry in DefaultChecklistItems)
            {
                builder.AppendLine($"\"{entry.Replace("\"", "\"\"", StringComparison.Ordinal)}\"");
            }

            var payload = Encoding.UTF8.GetBytes(builder.ToString());
            return File(payload, "text/csv", "site-visit-template.csv");
        }

        [HttpGet]
        public async Task<IActionResult> Log()
        {
            ViewData["Title"] = "Site Visit Log";
            ViewData["MainContainerClass"] = "main-inner--wide";

            if (TempData.TryGetValue(LogSuccessTempDataKey, out var message) && message is string alert)
            {
                ViewBag.LogSuccessMessage = alert;
                TempData.Remove(LogSuccessTempDataKey);
            }

            var viewModel = await BuildLogViewModelAsync();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLogEntry(SiteVisitLogUpdateViewModel model)
        {
            ViewData["Title"] = "Site Visit Log";
            ViewData["MainContainerClass"] = "main-inner--wide";

            var report = await _context.SiteVisitReports
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (report == null)
            {
                ModelState.AddModelError(string.Empty, "We could not find that site visit.");
                ViewBag.ActiveLogEntryId = model.Id;
                var missingViewModel = await BuildLogViewModelAsync(model.Id, model);
                return View("Log", missingViewModel);
            }

            var requireRecipients = string.Equals(model.SubmitAction, "email", StringComparison.OrdinalIgnoreCase);
            var recipients = ParseRecipients(model.RecipientEmails, nameof(model.RecipientEmails), requireRecipients);

            if (requireRecipients && recipients.Count == 0)
            {
                ModelState.AddModelError(nameof(model.RecipientEmails), "Enter at least one recipient before emailing an update.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ActiveLogEntryId = model.Id;
                var invalidViewModel = await BuildLogViewModelAsync(model.Id, model);
                return View("Log", invalidViewModel);
            }

            report.AssignedTo = string.IsNullOrWhiteSpace(model.AssignedTo) ? null : model.AssignedTo.Trim();
            report.ProgressStatus = model.ProgressStatus;
            report.CompletionNotes = string.IsNullOrWhiteSpace(model.CompletionNotes) ? null : model.CompletionNotes.Trim();
            report.RecipientEmails = recipients.Count > 0 ? string.Join(", ", recipients) : null;
            report.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (requireRecipients && recipients.Count > 0)
            {
                var attachment = CreateWorkbookAttachment(report);
                var attachmentList = new[] { attachment };
                var subject = BuildEmailSubject(report);
                var body = BuildEmailBody(report);
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
                        _logger.LogError(ex, "Failed to send updated site visit to {Recipient}", recipient);
                    }
                }

                if (successCount == 0)
                {
                    ModelState.AddModelError(string.Empty, "We could not send the updated site visit. Please try again.");
                    ViewBag.ActiveLogEntryId = model.Id;
                    var failureViewModel = await BuildLogViewModelAsync(model.Id, model);
                    return View("Log", failureViewModel);
                }

                TempData[LogSuccessTempDataKey] = $"Update emailed to {successCount} recipient{(successCount == 1 ? string.Empty : "s")}.";
            }
            else
            {
                TempData[LogSuccessTempDataKey] = "Site visit progress saved.";
            }

            return RedirectToAction(nameof(Log));
        }

        private SiteVisitPageViewModel BuildInitialModel(
            Property? currentProperty,
            IReadOnlyList<SiteVisitTemplate> templates,
            int? requestedTemplateId,
            bool fallbackToFirstTemplate)
        {
            SiteVisitTemplate? templateToApply = null;

            if (requestedTemplateId.HasValue)
            {
                templateToApply = templates.FirstOrDefault(t => t.Id == requestedTemplateId.Value);
            }
            else if (fallbackToFirstTemplate && templates.Count > 0)
            {
                templateToApply = templates[0];
            }

            var model = new SiteVisitPageViewModel
            {
                PropertyName = currentProperty?.Name,
                VisitDate = DateTime.Today,
                Items = templateToApply != null ? BuildItemsFromTemplate(templateToApply) : BuildDefaultItems(),
                SelectedTemplateId = templateToApply?.Id
            };

            if (templates.Count > 0)
            {
                model.TemplateOptions = BuildTemplateSelectList(templates, model.SelectedTemplateId);
            }

            EnsurePlaceholderRow(model);
            return model;
        }

        private async Task<List<SiteVisitTemplate>> LoadTemplatesAsync()
        {
            var templates = await _context.SiteVisitTemplates
                .Include(t => t.Items)
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync();

            foreach (var template in templates)
            {
                template.Items = template.Items
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .ToList();
            }

            return templates;
        }

        private async Task PopulateTemplateOptionsAsync(SiteVisitPageViewModel model)
        {
            var templates = await LoadTemplatesAsync();
            if (templates.Count > 0)
            {
                model.TemplateOptions = BuildTemplateSelectList(templates, model.SelectedTemplateId);
            }
            else
            {
                model.TemplateOptions = new List<SelectListItem>();
            }
        }

        private static List<SiteVisitChecklistItemViewModel> BuildItemsFromTemplate(SiteVisitTemplate template)
        {
            return template.Items
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .Select(item => new SiteVisitChecklistItemViewModel
                {
                    Title = item.Title,
                    Status = SiteVisitChecklistStatus.NotReviewed
                })
                .ToList();
        }

        private static List<SelectListItem> BuildTemplateSelectList(IEnumerable<SiteVisitTemplate> templates, int? selectedTemplateId)
        {
            var options = new List<SelectListItem>
            {
                new()
                {
                    Value = string.Empty,
                    Text = "Custom checklist (start from scratch)",
                    Selected = !selectedTemplateId.HasValue
                }
            };

            options.AddRange(templates.Select(template => new SelectListItem
            {
                Value = template.Id.ToString(CultureInfo.InvariantCulture),
                Text = $"{template.Name} ({template.Items.Count} {(template.Items.Count == 1 ? "item" : "items")})",
                Selected = selectedTemplateId.HasValue && selectedTemplateId.Value == template.Id
            }));

            return options;
        }

        private async Task<SiteVisitTemplateManagerViewModel> BuildTemplateManagerViewModelAsync()
        {
            var templates = await _context.SiteVisitTemplates
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Description,
                    t.UpdatedAtUtc,
                    ItemCount = t.Items.Count,
                    CreatorFirstName = t.CreatedByUser != null ? t.CreatedByUser.FirstName : null,
                    CreatorLastName = t.CreatedByUser != null ? t.CreatedByUser.LastName : null,
                    CreatorEmail = t.CreatedByUser != null ? t.CreatedByUser.Email : null,
                    CreatorUserName = t.CreatedByUser != null ? t.CreatedByUser.UserName : null
                })
                .ToListAsync();

            var summaries = templates
                .Select(t =>
                {
                    string? createdBy = null;
                    var nameParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(t.CreatorFirstName))
                    {
                        nameParts.Add(t.CreatorFirstName.Trim());
                    }

                    if (!string.IsNullOrWhiteSpace(t.CreatorLastName))
                    {
                        nameParts.Add(t.CreatorLastName.Trim());
                    }

                    if (nameParts.Count > 0)
                    {
                        createdBy = string.Join(' ', nameParts);
                    }
                    else if (!string.IsNullOrWhiteSpace(t.CreatorEmail))
                    {
                        createdBy = t.CreatorEmail;
                    }
                    else if (!string.IsNullOrWhiteSpace(t.CreatorUserName))
                    {
                        createdBy = t.CreatorUserName;
                    }

                    return new SiteVisitTemplateSummaryViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        ItemCount = t.ItemCount,
                        UpdatedAtUtc = t.UpdatedAtUtc,
                        CreatedByName = createdBy
                    };
                })
                .ToList();

            return new SiteVisitTemplateManagerViewModel
            {
                Templates = summaries,
                Upload = new SiteVisitTemplateUploadViewModel()
            };
        }

        private async Task<List<string>> ParseTemplateCsvAsync(IFormFile file)
        {
            var titles = new List<string>();

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            var lineNumber = 0;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                if (titles.Count >= TemplateRowLimit)
                {
                    throw new InvalidOperationException($"Templates can include up to {TemplateRowLimit} checklist rows.");
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var cells = SplitCsvLine(line);
                if (lineNumber == 1 && cells.Count > 0 && cells[0].Trim().Equals("title", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var title = cells.Count > 0 ? cells[0].Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                if (title.Length > 200)
                {
                    title = title.Substring(0, 200).Trim();
                }

                titles.Add(title);
            }

            return titles;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            if (line == null)
            {
                return values;
            }

            var builder = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var character = line[i];
                if (character == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (character == ',' && !inQuotes)
                {
                    values.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                builder.Append(character);
            }

            values.Add(builder.ToString());
            return values;
        }

        private static List<SiteVisitChecklistItemViewModel> BuildDefaultItems()
        {
            return DefaultChecklistItems
                .Select(title => new SiteVisitChecklistItemViewModel
                {
                    Title = title,
                    Status = SiteVisitChecklistStatus.NotReviewed
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

        private SiteVisitReport BuildReportEntity(
            SiteVisitPageViewModel model,
            IReadOnlyList<string> recipients,
            Property? property,
            ApplicationUser? createdBy)
        {
            var now = DateTime.UtcNow;

            var report = new SiteVisitReport
            {
                PropertyId = property?.Id,
                PropertyName = model.PropertyName ?? property?.Name ?? "Property",
                VisitDate = model.VisitDate,
                LeaderName = string.IsNullOrWhiteSpace(model.LeaderName) ? null : model.LeaderName.Trim(),
                SummaryNotes = string.IsNullOrWhiteSpace(model.SummaryNotes) ? null : model.SummaryNotes,
                RecipientEmails = recipients.Count > 0 ? string.Join(", ", recipients) : null,
                AssignedTo = null,
                ProgressStatus = SiteVisitProgressStatus.NotStarted,
                CompletionNotes = null,
                SiteVisitTemplateId = model.SelectedTemplateId,
                CreatedByUserId = createdBy?.Id,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            foreach (var item in model.Items)
            {
                report.Items.Add(new SiteVisitReportItem
                {
                    Title = item.Title,
                    Status = item.Status,
                    Notes = item.Notes
                });
            }

            return report;
        }

        private SiteVisitLogEntryViewModel MapLogEntry(SiteVisitReport report)
        {
            return new SiteVisitLogEntryViewModel
            {
                Id = report.Id,
                PropertyName = report.PropertyName,
                VisitDate = report.VisitDate,
                LeaderName = report.LeaderName,
                SummaryNotes = report.SummaryNotes,
                AssignedTo = report.AssignedTo,
                ProgressStatus = report.ProgressStatus,
                CompletionNotes = report.CompletionNotes,
                RecipientEmails = report.RecipientEmails,
                CreatedByDisplayName = BuildDisplayName(report.CreatedByUser),
                CreatedAtUtc = report.CreatedAtUtc,
                UpdatedAtUtc = report.UpdatedAtUtc,
                Items = report.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new SiteVisitChecklistItemViewModel
                    {
                        Title = i.Title,
                        Status = i.Status,
                        Notes = i.Notes
                    })
                    .ToList()
            };
        }

        private async Task<SiteVisitLogViewModel> BuildLogViewModelAsync(int? activeEntryId = null, SiteVisitLogUpdateViewModel? pendingUpdate = null)
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            var propertyId = currentProperty?.Id;

            var query = _context.SiteVisitReports
                .Include(r => r.Items)
                .Include(r => r.CreatedByUser)
                .OrderByDescending(r => r.VisitDate)
                .ThenByDescending(r => r.Id)
                .AsQueryable();

            if (propertyId.HasValue)
            {
                query = query.Where(r => r.PropertyId == propertyId.Value);
            }

            var reports = await query.Take(50).ToListAsync();
            var entries = reports.Select(MapLogEntry).ToList();

            if (activeEntryId.HasValue && pendingUpdate != null)
            {
                var entry = entries.FirstOrDefault(e => e.Id == activeEntryId.Value);
                if (entry != null)
                {
                    entry.AssignedTo = pendingUpdate.AssignedTo;
                    entry.ProgressStatus = pendingUpdate.ProgressStatus;
                    entry.CompletionNotes = pendingUpdate.CompletionNotes;
                    entry.RecipientEmails = pendingUpdate.RecipientEmails;
                }
            }

            return new SiteVisitLogViewModel
            {
                CurrentPropertyName = currentProperty?.Name,
                Entries = entries
            };
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

        private IReadOnlyList<string> ParseRecipients(string? rawValue, string modelStateKey, bool requireAtLeastOne)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (requireAtLeastOne)
                {
                    ModelState.AddModelError(modelStateKey, "Enter at least one email address.");
                }
                return recipients.ToList();
            }

            var validator = new EmailAddressAttribute();
            var tokens = rawValue
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
                    ModelState.AddModelError(modelStateKey, $"'{trimmed}' is not a valid email address.");
                    continue;
                }

                recipients.Add(trimmed);
            }

            if (requireAtLeastOne && recipients.Count == 0)
            {
                ModelState.AddModelError(modelStateKey, "Enter at least one email address.");
            }

            return recipients.ToList();
        }

        private EmailAttachment CreateWorkbookAttachment(SiteVisitReport report)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Site Visit");

            worksheet.Cell(1, 1).Value = "Site Visit Checklist";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Range(1, 1, 1, 3).Merge();

            worksheet.Cell(2, 1).Value = "Property";
            worksheet.Cell(2, 2).Value = report.PropertyName ?? "Not specified";
            worksheet.Cell(3, 1).Value = "Visit date";
            worksheet.Cell(3, 2).Value = report.VisitDate.ToString("D");
            worksheet.Cell(4, 1).Value = "Visit lead";
            worksheet.Cell(4, 2).Value = string.IsNullOrWhiteSpace(report.LeaderName) ? "Not specified" : report.LeaderName;
            worksheet.Cell(5, 1).Value = "Summary notes";
            worksheet.Cell(5, 2).Value = report.SummaryNotes ?? "—";
            worksheet.Range(5, 2, 5, 3).Merge();

            worksheet.Cell(6, 1).Value = "Assigned To";
            worksheet.Cell(6, 2).Value = string.IsNullOrWhiteSpace(report.AssignedTo) ? "Unassigned" : report.AssignedTo;
            worksheet.Cell(7, 1).Value = "Progress";
            worksheet.Cell(7, 2).Value = ProgressLabel(report.ProgressStatus);
            worksheet.Cell(8, 1).Value = "Completion Notes";
            worksheet.Cell(8, 2).Value = string.IsNullOrWhiteSpace(report.CompletionNotes) ? "—" : report.CompletionNotes;
            worksheet.Range(8, 2, 8, 3).Merge();

            var headerRow = 10;
            worksheet.Cell(headerRow, 1).Value = "Checklist Item";
            worksheet.Cell(headerRow, 2).Value = "Status";
            worksheet.Cell(headerRow, 3).Value = "Notes";
            worksheet.Range(headerRow, 1, headerRow, 3).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#e9ecef"));

            var row = headerRow + 1;
            foreach (var item in report.Items.OrderBy(i => i.Id))
            {
                worksheet.Cell(row, 1).Value = item.Title;
                worksheet.Cell(row, 2).Value = StatusLabel(item.Status);
                worksheet.Cell(row, 3).Value = item.Notes ?? string.Empty;

                XLColor? statusColor = item.Status switch
                {
                    SiteVisitChecklistStatus.Compliant => XLColor.FromHtml("#d1e7dd"),
                    SiteVisitChecklistStatus.NeedsReview => XLColor.FromHtml("#fff3cd"),
                    SiteVisitChecklistStatus.NotCompliant => XLColor.FromHtml("#f8d7da"),
                    _ => null
                };

                if (statusColor != null)
                {
                    worksheet.Cell(row, 2).Style.Fill.SetBackgroundColor(statusColor);
                }

                worksheet.Cell(row, 3).Style.Alignment.WrapText = true;
                row++;
            }

            worksheet.Columns(1, 3).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"SiteVisit-{BuildSafeFileSegment(report.PropertyName)}-{report.VisitDate:yyyyMMdd}.xlsx";
            return new EmailAttachment(fileName, stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        private static string StatusLabel(SiteVisitChecklistStatus status)
        {
            return status switch
            {
                SiteVisitChecklistStatus.Compliant => "Compliant",
                SiteVisitChecklistStatus.NeedsReview => "Needs Review",
                SiteVisitChecklistStatus.NotCompliant => "Not Compliant",
                SiteVisitChecklistStatus.NotReviewed => "Not Reviewed",
                _ => status.ToString()
            };
        }

        private static string ProgressLabel(SiteVisitProgressStatus status)
        {
            return status switch
            {
                SiteVisitProgressStatus.NotStarted => "Not Started",
                SiteVisitProgressStatus.InProgress => "In Progress",
                SiteVisitProgressStatus.Complete => "Complete",
                _ => status.ToString()
            };
        }

        private string BuildEmailSubject(SiteVisitReport report)
        {
            var property = string.IsNullOrWhiteSpace(report.PropertyName) ? "Property" : report.PropertyName;
            return $"Site Visit Checklist - {property} ({report.VisitDate:MMM d, yyyy})";
        }

        private string BuildEmailBody(SiteVisitReport report)
        {
            var builder = new StringBuilder();
            var property = Encode(report.PropertyName ?? "Not specified");
            var leader = Encode(string.IsNullOrWhiteSpace(report.LeaderName) ? "Not specified" : report.LeaderName!);
            var summary = EncodeWithBreaks(report.SummaryNotes);
            var assignedTo = Encode(string.IsNullOrWhiteSpace(report.AssignedTo) ? "Unassigned" : report.AssignedTo!);
            var progress = Encode(ProgressLabel(report.ProgressStatus));
            var completionNotes = EncodeWithBreaks(report.CompletionNotes);

            builder.Append("<p>");
            builder.Append($"Site visit summary for <strong>{property}</strong> on <strong>{report.VisitDate:D}</strong>.");
            builder.Append("</p>");
            builder.Append("<ul>");
            builder.Append($"<li><strong>Visit lead:</strong> {leader}</li>");
            builder.Append($"<li><strong>Assigned to:</strong> {assignedTo}</li>");
            builder.Append($"<li><strong>Progress:</strong> {progress}</li>");
            if (!string.IsNullOrWhiteSpace(summary))
            {
                builder.Append($"<li><strong>Summary:</strong> {summary}</li>");
            }
            if (!string.IsNullOrWhiteSpace(completionNotes))
            {
                builder.Append($"<li><strong>Completion notes:</strong> {completionNotes}</li>");
            }
            builder.Append("</ul>");

            builder.Append("<table style=\"border-collapse:collapse;width:100%;max-width:800px;\">");
            builder.Append("<thead><tr>");
            builder.Append("<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;\">Item</th>");
            builder.Append("<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;width:150px;\">Status</th>");
            builder.Append("<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;\">Notes</th>");
            builder.Append("</tr></thead><tbody>");

            foreach (var item in report.Items.OrderBy(i => i.Id))
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

        private static string BuildDisplayName(ApplicationUser? user)
        {
            if (user == null)
            {
                return "Unknown user";
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

            if (parts.Count > 0)
            {
                return string.Join(' ', parts);
            }

            return user.Email ?? user.UserName ?? "Unknown user";
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

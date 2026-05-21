using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Threading;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Services.Localization;
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
        private readonly ITranslationService _translationService;

        private static readonly (string Section, string Title)[] DefaultChecklistEntries =
        {
            ("Grounds & Landscaping", "Arrival experience & curb appeal"),
            ("Grounds & Landscaping", "Trees & shrubs are trimmed and healthy"),
            ("Grounds & Landscaping", "Irrigation systems are leak-free and scheduled"),
            ("Grounds & Landscaping", "Pavement and parking lot are clean and in good repair"),
            ("Public Areas", "Lobby and public space presentation"),
            ("Public Areas", "HVAC temperatures are comfortable"),
            ("Public Areas", "Floors, walls, and ceilings are clean"),
            ("Public Areas", "Front desk engagement & service"),
            ("Guest Rooms", "Guest room readiness and housekeeping"),
            ("Back of House", "Back-of-house cleanliness & organization"),
            ("Maintenance & Safety", "Maintenance and safety equipment checks"),
            ("Brand & Team", "Brand standards / marketing compliance"),
            ("Brand & Team", "Team engagement & training conversations")
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
            ILogger<SiteVisitController> logger,
            ITranslationService translationService)
            : base(context, userManager)
        {
            _emailSender = emailSender;
            _htmlEncoder = htmlEncoder;
            _logger = logger;
            _translationService = translationService;
        }

        private string GetActiveLanguage()
        {
            return HttpContext?.Items?["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
        }

        private string Translate(string key, string? fallback = null)
        {
            var language = GetActiveLanguage();
            return _translationService.Translate(key, language, fallback ?? key);
        }

        private async Task<string> TranslateDynamicAsync(
            string entityType,
            string entityId,
            string field,
            string? sourceText,
            CancellationToken cancellationToken = default)
        {
            var text = sourceText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var targetLanguage = GetActiveLanguage();
            if (string.Equals(targetLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var translated = await _translationService.TranslateDynamicAsync(
                entityType,
                entityId,
                field,
                text,
                _translationService.DefaultLanguage,
                targetLanguage,
                cancellationToken).ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(translated) ? text : translated;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? templateId = null)
        {
            ViewData["Title"] = Translate("Site Visit");
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
            ViewData["Title"] = Translate("Site Visit");
            ViewData["MainContainerClass"] = "main-inner--wide";

            NormalizeModel(model);

            ModelState.Clear();
            var isValid = TryValidateModel(model);

            var recipients = ParseRecipients(model.RecipientEmails, nameof(model.RecipientEmails), requireAtLeastOne: true);
            if (recipients.Count == 0)
            {
                ModelState.AddModelError(nameof(model.RecipientEmails), Translate("Enter at least one valid email address."));
                isValid = false;
            }

            if (model.Items.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Items), Translate("Please add at least one checklist item."));
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

            try
            {
                await _context.SiteVisitReports.AddAsync(report);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist site visit report for {Property}", report.PropertyName);
                ModelState.AddModelError(string.Empty, Translate("We could not save the visit to the log. Please try again."));
                await PopulateTemplateOptionsAsync(model);
                EnsurePlaceholderRow(model);
                return View("Index", model);
            }

            var attachment = await CreateWorkbookAttachmentAsync(report);
            var attachmentList = new[] { attachment };
            var subject = BuildEmailSubject(report);
            var body = await BuildEmailBodyAsync(report);

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
                ModelState.AddModelError(string.Empty, Translate("We could not send the site visit email. Please verify the addresses and try again."));
                await PopulateTemplateOptionsAsync(model);
                EnsurePlaceholderRow(model);
                return View("Index", model);
            }

            var successTemplate = Translate("Site visit checklist emailed to {0} recipient(s).", "Site visit checklist emailed to {0} recipient(s).");
            var successText = string.Format(CultureInfo.CurrentCulture, successTemplate, successCount);
            TempData[SuccessTempDataKey] = successText;

            _logger.LogInformation("Site visit checklist emailed to {RecipientCount} recipient(s) for property {Property}", successCount, model.PropertyName ?? "(unspecified)");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Templates()
        {
            ViewData["Title"] = Translate("Site Visit Templates");
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
            ViewData["Title"] = Translate("Site Visit Templates");
            ViewData["MainContainerClass"] = "main-inner--wide";

            var parsedRows = new List<(string? Section, string Title)>();
            if (form.CsvFile == null || form.CsvFile.Length == 0)
            {
                ModelState.AddModelError(nameof(form.CsvFile), Translate("Select a CSV file to upload."));
            }
            else if (form.CsvFile.Length > TemplateFileSizeLimitBytes)
            {
                ModelState.AddModelError(nameof(form.CsvFile), Translate("The CSV file is too large. Limit uploads to 2 MB."));
            }

            if (ModelState.IsValid && form.CsvFile != null)
            {
                try
                {
                    parsedRows = await ParseTemplateCsvAsync(form.CsvFile);
                    if (parsedRows.Count == 0)
                    {
                        ModelState.AddModelError(nameof(form.CsvFile), Translate("The file did not contain any checklist rows."));
                    }
                }
                catch (InvalidOperationException)
                {
                    var limitMessage = Translate("Templates can include up to {0} checklist rows.", "Templates can include up to {0} checklist rows.");
                    ModelState.AddModelError(nameof(form.CsvFile), string.Format(CultureInfo.CurrentCulture, limitMessage, TemplateRowLimit));
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

            for (var index = 0; index < parsedRows.Count; index++)
            {
                var row = parsedRows[index];
                template.Items.Add(new SiteVisitTemplateItem
                {
                    SectionName = row.Section,
                    Title = row.Title,
                    SortOrder = index
                });
            }

            await _context.SiteVisitTemplates.AddAsync(template);
            await _context.SaveChangesAsync();

            var itemLabel = parsedRows.Count == 1 ? Translate("item") : Translate("items");
            var templateMessage = Translate("Template '{0}' created with {1} {2}.", "Template '{0}' created with {1} {2}.");
            TempData[TemplateSuccessTempDataKey] = string.Format(CultureInfo.CurrentCulture, templateMessage, template.Name, parsedRows.Count, itemLabel);

            return RedirectToAction(nameof(Templates));
        }

        [HttpGet]
        public IActionResult DownloadBlankTemplate()
        {
            var builder = new StringBuilder();
            var sectionHeader = Translate("Section");
            var titleHeader = Translate("Title");
            builder.AppendLine($"{EscapeForCsv(sectionHeader)},{EscapeForCsv(titleHeader)}");
            foreach (var entry in DefaultChecklistEntries)
            {
                var section = string.IsNullOrWhiteSpace(entry.Section)
                    ? string.Empty
                    : Translate(entry.Section, entry.Section);
                var title = Translate(entry.Title, entry.Title);
                builder.AppendLine($"{EscapeForCsv(section)},{EscapeForCsv(title)}");
            }

            static string EscapeForCsv(string? value)
            {
                var text = value ?? string.Empty;
                var needsQuotes = text.Contains(',', StringComparison.Ordinal) ||
                                  text.Contains('"', StringComparison.Ordinal) ||
                                  text.Contains('\n') ||
                                  text.Contains('\r');
                var cleaned = text.Replace("\"", "\"\"", StringComparison.Ordinal);
                return needsQuotes ? $"\"{cleaned}\"" : cleaned;
            }

            var payload = Encoding.UTF8.GetBytes(builder.ToString());
            return File(payload, "text/csv", "site-visit-template.csv");
        }

        [HttpGet]
        public async Task<IActionResult> Log()
        {
            ViewData["Title"] = Translate("Site Visit Log");
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
            ViewData["Title"] = Translate("Site Visit Log");
            ViewData["MainContainerClass"] = "main-inner--wide";

            var report = await _context.SiteVisitReports
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (report == null)
            {
                ModelState.AddModelError(string.Empty, Translate("We could not find that site visit."));
                ViewBag.ActiveLogEntryId = model.Id;
                var missingViewModel = await BuildLogViewModelAsync(model.Id, model);
                return View("Log", missingViewModel);
            }

            var requireRecipients = string.Equals(model.SubmitAction, "email", StringComparison.OrdinalIgnoreCase);
            var recipients = ParseRecipients(model.RecipientEmails, nameof(model.RecipientEmails), requireRecipients);

            if (requireRecipients && recipients.Count == 0)
            {
                ModelState.AddModelError(nameof(model.RecipientEmails), Translate("Enter at least one recipient before emailing an update."));
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
                var attachment = await CreateWorkbookAttachmentAsync(report);
                var attachmentList = new[] { attachment };
                var subject = BuildEmailSubject(report);
                var body = await BuildEmailBodyAsync(report);
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
                    ModelState.AddModelError(string.Empty, Translate("We could not send the updated site visit. Please try again."));
                    ViewBag.ActiveLogEntryId = model.Id;
                    var failureViewModel = await BuildLogViewModelAsync(model.Id, model);
                    return View("Log", failureViewModel);
                }

                var updateTemplate = Translate("Update emailed to {0} recipient(s).", "Update emailed to {0} recipient(s).");
                TempData[LogSuccessTempDataKey] = string.Format(CultureInfo.CurrentCulture, updateTemplate, successCount);
            }
            else
            {
                TempData[LogSuccessTempDataKey] = Translate("Site visit progress saved.");
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
                    SectionName = item.SectionName,
                    Title = item.Title,
                    Status = SiteVisitChecklistStatus.NotReviewed
                })
                .ToList();
        }

        private List<SelectListItem> BuildTemplateSelectList(IEnumerable<SiteVisitTemplate> templates, int? selectedTemplateId)
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
                Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ({1} {2})",
                    template.Name,
                    template.Items.Count,
                    template.Items.Count == 1 ? "item" : "items"),
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

        private async Task<List<(string? Section, string Title)>> ParseTemplateCsvAsync(IFormFile file)
        {
            var rows = new List<(string? Section, string Title)>();

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            var lineNumber = 0;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                if (rows.Count >= TemplateRowLimit)
                {
                    throw new InvalidOperationException($"Templates can include up to {TemplateRowLimit} checklist rows.");
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var cells = SplitCsvLine(line);
                if (lineNumber == 1)
                {
                    if (cells.Count > 1 &&
                        cells[0].Trim().Equals("section", StringComparison.OrdinalIgnoreCase) &&
                        cells[1].Trim().Equals("title", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (cells.Count == 1 && cells[0].Trim().Equals("title", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                string? section = null;
                string title;

                if (cells.Count > 1)
                {
                    section = string.IsNullOrWhiteSpace(cells[0]) ? null : cells[0].Trim();
                    title = cells[1].Trim();
                }
                else
                {
                    title = cells[0].Trim();
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(section) && section.Length > 200)
                {
                    section = section.Substring(0, 200).Trim();
                }

                if (title.Length > 200)
                {
                    title = title.Substring(0, 200).Trim();
                }

                rows.Add((section, title));
            }

            return rows;
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
            return DefaultChecklistEntries
                .Select(entry => new SiteVisitChecklistItemViewModel
                {
                    SectionName = entry.Section,
                    Title = entry.Title,
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
                    SectionName = item.SectionName,
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
                        ReportItemId = i.Id,
                        SectionName = i.SectionName,
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
                    SectionName = string.IsNullOrWhiteSpace(item.SectionName) ? null : item.SectionName.Trim(),
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
                    ModelState.AddModelError(modelStateKey, Translate("Enter at least one email address."));
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
                    var invalidTemplate = Translate("'{0}' is not a valid email address.", "'{0}' is not a valid email address.");
                    ModelState.AddModelError(modelStateKey, string.Format(CultureInfo.CurrentCulture, invalidTemplate, trimmed));
                    continue;
                }

                recipients.Add(trimmed);
            }

            if (requireAtLeastOne && recipients.Count == 0)
            {
                ModelState.AddModelError(modelStateKey, Translate("Enter at least one email address."));
            }

            return recipients.ToList();
        }

        private async Task<EmailAttachment> CreateWorkbookAttachmentAsync(SiteVisitReport report)
        {
            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
            var activeLanguage = GetActiveLanguage();
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(activeLanguage);
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.CurrentCulture;
            }

            var reportId = report.Id.ToString(CultureInfo.InvariantCulture);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(Translate("Site Visit"));

            worksheet.Cell(1, 1).Value = Translate("Site Visit Checklist");
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Range(1, 1, 1, 4).Merge();

            var propertyLabel = Translate("Property");
            var visitDateLabel = Translate("Visit date");
            var visitLeadLabel = Translate("Visit lead");
            var summaryLabel = Translate("Summary notes");
            var assignedLabel = Translate("Assigned To");
            var progressLabel = Translate("Progress");
            var completionLabel = Translate("Completion Notes");
            var sectionHeader = Translate("Section");
            var itemHeader = Translate("Checklist Item");
            var statusHeader = Translate("Status");
            var notesHeader = Translate("Notes");
            var notSpecified = Translate("Not specified");
            var unassigned = Translate("Unassigned");
            var noneValue = Translate("None");

            worksheet.Cell(2, 1).Value = propertyLabel;
            worksheet.Cell(2, 2).Value = string.IsNullOrWhiteSpace(report.PropertyName) ? notSpecified : report.PropertyName;

            worksheet.Cell(3, 1).Value = visitDateLabel;
            worksheet.Cell(3, 2).Value = report.VisitDate.ToString("D", culture);

            worksheet.Cell(4, 1).Value = visitLeadLabel;
            worksheet.Cell(4, 2).Value = string.IsNullOrWhiteSpace(report.LeaderName) ? notSpecified : report.LeaderName;

            var summaryValue = string.IsNullOrWhiteSpace(report.SummaryNotes)
                ? noneValue
                : await TranslateDynamicAsync("SiteVisitReport", reportId, "SummaryNotes", report.SummaryNotes, cancellationToken);

            worksheet.Cell(5, 1).Value = summaryLabel;
            worksheet.Cell(5, 2).Value = summaryValue;
            worksheet.Range(5, 2, 5, 3).Merge();

            worksheet.Cell(6, 1).Value = assignedLabel;
            worksheet.Cell(6, 2).Value = string.IsNullOrWhiteSpace(report.AssignedTo) ? unassigned : report.AssignedTo;

            worksheet.Cell(7, 1).Value = progressLabel;
            worksheet.Cell(7, 2).Value = ProgressLabel(report.ProgressStatus);

            var completionValue = string.IsNullOrWhiteSpace(report.CompletionNotes)
                ? noneValue
                : await TranslateDynamicAsync("SiteVisitReport", reportId, "CompletionNotes", report.CompletionNotes, cancellationToken);

            worksheet.Cell(8, 1).Value = completionLabel;
            worksheet.Cell(8, 2).Value = completionValue;
            worksheet.Range(8, 2, 8, 3).Merge();

            var headerRow = 10;
            worksheet.Cell(headerRow, 1).Value = sectionHeader;
            worksheet.Cell(headerRow, 2).Value = itemHeader;
            worksheet.Cell(headerRow, 3).Value = statusHeader;
            worksheet.Cell(headerRow, 4).Value = notesHeader;
            worksheet.Range(headerRow, 1, headerRow, 4).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#e9ecef"));

            var row = headerRow + 1;
            foreach (var item in report.Items.OrderBy(i => i.Id))
            {
                var itemId = item.Id.ToString(CultureInfo.InvariantCulture);
                var sectionValue = string.IsNullOrWhiteSpace(item.SectionName)
                    ? string.Empty
                    : await TranslateDynamicAsync("SiteVisitReportItem", itemId, "SectionName", item.SectionName, cancellationToken);
                var titleValue = await TranslateDynamicAsync("SiteVisitReportItem", itemId, "Title", item.Title, cancellationToken);
                var notesValue = string.IsNullOrWhiteSpace(item.Notes)
                    ? string.Empty
                    : await TranslateDynamicAsync("SiteVisitReportItem", itemId, "Notes", item.Notes, cancellationToken);

                worksheet.Cell(row, 1).Value = sectionValue;
                worksheet.Cell(row, 2).Value = titleValue;
                worksheet.Cell(row, 3).Value = StatusLabel(item.Status);
                worksheet.Cell(row, 4).Value = notesValue;

                XLColor? statusColor = item.Status switch
                {
                    SiteVisitChecklistStatus.Compliant => XLColor.FromHtml("#d1e7dd"),
                    SiteVisitChecklistStatus.NeedsReview => XLColor.FromHtml("#fff3cd"),
                    SiteVisitChecklistStatus.NotCompliant => XLColor.FromHtml("#f8d7da"),
                    _ => null
                };

                if (statusColor != null)
                {
                    worksheet.Cell(row, 3).Style.Fill.SetBackgroundColor(statusColor);
                }

                worksheet.Cell(row, 4).Style.Alignment.WrapText = true;
                row++;
            }

            worksheet.Columns(1, 4).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"SiteVisit-{BuildSafeFileSegment(report.PropertyName)}-{report.VisitDate:yyyyMMdd}.xlsx";
            return new EmailAttachment(fileName, stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        private string StatusLabel(SiteVisitChecklistStatus status)
        {
            return status switch
            {
                SiteVisitChecklistStatus.Compliant => Translate("Compliant"),
                SiteVisitChecklistStatus.NeedsReview => Translate("Needs Review"),
                SiteVisitChecklistStatus.NotCompliant => Translate("Not Compliant"),
                SiteVisitChecklistStatus.NotReviewed => Translate("Not Reviewed"),
                _ => status.ToString()
            };
        }

        private string ProgressLabel(SiteVisitProgressStatus status)
        {
            return status switch
            {
                SiteVisitProgressStatus.NotStarted => Translate("Not Started"),
                SiteVisitProgressStatus.InProgress => Translate("In Progress"),
                SiteVisitProgressStatus.Complete => Translate("Complete"),
                _ => status.ToString()
            };
        }

        private string BuildEmailSubject(SiteVisitReport report)
        {
            var property = string.IsNullOrWhiteSpace(report.PropertyName) ? Translate("Property") : report.PropertyName;
            var culture = CultureInfo.CurrentCulture;
            try
            {
                culture = CultureInfo.GetCultureInfo(GetActiveLanguage());
            }
            catch (CultureNotFoundException)
            {
                // Fallback to current culture.
            }

            var visitDate = report.VisitDate.ToString("MMM d, yyyy", culture);
            var template = Translate("Site Visit Checklist - {0} ({1})", "Site Visit Checklist - {0} ({1})");
            return string.Format(CultureInfo.CurrentCulture, template, property, visitDate);
        }

        private async Task<string> BuildEmailBodyAsync(SiteVisitReport report)
        {
            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
            var activeLanguage = GetActiveLanguage();
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(activeLanguage);
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.CurrentCulture;
            }

            var reportId = report.Id.ToString(CultureInfo.InvariantCulture);

            var builder = new StringBuilder();
            var property = Encode(report.PropertyName ?? Translate("Not specified"));
            var visitDateDisplay = Encode(report.VisitDate.ToString("D", culture));
            var leader = Encode(string.IsNullOrWhiteSpace(report.LeaderName) ? Translate("Not specified") : report.LeaderName!);
            var assignedTo = Encode(string.IsNullOrWhiteSpace(report.AssignedTo) ? Translate("Unassigned") : report.AssignedTo!);
            var progress = Encode(ProgressLabel(report.ProgressStatus));
            var summary = string.IsNullOrWhiteSpace(report.SummaryNotes)
                ? string.Empty
                : EncodeWithBreaks(await TranslateDynamicAsync("SiteVisitReport", reportId, "SummaryNotes", report.SummaryNotes, cancellationToken));
            var completionNotes = string.IsNullOrWhiteSpace(report.CompletionNotes)
                ? string.Empty
                : EncodeWithBreaks(await TranslateDynamicAsync("SiteVisitReport", reportId, "CompletionNotes", report.CompletionNotes, cancellationToken));

            var introTemplate = Translate("Site visit summary for <strong>{0}</strong> on <strong>{1}</strong>.", "Site visit summary for <strong>{0}</strong> on <strong>{1}</strong>.");
            var visitLeadLabel = Translate("Visit lead:");
            var assignedLabel = Translate("Assigned to:");
            var progressLabel = Translate("Progress:");
            var summaryLabel = Translate("Summary:");
            var completionLabel = Translate("Completion notes:");
            var sectionHeader = Translate("Section");
            var itemHeader = Translate("Checklist Item");
            var statusHeader = Translate("Status");
            var notesHeader = Translate("Notes");
            var attachmentNote = Translate("The full checklist is attached as an Excel file.");

            builder.Append("<p>");
            builder.AppendFormat(CultureInfo.InvariantCulture, introTemplate, property, visitDateDisplay);
            builder.Append("</p>");
            builder.Append("<ul>");
            builder.AppendFormat(CultureInfo.InvariantCulture, "<li><strong>{0}</strong> {1}</li>", visitLeadLabel, leader);
            builder.AppendFormat(CultureInfo.InvariantCulture, "<li><strong>{0}</strong> {1}</li>", assignedLabel, assignedTo);
            builder.AppendFormat(CultureInfo.InvariantCulture, "<li><strong>{0}</strong> {1}</li>", progressLabel, progress);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "<li><strong>{0}</strong> {1}</li>", summaryLabel, summary);
            }
            if (!string.IsNullOrWhiteSpace(completionNotes))
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "<li><strong>{0}</strong> {1}</li>", completionLabel, completionNotes);
            }
            builder.Append("</ul>");

            builder.Append("<table style=\"border-collapse:collapse;width:100%;max-width:800px;\">");
            builder.Append("<thead><tr>");
            builder.AppendFormat(CultureInfo.InvariantCulture, "<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;width:160px;\">{0}</th>", sectionHeader);
            builder.AppendFormat(CultureInfo.InvariantCulture, "<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;\">{0}</th>", itemHeader);
            builder.AppendFormat(CultureInfo.InvariantCulture, "<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;width:150px;\">{0}</th>", statusHeader);
            builder.AppendFormat(CultureInfo.InvariantCulture, "<th style=\"text-align:left;border-bottom:2px solid #dee2e6;padding:8px;\">{0}</th>", notesHeader);
            builder.Append("</tr></thead><tbody>");

            foreach (var item in report.Items.OrderBy(i => i.Id))
            {
                var itemId = item.Id.ToString(CultureInfo.InvariantCulture);
                var sectionValue = string.IsNullOrWhiteSpace(item.SectionName)
                    ? "&nbsp;"
                    : Encode(await TranslateDynamicAsync("SiteVisitReportItem", itemId, "SectionName", item.SectionName, cancellationToken));
                var titleValue = Encode(await TranslateDynamicAsync("SiteVisitReportItem", itemId, "Title", item.Title, cancellationToken));
                var notesValue = string.IsNullOrWhiteSpace(item.Notes)
                    ? "&nbsp;"
                    : EncodeWithBreaks(await TranslateDynamicAsync("SiteVisitReportItem", itemId, "Notes", item.Notes, cancellationToken));

                builder.Append("<tr>");
                builder.AppendFormat(CultureInfo.InvariantCulture, "<td style=\"border-bottom:1px solid #f1f3f5;padding:8px;\">{0}</td>", sectionValue);
                builder.AppendFormat(CultureInfo.InvariantCulture, "<td style=\"border-bottom:1px solid #f1f3f5;padding:8px;\">{0}</td>", titleValue);
                builder.AppendFormat(CultureInfo.InvariantCulture, "<td style=\"border-bottom:1px solid #f1f3f5;padding:8px;\">{0}</td>", Encode(StatusLabel(item.Status)));
                builder.AppendFormat(CultureInfo.InvariantCulture, "<td style=\"border-bottom:1px solid #f1f3f5;padding:8px;\">{0}</td>", notesValue);
                builder.Append("</tr>");
            }

            builder.Append("</tbody></table>");
            builder.AppendFormat(CultureInfo.InvariantCulture, "<p>{0}</p>", attachmentNote);

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

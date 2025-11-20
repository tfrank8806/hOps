using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class SalesController : BaseController
    {
        private readonly IEmailSender _emailSender;
        private readonly HtmlEncoder _htmlEncoder;
        private readonly ILogger<SalesController> _logger;

        private const string SuccessTempDataKey = "SalesLeadSubmitted";

        public SalesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            HtmlEncoder htmlEncoder,
            ILogger<SalesController> logger)
            : base(context, userManager)
        {
            _emailSender = emailSender;
            _htmlEncoder = htmlEncoder;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var viewModel = await BuildPageModelAsync(user, null);

            if (TempData.ContainsKey(SuccessTempDataKey))
            {
                viewModel.SubmittedSuccessfully = true;
                TempData.Remove(SuccessTempDataKey);
            }

            ViewData["Title"] = "Sales Lead";
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index([Bind(Prefix = "Form")] SalesLeadFormViewModel form)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            form.GroupName = form.GroupName?.Trim() ?? string.Empty;
            form.ContactName = form.ContactName?.Trim() ?? string.Empty;
            form.ContactPhone = string.IsNullOrWhiteSpace(form.ContactPhone) ? null : form.ContactPhone.Trim();
            form.ContactEmail = form.ContactEmail?.Trim() ?? string.Empty;
            form.SubmittedByName = string.IsNullOrWhiteSpace(form.SubmittedByName)
                ? BuildDisplayName(user) ?? user.Email ?? user.UserName ?? string.Empty
                : form.SubmittedByName.Trim();
            form.InquiryOtherDetails = string.IsNullOrWhiteSpace(form.InquiryOtherDetails)
                ? null
                : form.InquiryOtherDetails.Trim();
            form.AdditionalDetails = string.IsNullOrWhiteSpace(form.AdditionalDetails)
                ? null
                : form.AdditionalDetails.Trim();
            form.InquiryTypes ??= new List<string>();

            var viewModel = await BuildPageModelAsync(user, form);
            var currentProperty = ViewBag.CurrentProperty as Property;

            ViewData["Title"] = "Sales Lead";

            if (currentProperty == null)
            {
                ModelState.AddModelError(string.Empty, "Select a property before submitting a sales lead.");
                return View(viewModel);
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            if (!accessiblePropertyIds.Contains(currentProperty.Id))
            {
                return Forbid();
            }

            SalesContact? selectedContact = null;
            if (form.SalesContactId.HasValue)
            {
                selectedContact = await _context.SalesContacts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == form.SalesContactId.Value && c.PropertyId == currentProperty.Id);
                if (selectedContact == null)
                {
                    ModelState.AddModelError("Form.SalesContactId", "Select a sales contact for this property.");
                }
            }
            else
            {
                ModelState.AddModelError("Form.SalesContactId", "Select a sales contact.");
            }

            if (form.InquiryTypes.Count == 0)
            {
                ModelState.AddModelError("Form.InquiryTypes", "Select at least one inquiry type.");
            }
            else if (form.InquiryTypes.Any(type => !SalesLeadFormViewModel.IsValidInquiryKey(type)))
            {
                ModelState.AddModelError("Form.InquiryTypes", "One or more inquiry types are invalid.");
            }

            var otherSelected = form.InquiryTypes.Any(type =>
                type.Equals(SalesLeadFormViewModel.OtherInquiryKey, StringComparison.OrdinalIgnoreCase));
            if (otherSelected && string.IsNullOrWhiteSpace(form.InquiryOtherDetails))
            {
                ModelState.AddModelError("Form.InquiryOtherDetails", "Share more information for the 'Other' inquiry type.");
            }

            if (form.StartDate.HasValue && form.EndDate.HasValue && form.EndDate.Value < form.StartDate.Value)
            {
                ModelState.AddModelError("Form.EndDate", "End date cannot be before the start date.");
            }

            if (form.BudgetMinimum.HasValue && form.BudgetMaximum.HasValue &&
                form.BudgetMinimum.Value > form.BudgetMaximum.Value)
            {
                ModelState.AddModelError("Form.BudgetMaximum", "Budget maximum must be greater than or equal to the minimum.");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var recipients = await BuildRecipientListAsync(currentProperty.Id, selectedContact!.Email);
            if (recipients.Count == 0)
            {
                _logger.LogWarning("Sales lead submission aborted because no recipients were resolved for property {PropertyId}.", currentProperty.Id);
                ModelState.AddModelError(string.Empty, "We could not find an email recipient for this property. Add a sales contact or ensure managers have email addresses.");
                return View(viewModel);
            }

            var subject = $"Sales lead - {form.GroupName}".Trim();
            var body = BuildSalesLeadEmailBody(form, selectedContact!, currentProperty, user);

            foreach (var email in recipients)
            {
                try
                {
                    await _emailSender.SendEmailAsync(email, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send sales lead email to {Recipient}", email);
                }
            }

            TempData[SuccessTempDataKey] = true;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddContact([FromBody] SalesContactInputModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                return BadRequest(new { message = "Select a property before adding a sales contact." });
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            if (!accessiblePropertyIds.Contains(currentProperty.Id))
            {
                return Forbid();
            }

            if (input == null)
            {
                return BadRequest(new { message = "Send the contact details in the request body." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kvp => kvp.Value?.Errors?.Any() == true)
                    .Select(kvp => new
                    {
                        field = kvp.Key,
                        message = kvp.Value!.Errors.First().ErrorMessage
                    })
                    .ToList();
                return BadRequest(new { message = "Please fix the highlighted fields.", errors });
            }

            var name = input.Name.Trim();
            var email = input.Email.Trim();
            var emailLower = email.ToLowerInvariant();

            var exists = await _context.SalesContacts
                .AnyAsync(sc => sc.PropertyId == currentProperty.Id && sc.Email.ToLower() == emailLower);
            if (exists)
            {
                return BadRequest(new { message = "A sales contact with that email already exists for this property." });
            }

            var contact = new SalesContact
            {
                Name = name,
                Email = email,
                PropertyId = currentProperty.Id,
                CreatedByUserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.SalesContacts.Add(contact);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to create sales contact for property {PropertyId}", currentProperty.Id);
                return StatusCode(500, new { message = "Something went wrong while saving the sales contact. Please try again." });
            }

            return Ok(new
            {
                contact = new
                {
                    id = contact.Id,
                    name = contact.Name,
                    email = contact.Email
                }
            });
        }

        private async Task<SalesLeadPageViewModel> BuildPageModelAsync(ApplicationUser? user, SalesLeadFormViewModel? form)
        {
            var property = ViewBag.CurrentProperty as Property;
            form ??= new SalesLeadFormViewModel();
            form.InquiryTypes ??= new List<string>();

            if (string.IsNullOrWhiteSpace(form.SubmittedByName) && user != null)
            {
                form.SubmittedByName = BuildDisplayName(user) ?? user.Email ?? user.UserName ?? string.Empty;
            }

            var contactOptions = new List<SelectListItem>();
            if (property != null)
            {
                var contacts = await _context.SalesContacts
                    .Where(sc => sc.PropertyId == property.Id)
                    .OrderBy(sc => sc.Name)
                    .ThenBy(sc => sc.Email)
                    .AsNoTracking()
                    .ToListAsync();

                contactOptions = contacts
                    .Select(sc => new SelectListItem
                    {
                        Value = sc.Id.ToString(CultureInfo.InvariantCulture),
                        Text = $"{sc.Name} ({sc.Email})",
                        Selected = form.SalesContactId.HasValue && sc.Id == form.SalesContactId.Value
                    })
                    .ToList();
            }

            return new SalesLeadPageViewModel
            {
                Form = form,
                SalesContactOptions = contactOptions,
                CurrentPropertyId = property?.Id,
                CurrentPropertyName = property?.Name,
                CurrentPropertyCode = property?.Code
            };
        }

        private string BuildSalesLeadEmailBody(
            SalesLeadFormViewModel form,
            SalesContact contact,
            Property property,
            ApplicationUser submitter)
        {
            var builder = new StringBuilder();
            var culture = CultureInfo.CurrentCulture;
            var submittedBy = string.IsNullOrWhiteSpace(form.SubmittedByName)
                ? BuildDisplayName(submitter) ?? submitter.Email ?? "Unknown user"
                : form.SubmittedByName;

            builder.Append("<p>A new sales lead was submitted in hOps.</p>");
            builder.Append("<table style=\"border-collapse:collapse; width:100%; max-width:720px;\">");
            AppendRow(builder, "Property", $"{property.Name} ({property.Code})");
            AppendRow(builder, "Sales contact", $"{contact.Name} ({contact.Email})");
            AppendRow(builder, "Submitted by", submittedBy);
            AppendRow(builder, "Group / Company", form.GroupName);
            AppendRow(builder, "Contact name", form.ContactName);
            AppendRow(builder, "Contact phone", string.IsNullOrWhiteSpace(form.ContactPhone) ? "Not provided" : form.ContactPhone!);
            AppendRow(builder, "Contact email", form.ContactEmail);
            AppendRow(builder, "Number of rooms", FormatQuantity(form.NumberOfRooms, culture));
            AppendRow(builder, "Number of guests", FormatQuantity(form.NumberOfGuests, culture));

            var inquiryLabels = form.InquiryTypes
                .Select(SalesLeadFormViewModel.GetInquiryLabel)
                .ToList();
            if (inquiryLabels.Count > 0)
            {
                var inquiryText = string.Join("<br />", inquiryLabels.Select(_htmlEncoder.Encode));
                if (!string.IsNullOrWhiteSpace(form.InquiryOtherDetails))
                {
                    inquiryText += "<br /><em>Other details:</em> " + _htmlEncoder.Encode(form.InquiryOtherDetails);
                }
                AppendRow(builder, "Inquiry type(s)", inquiryText, encodeValue: false);
            }
            else
            {
                AppendRow(builder, "Inquiry type(s)", "Not provided");
            }

            AppendRow(builder, "Dates", FormatDateRange(form.StartDate, form.EndDate, culture));
            AppendRow(builder, "Budget", FormatBudget(form.BudgetMinimum, form.BudgetMaximum, culture));
            builder.Append("</table>");

            builder.Append("<hr style=\"margin:1.5rem 0;\" />");
            builder.Append("<p style=\"margin-bottom:0.5rem;\"><strong>Additional Details</strong></p>");
            var detailText = string.IsNullOrWhiteSpace(form.AdditionalDetails)
                ? "No additional details were provided."
                : _htmlEncoder.Encode(form.AdditionalDetails)
                    .Replace("\r\n", "<br />", StringComparison.Ordinal)
                    .Replace("\n", "<br />", StringComparison.Ordinal)
                    .Replace("\r", "<br />", StringComparison.Ordinal);
            builder.Append("<div style=\"white-space:pre-wrap;\">").Append(detailText).Append("</div>");

            return builder.ToString();
        }

        private string FormatDateRange(DateTime? start, DateTime? end, CultureInfo culture)
        {
            if (!start.HasValue && !end.HasValue)
            {
                return "Not provided";
            }

            if (start.HasValue && end.HasValue)
            {
                var startText = start.Value.ToString("MMM d, yyyy", culture);
                var endText = end.Value.ToString("MMM d, yyyy", culture);
                return $"{startText} - {endText}";
            }

            var dateValue = (start ?? end)!.Value.ToString("MMM d, yyyy", culture);
            return dateValue;
        }

        private string FormatBudget(decimal? minimum, decimal? maximum, CultureInfo culture)
        {
            if (!minimum.HasValue && !maximum.HasValue)
            {
                return "Not provided";
            }

            if (minimum.HasValue && maximum.HasValue)
            {
                return $"{minimum.Value.ToString("C0", culture)} - {maximum.Value.ToString("C0", culture)}";
            }

            var value = minimum ?? maximum;
            return value?.ToString("C0", culture) ?? "Not provided";
        }

        private string FormatQuantity(int? value, CultureInfo culture)
        {
            if (!value.HasValue || value.Value <= 0)
            {
                return "Not provided";
            }

            return value.Value.ToString("N0", culture);
        }

        private void AppendRow(StringBuilder builder, string label, string value, bool encodeValue = true)
        {
            builder.Append("<tr>")
                .Append("<td style=\"padding:4px 8px; font-weight:600; width:200px; vertical-align:top;\">")
                .Append(_htmlEncoder.Encode(label))
                .Append("</td>")
                .Append("<td style=\"padding:4px 8px;\">")
                .Append(encodeValue ? _htmlEncoder.Encode(value ?? string.Empty) : value ?? string.Empty)
                .Append("</td>")
                .Append("</tr>");
        }

        private async Task<HashSet<int>> GetAccessiblePropertyIdsAsync(ApplicationUser user)
        {
            var ids = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        private async Task<List<string>> BuildRecipientListAsync(int propertyId, string? salesContactEmail)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(salesContactEmail))
            {
                recipients.Add(salesContactEmail);
            }

            var managementEmails = await (from upa in _context.UserPropertyAccesses
                                          join user in _context.Users on upa.ApplicationUserId equals user.Id
                                          join userRole in _context.UserRoles on upa.ApplicationUserId equals userRole.UserId
                                          join role in _context.Roles on userRole.RoleId equals role.Id
                                          where upa.PropertyId == propertyId &&
                                                (role.NormalizedName == "ADMIN" || role.NormalizedName == "MANAGER")
                                          select user.Email)
                .Where(email => email != null && email != string.Empty)
                .Distinct()
                .ToListAsync();

            foreach (var email in managementEmails)
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    recipients.Add(email);
                }
            }

            return recipients.ToList();
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

            if (parts.Count == 0)
            {
                return user.Email ?? user.UserName;
            }

            return string.Join(" ", parts);
        }
    }
}

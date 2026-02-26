using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace hOps.web.Models
{
    public class PhonebookContact : IValidatableObject
    {
        public int Id { get; set; }

        public string TypeName { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public int? PhonebookTypeId { get; set; }

        public PhonebookType? PhonebookType { get; set; }

        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        public string? Company { get; set; }

        public string? Title { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Phone Number Types")]
        public string? PhoneNumberTypes { get; set; }

        public string? Email { get; set; }

        [Phone]
        public string? Fax { get; set; }

        public string? Website { get; set; }

        public string? Address { get; set; }

        public string? Notes { get; set; }

        [NotMapped]
        public string DisplayName
        {
            get
            {
                var nameParts = new[] { FirstName, LastName }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (nameParts.Length > 0)
                {
                    return string.Join(" ", nameParts);
                }

                if (!string.IsNullOrWhiteSpace(Company))
                {
                    return Company!;
                }

                return TypeName;
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var phoneValidator = new PhoneAttribute();
            foreach (var phone in SplitMultiline(PhoneNumber))
            {
                if (!phoneValidator.IsValid(phone))
                {
                    yield return new ValidationResult($"Invalid phone number: {phone}", new[] { nameof(PhoneNumber) });
                }
            }

            var phoneEntries = SplitMultiline(PhoneNumber).ToList();
            var typeEntries = SplitMultiline(PhoneNumberTypes).ToList();
            if (typeEntries.Count > 0 && typeEntries.Count != phoneEntries.Count)
            {
                yield return new ValidationResult(
                    "Each phone number must have a matching phone type.",
                    new[] { nameof(PhoneNumberTypes) });
            }

            Website = Website?.Trim();

            if (!string.IsNullOrWhiteSpace(Website) && !IsAllowedWebsite(Website))
            {
                yield return new ValidationResult(
                    "Enter a valid website (example.com or https://example.com).",
                    new[] { nameof(Website) });
            }

            var emailValidator = new EmailAddressAttribute();
            foreach (var email in SplitMultiline(Email))
            {
                if (!emailValidator.IsValid(email))
                {
                    yield return new ValidationResult($"Invalid email address: {email}", new[] { nameof(Email) });
                }
            }
        }

        private static IEnumerable<string> SplitMultiline(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Enumerable.Empty<string>();
            }

            return value
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v));
        }

        private static bool IsAllowedWebsite(string value)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps ||
                 uri.Scheme == Uri.UriSchemeFtp))
            {
                return true;
            }

            if (!value.Contains(' ') &&
                Uri.TryCreate($"https://{value}", UriKind.Absolute, out var httpsUri) &&
                (httpsUri.Scheme == Uri.UriSchemeHttp || httpsUri.Scheme == Uri.UriSchemeHttps))
            {
                return true;
            }

            return false;
        }
    }
}

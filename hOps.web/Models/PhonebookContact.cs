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

        [Required]
        [Display(Name = "Type")]
        public string TypeName { get; set; } = string.Empty;

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

        public string? Email { get; set; }

        [Phone]
        public string? Fax { get; set; }

        [Url]
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
    }
}

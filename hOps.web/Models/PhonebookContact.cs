using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace hOps.web.Models
{
    public class PhonebookContact
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

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [EmailAddress]
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
    }
}

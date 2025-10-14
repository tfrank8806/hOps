#nullable enable

using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class Property
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "MARSHA / INN Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Property Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }
    }
}

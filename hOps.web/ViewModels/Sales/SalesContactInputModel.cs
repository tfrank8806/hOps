using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Sales
{
    public class SalesContactInputModel
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }
}

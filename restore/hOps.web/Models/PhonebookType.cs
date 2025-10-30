using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PhonebookType
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Type Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Color Code (HEX)")]
        public string Color { get; set; } = "#0d6efd";
    }
}

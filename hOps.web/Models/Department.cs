using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Department Name")]
        public string ? Name { get; set; }

        [Display(Name = "Color Code (HEX)")]
        public string Color { get; set; } = "#6c757d"; // default gray

        public int? PropertyId { get; set; }
        public Property? Property { get; set; }
    }
}

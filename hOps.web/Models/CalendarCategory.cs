using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class CalendarCategory
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Color Code (HEX)")]
        public string Color { get; set; } = "#198754"; // Default: Bootstrap green

        public int? PropertyId { get; set; }
        public Property? Property { get; set; }

        public ICollection<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
    }
}

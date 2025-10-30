using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class RoomType
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Room Type Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class Room
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
        public string RoomNumber { get; set; } = null!;
        [StringLength(3)]
        public string? Abbreviation { get; set; }
        public int Floor { get; set; }
        public string RoomType { get; set; } = null!;
        public string? Description { get; set; }
        public bool IncludeInPreventiveMaintenance { get; set; } = true;
        public bool IncludeInDeepClean { get; set; } = true;
        // optional layout position
        public int X { get; set; }
        public int Y { get; set; }

        public ICollection<RoomLayout> RoomLayouts { get; set; } = new List<RoomLayout>();
    }
}

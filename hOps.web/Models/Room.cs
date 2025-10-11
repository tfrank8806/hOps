namespace hOps.web.Models
{
    public class Room
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
        public string RoomNumber { get; set; } = null!;
        public int Floor { get; set; }
        public string RoomType { get; set; } = null!;
        // optional layout position
        public int X { get; set; }
        public int Y { get; set; }
    }
}

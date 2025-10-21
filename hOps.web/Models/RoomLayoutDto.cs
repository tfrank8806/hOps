namespace hOps.web.ViewModels
{
    public class RoomLayoutDto
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public int RoomId { get; set; }
        public int Floor { get; set; }  // use room’s floor number
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Label { get; set; } // Optional label for display purposes
        public string? ShapeType { get; set; }
        public string? ShapeData { get; set; }
    }
}

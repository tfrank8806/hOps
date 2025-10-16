using hOps.web.Models;

public class RoomLayout
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public int RoomId { get; set; }
    public int Floor { get; set; }

    public int X { get; set; }   // Left, in px or grid units
    public int Y { get; set; }   // Top
    public int Width { get; set; }
    public int Height { get; set; }
}

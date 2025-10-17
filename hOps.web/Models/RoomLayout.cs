using hOps.web.Models;

namespace hOps.web.Models
{
    /// <summary>
    /// Represents the layout position of a room on a floor plan.
    /// </summary>
    public class RoomLayout
    {
        public int Id { get; set; }

        /// <summary>
        /// The property this layout belongs to.
        /// </summary>
        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        /// <summary>
        /// The room this layout box is associated with.
        /// </summary>
        public int RoomId { get; set; }

        /// <summary>
        /// The floor number the layout applies to (matches Room.Floor).
        /// </summary>
        public int Floor { get; set; }

        /// <summary>
        /// X-coordinate position on the layout grid.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y-coordinate position on the layout grid.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width of the room shape (in grid units).
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the room shape (in grid units).
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Optional display label (used for custom shapes).
        /// </summary>
        public string? Label { get; set; }
    }
}

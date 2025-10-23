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
        public Room? Room { get; set; }

        /// <summary>
        /// The floor number the layout applies to (matches Room.Floor).
        /// </summary>
        public int Floor { get; set; }

        /// <summary>
        /// X-coordinate position, in pixels captured from the layout editor canvas.
        /// See <c>Views/Settings/LayoutEditor.cshtml</c> for snap-to-grid behavior that
        /// can quantize this value.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y-coordinate position, in pixels captured from the layout editor canvas.
        /// See <c>Views/Settings/LayoutEditor.cshtml</c> for snap-to-grid behavior that
        /// can quantize this value.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width of the room shape, measured in pixels from the layout editor when the
        /// user drops or resizes a box.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the room shape, measured in pixels from the layout editor when the
        /// user drops or resizes a box.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Optional display label (used for custom shapes).
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Optional identifier describing the visual shape of the layout element
        /// (e.g., rectangle, l-shape, custom polygon).
        /// </summary>
        public string? ShapeType { get; set; }

        /// <summary>
        /// Optional serialized data used to render the custom shape. When
        /// <see cref="ShapeType"/> is a pre-defined value this may be null, but it
        /// can also contain a CSS clip-path polygon or other data needed by the
        /// layout editor to reproduce the custom appearance.
        /// </summary>
        public string? ShapeData { get; set; }
    }
}

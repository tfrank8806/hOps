using System.Collections.Generic;
using hOps.web.Models;

namespace hOps.web.ViewModels
{
    public class LayoutEditorViewModel
    {
        public int PropertyId { get; set; }
        public List<Room> Rooms { get; set; } = new();
        public List<RoomLayout> Layouts { get; set; } = new();
    }
}

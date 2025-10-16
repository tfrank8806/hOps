using System.Collections.Generic;
using hOps.web.Models;

namespace hOps.web.ViewModels
{
    public class LayoutEditorViewModel
    {
        public int PropertyId { get; set; }
        public int SelectedFloor { get; set; }
        public List<int> AllFloors { get; set; } = new List<int>();
        public List<Room> Rooms { get; set; } = new List<Room>();
        public List<RoomLayout> Layouts { get; set; } = new List<RoomLayout>();
    }
}

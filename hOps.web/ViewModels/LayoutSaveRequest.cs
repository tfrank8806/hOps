using System.Collections.Generic;
namespace hOps.web.ViewModels
{
    public class LayoutSaveRequest
    {
        public int PropertyId { get; set; }
        public int Floor { get; set; }
        public List<RoomLayoutDto> Layouts { get; set; } = new();
    }
}

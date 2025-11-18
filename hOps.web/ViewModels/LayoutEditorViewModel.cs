using System.Collections.Generic;
using hOps.web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels
{
    public class LayoutEditorViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public int SelectedFloor { get; set; }
        public List<int> AllFloors { get; set; } = new List<int>();
        public List<LayoutEditorRoomViewModel> Rooms { get; set; } = new List<LayoutEditorRoomViewModel>();
        public List<LayoutEditorRoomLayoutViewModel> Layouts { get; set; } = new List<LayoutEditorRoomLayoutViewModel>();
        public Dictionary<int, List<LayoutEditorRoomLayoutViewModel>> LayoutsByFloor { get; set; } = new Dictionary<int, List<LayoutEditorRoomLayoutViewModel>>();
        public List<SelectListItem> PropertyOptions { get; set; } = new List<SelectListItem>();
    }

    public class LayoutEditorRoomViewModel
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int Floor { get; set; }
    }

    public class LayoutEditorRoomLayoutViewModel
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public int RoomId { get; set; }
        public int Floor { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Label { get; set; }
        public string? ShapeType { get; set; }
        public string? ShapeData { get; set; }
        public int TextRotation { get; set; }
    }
}

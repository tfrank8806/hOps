#nullable enable

using System;
using System.Collections.Generic;

namespace hOps.web.ViewModels.Maintenance
{
    public class MaintenanceArchiveViewModel
    {
        public string PageTitle { get; set; } = string.Empty;
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public int SelectedYear { get; set; }
        public IReadOnlyList<int> AvailableYears { get; set; } = Array.Empty<int>();
        public IReadOnlyList<MaintenanceArchiveRoomViewModel> Rooms { get; set; } = Array.Empty<MaintenanceArchiveRoomViewModel>();
    }

    public class MaintenanceArchiveRoomViewModel
    {
        public string RoomNumber { get; set; } = string.Empty;
        public IReadOnlyList<DateTime> CompletionDates { get; set; } = Array.Empty<DateTime>();
    }
}

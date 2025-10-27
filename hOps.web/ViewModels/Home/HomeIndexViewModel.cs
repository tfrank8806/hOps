using System;
using System.Collections.Generic;
using hOps.web.Models;

namespace hOps.web.ViewModels.Home
{
    public class HomeIndexViewModel
    {
        public Property? CurrentProperty { get; set; }
        public bool CanManageAnnouncements { get; set; }

        public ManagerAnnouncementViewModel Announcement { get; set; } = new();

        public List<BulletinPostViewModel> BulletinPosts { get; set; } = new();

        public List<RoomLayoutTileViewModel> RoomTiles { get; set; } = new();

        public List<WorkOrderSummaryViewModel> WorkOrders { get; set; } = new();

        public List<LostFoundSummaryViewModel> LostFoundEntries { get; set; } = new();

        public List<PackageLogSummaryViewModel> PackageLogs { get; set; } = new();

        public List<CalendarEventSummaryViewModel> UpcomingEvents { get; set; } = new();
    }

    public class ManagerAnnouncementViewModel
    {
        public int? Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
    }

    public class BulletinPostViewModel
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
        public bool CanEdit { get; set; }
    }

    public class RoomLayoutTileViewModel
    {
        public int LayoutId { get; set; }
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public int Floor { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ShapeType { get; set; } = string.Empty;
        public string? ShapeData { get; set; }
        public string FloorColor { get; set; } = "#6c757d";
        public string CssClass { get; set; } = string.Empty;
        public List<RoomTileBadgeViewModel> Badges { get; set; } = new();
    }

    public class RoomTileBadgeViewModel
    {
        public string Text { get; set; } = string.Empty;
        public string Variant { get; set; } = "secondary";
        public string? Url { get; set; }
    }

    public class WorkOrderSummaryViewModel
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string? Location { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DetailUrl { get; set; } = string.Empty;
    }

    public class LostFoundSummaryViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public LostFoundType Type { get; set; }
        public LostFoundStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DetailUrl { get; set; } = string.Empty;
    }

    public class PackageLogSummaryViewModel
    {
        public int Id { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string? RoomNumber { get; set; }
        public string? Carrier { get; set; }
        public string? TrackingNumber { get; set; }
        public string? StorageLocation { get; set; }
        public bool Delivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime LoggedAt { get; set; }
        public string DetailUrl { get; set; } = string.Empty;
    }

    public class CalendarEventSummaryViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryColor { get; set; }
        public string DetailUrl { get; set; } = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using hOps.web.Models;
using hOps.web.ViewModels;

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
        public List<ActivityFeedItemViewModel> ActivityFeed { get; set; } = new();
        public List<LayoutPersonaViewModel> LayoutPersonas { get; set; } = new();
        public string SelectedPersona { get; set; } = "default";
        public bool CanManageWidgets { get; set; }
        public List<WidgetMarketplaceItemViewModel> MarketplaceModules { get; set; } = new();

        public List<LostFoundSummaryViewModel> LostFoundEntries { get; set; } = new();

        public List<PackageLogSummaryViewModel> PackageLogs { get; set; } = new();

        public List<CalendarEventSummaryViewModel> UpcomingEvents { get; set; } = new();

        public List<PassOnLogSummaryViewModel> PassOnLogs { get; set; } = new();

        public List<MyScheduleShiftViewModel> MyScheduleShifts { get; set; } = new();

        public List<QuickSelectOptionViewModel> WorkOrderTypeOptions { get; set; } = new();
        public List<QuickSelectOptionViewModel> DepartmentOptions { get; set; } = new();
        public string DefaultWorkOrderStatus { get; set; } = "New";

        public List<HomeWidgetLayoutEntry> WidgetLayout { get; set; } = new();
        public Dictionary<HomeWidgetSize, string> WidgetSizeClasses { get; set; } = new();
        public List<HomeWidgetDefinition> ActiveWidgetDefinitions { get; set; } = new();
        public int WidgetHeightMin { get; set; } = 220;
        public int WidgetHeightMax { get; set; } = 1500;
        public int WidgetHeightStep { get; set; } = 10;
        public int WidgetHeightDefault { get; set; } = 300;
        public int WidgetHeightResetThreshold { get; set; } = 5;
    }

    public class ManagerAnnouncementViewModel
    {
        public int? Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
        public List<HomeAttachmentViewModel> Attachments { get; set; } = new();
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
        public List<HomeAttachmentViewModel> Attachments { get; set; } = new();
    }

    public class RoomLayoutTileViewModel
    {
        public int LayoutId { get; set; }
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string? LocationKey { get; set; }
        public string RoomType { get; set; } = string.Empty;
        public int Floor { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ShapeType { get; set; } = string.Empty;
        public string? ShapeData { get; set; }
        public int TextRotation { get; set; }
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
        public string? DepartmentName { get; set; }
        public string? DepartmentColor { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime DueDate { get; set; }
        public string PriorityLabel { get; set; } = string.Empty;
        public string PriorityClass { get; set; } = "badge bg-light text-dark border";
        public string SlaStatus { get; set; } = string.Empty;
        public string SlaStatusClass { get; set; } = string.Empty;
        public string SlaSummary { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
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
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryColor { get; set; }
        public string DetailUrl { get; set; } = string.Empty;
        public string DateDisplay { get; set; } = string.Empty;
        public string? TimeDisplay { get; set; }
        public bool HasTime => !string.IsNullOrWhiteSpace(TimeDisplay);
    }

    public class PassOnLogSummaryViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public UserAvatarViewModel CreatorAvatar { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string DetailUrl { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }

    public class HomeAttachmentViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public class QuickSelectOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class HomeWidgetLayoutEntry
    {
        public string WidgetId { get; set; } = string.Empty;
        public HomeWidgetSize Size { get; set; } = HomeWidgetSize.Third;
        public int? CustomSpan { get; set; }
        public int? CustomHeight { get; set; }
    }

    public enum HomeWidgetSize
    {
        Full,
        Half,
        Third,
        Quarter
    }

    public class HomeWidgetDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public HomeWidgetSize DefaultSize { get; init; } = HomeWidgetSize.Third;
        public int DefaultHeight { get; init; } = 300;
    }

    public static class HomeWidgetIds
    {
        public const string Announcements = "announcements";
        public const string Bulletins = "bulletins";
        public const string PassOnLogs = "passOnLogs";
        public const string PackageLog = "packageLog";
        public const string UpcomingEvents = "upcomingEvents";
        public const string WorkOrders = "workOrders";
        public const string LostFound = "lostFound";
        public const string HotelLayout = "hotelLayout";
        public const string OpsFeed = "opsFeed";
        public const string MySchedule = "mySchedule";
    }

    public class ActivityFeedItemViewModel
    {
        public string ItemType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public string? Meta { get; set; }
        public string? LinkUrl { get; set; }
        public string? BadgeText { get; set; }
        public string? BadgeClass { get; set; }
        public DateTime OccurredAt { get; set; }
        public UserAvatarViewModel? Avatar { get; set; }
        public bool CanReply { get; set; }
        public int? PassOnLogId { get; set; }
        public string ReplyPlaceholder { get; set; } = "Add a quick reply…";
        public string? ReplyReturnUrl { get; set; }
    }

    public class LayoutPersonaViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class WidgetMarketplaceItemViewModel
    {
        public string WidgetId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class MyScheduleShiftViewModel
    {
        public int AssignmentId { get; set; }
        public int ScheduleId { get; set; }
        public int ScheduleEmployeeId { get; set; }
        public DateTime ShiftDate { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan? ShiftStartTime { get; set; }
        public TimeSpan? ShiftEndTime { get; set; }
        public string? Notes { get; set; }
        public string? ScheduleTitle { get; set; }
        public DateTime WeekStartDate { get; set; }
    }
}

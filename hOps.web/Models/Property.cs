#nullable enable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace hOps.web.Models
{
    public class Property
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "MARSHA / INN Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Property Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }

        // Optional: Used for navigation in EF and View rendering
        public ICollection<UserPropertyAccess> UserAccesses { get; set; } = new List<UserPropertyAccess>();

        public ICollection<Room> Rooms { get; set; } = new List<Room>();

        public ICollection<WorkOrderProperty> WorkOrderLinks { get; set; } = new List<WorkOrderProperty>();
        public ICollection<CalendarEventProperty> CalendarEvents { get; set; } = new List<CalendarEventProperty>();
        public ICollection<LostFoundEntry> LostFoundEntries { get; set; } = new List<LostFoundEntry>();

        public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
        public ICollection<Department> Departments { get; set; } = new List<Department>();
        public ICollection<WorkOrderType> WorkOrderTypes { get; set; } = new List<WorkOrderType>();
        public ICollection<PhonebookType> PhonebookTypes { get; set; } = new List<PhonebookType>();
        public ICollection<CalendarCategory> CalendarCategories { get; set; } = new List<CalendarCategory>();

        [InverseProperty(nameof(PassOnLogProperty.Property))]
        public ICollection<PassOnLogProperty> PassOnLogLinks { get; set; } = new List<PassOnLogProperty>();

        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<DocumentProperty> DocumentLinks { get; set; } = new List<DocumentProperty>();
        public ICollection<DocumentFolderProperty> DocumentFolderLinks { get; set; } = new List<DocumentFolderProperty>();

        public ICollection<ManagerAnnouncement> ManagerAnnouncements { get; set; } = new List<ManagerAnnouncement>();
        public ICollection<SalesContact> SalesContacts { get; set; } = new List<SalesContact>();
        public ICollection<SalesLeadSubmission> SalesLeadSubmissions { get; set; } = new List<SalesLeadSubmission>();

        public ICollection<BulletinPost> BulletinPosts { get; set; } = new List<BulletinPost>();

        public ICollection<PackageLogEntry> PackageLogEntries { get; set; } = new List<PackageLogEntry>();
        public ICollection<UserPropertyEmailSubscription> EmailSubscriptions { get; set; } = new List<UserPropertyEmailSubscription>();

        public ScheduleSettings? ScheduleSettings { get; set; }
        public ICollection<ScheduleShiftTemplate> ScheduleShiftTemplates { get; set; } = new List<ScheduleShiftTemplate>();
        public ICollection<ScheduleEmployee> ScheduleEmployees { get; set; } = new List<ScheduleEmployee>();
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public ICollection<ScheduleTimeOffRequest> ScheduleTimeOffRequests { get; set; } = new List<ScheduleTimeOffRequest>();

        public PreventiveMaintenanceSetting? PreventiveMaintenanceSetting { get; set; }
        public ICollection<PreventiveMaintenanceChecklist> PreventiveMaintenanceChecklists { get; set; } = new List<PreventiveMaintenanceChecklist>();
        public ICollection<PreventiveMaintenanceTask> PreventiveMaintenanceTasks { get; set; } = new List<PreventiveMaintenanceTask>();
        public ICollection<PreventiveMaintenanceSession> PreventiveMaintenanceSessions { get; set; } = new List<PreventiveMaintenanceSession>();

        public DeepCleanSetting? DeepCleanSetting { get; set; }
        public ICollection<DeepCleanChecklistItem> DeepCleanChecklistItems { get; set; } = new List<DeepCleanChecklistItem>();
        public ICollection<DeepCleanSession> DeepCleanSessions { get; set; } = new List<DeepCleanSession>();

        public LinenInventorySettings? LinenInventorySettings { get; set; }
        public ICollection<LinenInventoryRoomType> LinenInventoryRoomTypes { get; set; } = new List<LinenInventoryRoomType>();
        public ICollection<LinenInventoryItem> LinenInventoryItems { get; set; } = new List<LinenInventoryItem>();
        public ICollection<LinenInventorySession> LinenInventorySessions { get; set; } = new List<LinenInventorySession>();
        public SupplyInventoryState? SupplyInventoryState { get; set; }
        public ICollection<SupplyInventorySnapshot> SupplyInventorySnapshots { get; set; } = new List<SupplyInventorySnapshot>();

        public ICollection<EquipmentItem> EquipmentItems { get; set; } = new List<EquipmentItem>();
        public ICollection<MaintenanceLogTemplate> MaintenanceLogTemplates { get; set; } = new List<MaintenanceLogTemplate>();
        public ICollection<EmergencyLightTestEntry> EmergencyLightTestEntries { get; set; } = new List<EmergencyLightTestEntry>();

        public ICollection<HousekeeperProfile> Housekeepers { get; set; } = new List<HousekeeperProfile>();
        public ICollection<HousekeepingMprEntry> HousekeepingMprEntries { get; set; } = new List<HousekeepingMprEntry>();
    }
}

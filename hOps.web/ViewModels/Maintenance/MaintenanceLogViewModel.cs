#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using hOps.web.Models;
using hOps.web.Utilities;

namespace hOps.web.ViewModels.Maintenance
{
    public class MaintenanceLogsIndexViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public IReadOnlyList<MaintenanceLogTemplateSummaryViewModel> Templates { get; set; } = Array.Empty<MaintenanceLogTemplateSummaryViewModel>();
    }

    public class MaintenanceLogTemplateSummaryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public MaintenanceLogScheduleType ScheduleType { get; set; } = MaintenanceLogScheduleType.None;
        public string ScheduleSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int EntryCount { get; set; }
        public DateTime? LastEntryDate { get; set; }
    }

    public class MaintenanceLogTemplateEditorViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public bool IsEditMode => Id.HasValue;

        public int? Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public MaintenanceLogScheduleType ScheduleType { get; set; } = MaintenanceLogScheduleType.None;

        public bool[] WeeklyDays { get; set; } = new bool[7];

        [Range(1, 31)]
        public int? DayOfMonth { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? DueTimeLocal { get; set; }

        public bool IsActive { get; set; } = true;

        public List<MaintenanceLogColumnEditorViewModel> Columns { get; set; } = new();
    }

    public class MaintenanceLogColumnEditorViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = MaintenanceLogColumnDefinition.DefaultColumnType;
        public bool Required { get; set; }
        public string OptionsText { get; set; } = string.Empty;
        public bool IncludeNotes { get; set; }
        public bool IncludePhotos { get; set; }
    }

    public class MaintenanceLogTemplateDetailViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public MaintenanceLogScheduleType ScheduleType { get; set; } = MaintenanceLogScheduleType.None;
        public string ScheduleSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public IReadOnlyList<MaintenanceLogColumnDefinition> Columns { get; set; } = Array.Empty<MaintenanceLogColumnDefinition>();
        public IReadOnlyList<MaintenanceLogEntryViewModel> Entries { get; set; } = Array.Empty<MaintenanceLogEntryViewModel>();
        public DateTime? FilterStart { get; set; }
        public DateTime? FilterEnd { get; set; }
    }

    public class MaintenanceLogEntryViewModel
    {
        public int Id { get; set; }
        public DateTime EntryDate { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public IReadOnlyDictionary<string, string?> Values { get; set; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string?> Notes { get; set; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, IReadOnlyList<MaintenanceLogEntryPhotoViewModel>> Photos { get; set; }
            = new Dictionary<string, IReadOnlyList<MaintenanceLogEntryPhotoViewModel>>(StringComparer.OrdinalIgnoreCase);
    }

    public class MaintenanceLogEntryInputModel
    {
        [DataType(DataType.Date)]
        public DateTime EntryDate { get; set; } = DateTime.UtcNow.Date;

        public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string?> Notes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class MaintenanceLogTemplateReorderRequest
    {
        public List<int> TemplateIds { get; set; } = new();
    }

    public class MaintenanceLogEntryPhotoViewModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public DateTime UploadedAtUtc { get; set; }
    }

    public class EmergencyLightTestingIndexViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanRecord { get; set; }
        public IReadOnlyList<EmergencyLightTestLocationStatusViewModel> LocationStatuses { get; set; }
            = Array.Empty<EmergencyLightTestLocationStatusViewModel>();
        public IReadOnlyList<EmergencyLightTestEntryViewModel> RecentEntries { get; set; }
            = Array.Empty<EmergencyLightTestEntryViewModel>();
        public IReadOnlyList<string> SavedLocations { get; set; } = Array.Empty<string>();
    }

    public class EmergencyLightTestLocationStatusViewModel
    {
        public string Location { get; set; } = string.Empty;
        public DateTime? LastTestDate { get; set; }
        public string? LastTestedBy { get; set; }
        public DateTime? NextDueDate { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsDueSoon { get; set; }
    }

    public class EmergencyLightTestEntryViewModel
    {
        public int Id { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime TestDate { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class EmergencyLightTestEntryInputModel
    {
        [Required]
        [StringLength(160)]
        public string Location { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Required]
        public DateTime? TestDate { get; set; }
    }
}

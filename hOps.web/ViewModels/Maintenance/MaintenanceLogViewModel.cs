#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.IO;
using hOps.web.Models;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.Maintenance
{
    public class MaintenanceLogsIndexViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public MaintenanceLogIndexFilterViewModel Filters { get; set; } = new();
        public int AdditionalHistoryBlocks { get; set; }
        public IReadOnlyList<MaintenanceLogTemplateListItemViewModel> DailyTemplates { get; set; } = Array.Empty<MaintenanceLogTemplateListItemViewModel>();
        public IReadOnlyList<MaintenanceLogTemplateListItemViewModel> WeeklyTemplates { get; set; } = Array.Empty<MaintenanceLogTemplateListItemViewModel>();
        public IReadOnlyList<MaintenanceLogTemplateListItemViewModel> MonthlyTemplates { get; set; } = Array.Empty<MaintenanceLogTemplateListItemViewModel>();
        public IReadOnlyList<MaintenanceLogTemplateListItemViewModel> OtherTemplates { get; set; } = Array.Empty<MaintenanceLogTemplateListItemViewModel>();
        public bool CanLoadMoreHistory { get; set; }
    }

    public class MaintenanceLogIndexFilterViewModel
    {
        public MaintenanceLogScheduleType? ScheduleFilter { get; set; }
        public string StatusFilter { get; set; } = string.Empty;
        public string CompletionFilter { get; set; } = string.Empty;
        public string NameQuery { get; set; } = string.Empty;
    }

    public class MaintenanceLogTemplateListItemViewModel
    {
        public int TemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public MaintenanceLogScheduleType ScheduleType { get; set; } = MaintenanceLogScheduleType.None;
        public string ScheduleSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsOverdue { get; set; }
        public MaintenanceLogCycleStatusKind LatestStatus { get; set; } = MaintenanceLogCycleStatusKind.Upcoming;
        public string? ChecklistFilePath { get; set; }
        public bool HasChecklistFile => !string.IsNullOrWhiteSpace(ChecklistFilePath);
        public IReadOnlyList<MaintenanceLogCycleHistoryItemViewModel> VisibleCycles { get; set; } = Array.Empty<MaintenanceLogCycleHistoryItemViewModel>();
        public bool HasLegacyEntries => VisibleCycles.Any(cycle => cycle.LegacyEntries.Count > 0);
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

        public string? ChecklistFilePath { get; set; }
        public string? ChecklistFileName { get; set; }
        public long? ChecklistFileSizeBytes { get; set; }
        public bool RemoveChecklistFile { get; set; }
        public bool HasChecklistFile => !string.IsNullOrWhiteSpace(ChecklistFilePath);
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

    public class MaintenanceLogCycleDetailViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public MaintenanceLogScheduleType ScheduleType { get; set; } = MaintenanceLogScheduleType.None;
        public string ScheduleSummary { get; set; } = string.Empty;
        public bool CanManage { get; set; }
        public string? ChecklistFilePath { get; set; }
        public MaintenanceLogCycleHistoryItemViewModel Cycle { get; set; } = new();
        public MaintenanceLogCycleCompletionInputModel CompletionForm { get; set; } = new();
        public MaintenanceLogCycleCompletionInputModel? EditForm { get; set; }
        public IReadOnlyList<MaintenanceLogCycleCompletionSummaryViewModel> PriorCompletions { get; set; } = Array.Empty<MaintenanceLogCycleCompletionSummaryViewModel>();
    }

    public class MaintenanceLogTemplateReorderRequest
    {
        public List<int> TemplateIds { get; set; } = new();
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

    public class MaintenanceLogTaskCardViewModel
    {
        public int TemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ScheduleSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastCompletedAtUtc { get; set; }
        public string? LastCompletedByName { get; set; }
        public DateTime? NextDueDate { get; set; }
        public string? ChecklistFilePath { get; set; }
        public IReadOnlyList<MaintenanceCycleDefinitionViewModel> CycleDefinitions { get; set; } = Array.Empty<MaintenanceCycleDefinitionViewModel>();
        public IReadOnlyList<MaintenanceCycleStatusViewModel> CycleStatuses { get; set; } = Array.Empty<MaintenanceCycleStatusViewModel>();
    }

    public class MaintenanceLogCycleCompletionInputModel
    {
        [Required]
        public int TemplateId { get; set; }

        [Required]
        public string WindowKey { get; set; } = string.Empty;

        public int? CompletionId { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime? CompletedAtLocal { get; set; }

        [Range(0, 1440)]
        public int? DurationMinutes { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public MaintenanceLogCompletionResult Result { get; set; } = MaintenanceLogCompletionResult.Passed;

        public bool ConfirmCycleChange { get; set; }

        public List<int> RemoveAttachmentIds { get; set; } = new();
    }

    public class MaintenanceLogCycleHistoryItemViewModel
    {
        public string WindowKey { get; set; } = string.Empty;
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }
        public DateTime DueLocal { get; set; }
        public MaintenanceLogCycleStatusKind Status { get; set; } = MaintenanceLogCycleStatusKind.Upcoming;
        public bool IsLate { get; set; }
        public IReadOnlyList<MaintenanceLogCycleCompletionSummaryViewModel> Completions { get; set; }
            = Array.Empty<MaintenanceLogCycleCompletionSummaryViewModel>();
        public IReadOnlyList<MaintenanceLogLegacyEntryBridgeViewModel> LegacyEntries { get; set; }
            = Array.Empty<MaintenanceLogLegacyEntryBridgeViewModel>();
        public MaintenanceLogCycleCompletionSummaryViewModel? LatestCompletion => Completions.FirstOrDefault();
    }

    public class MaintenanceLogCycleCompletionSummaryViewModel
    {
        public int CompletionId { get; set; }
        public MaintenanceLogCompletionResult Result { get; set; } = MaintenanceLogCompletionResult.Passed;
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? CompletedAtLocal { get; set; }
        public string? CompletedByUserId { get; set; }
        public string? CompletedByName { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Notes { get; set; }
        public bool IsLatest { get; set; }
        public IReadOnlyList<MaintenanceLogCycleAttachmentViewModel> Attachments { get; set; }
            = Array.Empty<MaintenanceLogCycleAttachmentViewModel>();
    }

    public class MaintenanceLogCycleAttachmentViewModel
    {
        public int AttachmentId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime UploadedAtUtc { get; set; }
    }

    public class MaintenanceLogLegacyEntryBridgeViewModel
    {
        public int EntryId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime CreatedAtLocal { get; set; }
        public string? CreatedByName { get; set; }
    }
}

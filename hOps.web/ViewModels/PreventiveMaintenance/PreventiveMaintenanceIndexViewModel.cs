#nullable enable

using System;
using System.Collections.Generic;
using hOps.web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using hOps.web.ViewModels.Maintenance;

namespace hOps.web.ViewModels.PreventiveMaintenance
{
    public class PreventiveMaintenanceIndexViewModel
    {
        public int? PropertyId { get; set; }
        public string? PropertyName { get; set; }
        public int FrequencyPerYear { get; set; }
        public bool HasChecklist { get; set; }
        public int SelectedChecklistId { get; set; }
        public string SelectedChecklistName { get; set; } = string.Empty;
        public PreventiveMaintenanceChecklistType SelectedChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;
        public IReadOnlyList<PreventiveMaintenanceChecklistOptionViewModel> Checklists { get; set; } = Array.Empty<PreventiveMaintenanceChecklistOptionViewModel>();
        public IReadOnlyList<string> AreaOptions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<SelectListItem> RoomOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<PreventiveMaintenanceRoomLogViewModel> RoomLogs { get; set; } = Array.Empty<PreventiveMaintenanceRoomLogViewModel>();
        public IReadOnlyList<PreventiveMaintenanceAreaLogViewModel> AreaLogs { get; set; } = Array.Empty<PreventiveMaintenanceAreaLogViewModel>();
        public PreventiveMaintenanceActiveSessionViewModel? ActiveSession { get; set; }
        public IReadOnlyList<MaintenanceCycleDefinitionViewModel> CycleDefinitions { get; set; } = Array.Empty<MaintenanceCycleDefinitionViewModel>();
        public bool RequiresRoom => SelectedChecklistType == PreventiveMaintenanceChecklistType.Room;
        public bool RequiresArea => SelectedChecklistType == PreventiveMaintenanceChecklistType.Area;
    }

    public class PreventiveMaintenanceChecklistOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PreventiveMaintenanceChecklistType ChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;
        public bool IsActive { get; set; }
    }

    public class PreventiveMaintenanceRoomLogViewModel
    {
        public int? RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime? LastCompletedAtUtc { get; set; }
        public double? LastDurationSeconds { get; set; }
        public bool IsDue { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? NextDueAtUtc { get; set; }
        public string? CompletedByName { get; set; }
        public IReadOnlyList<MaintenanceCycleStatusViewModel> CycleStatuses { get; set; } = Array.Empty<MaintenanceCycleStatusViewModel>();
    }

    public class PreventiveMaintenanceAreaLogViewModel
    {
        public string AreaLabel { get; set; } = string.Empty;
        public DateTime? LastCompletedAtUtc { get; set; }
        public double? LastDurationSeconds { get; set; }
        public bool IsDue { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? NextDueAtUtc { get; set; }
        public string? CompletedByName { get; set; }
        public IReadOnlyList<MaintenanceCycleStatusViewModel> CycleStatuses { get; set; } = Array.Empty<MaintenanceCycleStatusViewModel>();
    }

    public class PreventiveMaintenanceActiveSessionViewModel
    {
        public int SessionId { get; set; }
        public int ChecklistId { get; set; }
        public string ChecklistName { get; set; } = string.Empty;
        public PreventiveMaintenanceChecklistType ChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;
        public string RoomNumber { get; set; } = string.Empty;
        public string? RoomLabel { get; set; }
        public string? AreaLabel { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public double TotalDurationSeconds { get; set; }
        public PreventiveMaintenanceSessionStatus Status { get; set; }
        public IReadOnlyList<PreventiveMaintenanceActiveSessionTaskViewModel> Tasks { get; set; } = Array.Empty<PreventiveMaintenanceActiveSessionTaskViewModel>();
    }

    public class PreventiveMaintenanceActiveSessionTaskViewModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PreventiveMaintenanceTaskStatus Status { get; set; }
        public string? Notes { get; set; }
    }
}

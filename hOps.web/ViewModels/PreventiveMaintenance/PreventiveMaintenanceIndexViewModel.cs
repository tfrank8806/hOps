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
        public IReadOnlyList<SelectListItem> RoomOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<PreventiveMaintenanceRoomLogViewModel> RoomLogs { get; set; } = Array.Empty<PreventiveMaintenanceRoomLogViewModel>();
        public PreventiveMaintenanceActiveSessionViewModel? ActiveSession { get; set; }
        public IReadOnlyList<MaintenanceCycleDefinitionViewModel> CycleDefinitions { get; set; } = Array.Empty<MaintenanceCycleDefinitionViewModel>();
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

    public class PreventiveMaintenanceActiveSessionViewModel
    {
        public int SessionId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string? RoomLabel { get; set; }
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

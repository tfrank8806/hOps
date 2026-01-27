#nullable enable

using System;
using System.Collections.Generic;
using hOps.web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.DeepCleans
{
    public class DeepCleanIndexViewModel
    {
        public int? PropertyId { get; set; }
        public string? PropertyName { get; set; }
        public int FrequencyPerYear { get; set; }
        public bool HasChecklist { get; set; }
        public IReadOnlyList<SelectListItem> RoomOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<DeepCleanRoomLogViewModel> RoomLogs { get; set; } = Array.Empty<DeepCleanRoomLogViewModel>();
        public DeepCleanActiveSessionViewModel? ActiveSession { get; set; }
    }

    public class DeepCleanRoomLogViewModel
    {
        public int? RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime? LastCompletedAtUtc { get; set; }
        public double? LastDurationSeconds { get; set; }
        public bool IsDue { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? NextDueAtUtc { get; set; }
        public string? CompletedByName { get; set; }
    }

    public class DeepCleanActiveSessionViewModel
    {
        public int SessionId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string? RoomLabel { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public double TotalDurationSeconds { get; set; }
        public DeepCleanSessionStatus Status { get; set; }
        public IReadOnlyList<DeepCleanActiveSessionTaskViewModel> Tasks { get; set; } = Array.Empty<DeepCleanActiveSessionTaskViewModel>();
    }

    public class DeepCleanActiveSessionTaskViewModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DeepCleanTaskStatus Status { get; set; }
        public string? Notes { get; set; }
    }
}

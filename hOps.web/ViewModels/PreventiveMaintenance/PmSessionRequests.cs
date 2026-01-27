#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;

namespace hOps.web.ViewModels.PreventiveMaintenance
{
    public class PmSessionStartRequest
    {
        [Required]
        public string RoomNumber { get; set; } = string.Empty;

        public int? RoomId { get; set; }

        [Required]
        public DateTime StartedAtUtc { get; set; }
    }

    public class PmTaskUpdateRequest
    {
        [Required]
        public int SessionId { get; set; }

        [Required]
        public int TaskId { get; set; }

        public PreventiveMaintenanceTaskStatus Status { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class PmSessionCommandRequest
    {
        [Required]
        public int SessionId { get; set; }
    }
}

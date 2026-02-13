#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.PreventiveMaintenance
{
    public class PmManualCompletionRequest
    {
        [Range(1, int.MaxValue)]
        public int ChecklistId { get; set; }

        public int? RoomId { get; set; }

        [Required]
        [MaxLength(64)]
        public string RoomNumber { get; set; } = string.Empty;

        [MaxLength(160)]
        public string? AreaLabel { get; set; }

        [Required]
        public DateTime CompletedAtLocal { get; set; }

        [Range(0, 1440)]
        public int DurationMinutes { get; set; } = 60;
    }
}

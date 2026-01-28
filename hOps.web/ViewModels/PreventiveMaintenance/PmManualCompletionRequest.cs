#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.PreventiveMaintenance
{
    public class PmManualCompletionRequest
    {
        public int? RoomId { get; set; }

        [Required]
        [MaxLength(64)]
        public string RoomNumber { get; set; } = string.Empty;

        [Required]
        public DateTime CompletedAtLocal { get; set; }

        [Range(0, 1440)]
        public int DurationMinutes { get; set; } = 60;
    }
}

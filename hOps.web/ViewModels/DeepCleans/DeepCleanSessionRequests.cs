#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;

namespace hOps.web.ViewModels.DeepCleans
{
    public class DeepCleanSessionStartRequest
    {
        [Required]
        public string RoomNumber { get; set; } = string.Empty;

        public int? RoomId { get; set; }

        [Required]
        public DateTime StartedAtUtc { get; set; }
    }

    public class DeepCleanTaskUpdateRequest
    {
        [Required]
        public int SessionId { get; set; }

        [Required]
        public int TaskId { get; set; }

        public DeepCleanTaskStatus Status { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class DeepCleanSessionCommandRequest
    {
        [Required]
        public int SessionId { get; set; }
    }
}

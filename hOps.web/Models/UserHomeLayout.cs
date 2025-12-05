using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class UserHomeLayout
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        [MaxLength(64)]
        public string PersonaKey { get; set; } = "default";
        public bool IsDefault { get; set; }
        public string LayoutJson { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

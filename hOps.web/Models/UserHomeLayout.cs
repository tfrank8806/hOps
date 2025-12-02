using System;

namespace hOps.web.Models
{
    public class UserHomeLayout
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string LayoutJson { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

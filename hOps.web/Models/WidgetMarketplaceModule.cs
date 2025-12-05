using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class WidgetMarketplaceModule
    {
        public int Id { get; set; }
        [MaxLength(128)]
        public string WidgetId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

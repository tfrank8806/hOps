#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class MaintenanceLogEntry
    {
        public int Id { get; set; }

        public int TemplateId { get; set; }
        public MaintenanceLogTemplate Template { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateTime EntryDate { get; set; } = DateTime.UtcNow.Date;

        public string ValuesJson { get; set; } = "{}";

        public string CreatedByUserId { get; set; } = string.Empty;
        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

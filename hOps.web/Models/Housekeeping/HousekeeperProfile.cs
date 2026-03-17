using System;
using System.Collections.Generic;

namespace hOps.web.Models
{
    public class HousekeeperProfile
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<HousekeepingMprEntry> Entries { get; set; } = new List<HousekeepingMprEntry>();
    }
}

#nullable enable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class LinenInventoryRoomType
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 5000)]
        public int TotalRooms { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<LinenInventoryItemRequirement> Requirements { get; set; } = new List<LinenInventoryItemRequirement>();
    }
}

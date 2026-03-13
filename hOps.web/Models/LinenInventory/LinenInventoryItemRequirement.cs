#nullable enable

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Models
{
    public class LinenInventoryItemRequirement
    {
        public int Id { get; set; }

        public int InventoryItemId { get; set; }

        public LinenInventoryItem InventoryItem { get; set; } = null!;

        public int RoomTypeId { get; set; }

        public LinenInventoryRoomType RoomType { get; set; } = null!;

        [Precision(18, 4)]
        [Range(0, 1000)]
        public decimal UnitsPerRoom { get; set; }
    }
}

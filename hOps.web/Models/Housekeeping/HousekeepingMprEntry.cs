using System;

namespace hOps.web.Models
{
    public class HousekeepingMprEntry
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
        public int? HousekeeperId { get; set; }
        public HousekeeperProfile? Housekeeper { get; set; }
        public string HousekeeperName { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public int CheckoutRooms { get; set; }
        public int LinenChangeRooms { get; set; }
        public int StayoverRooms { get; set; }
        public int DndRooms { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal TotalMinutesWorked { get; set; }
        public decimal? MinutesPerRoom { get; set; }
        public decimal DepartureStandardMinutes { get; set; }
        public decimal LinenChangeStandardMinutes { get; set; }
        public decimal StayoverStandardMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }
    }
}

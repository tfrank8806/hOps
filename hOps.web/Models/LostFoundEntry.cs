using System.ComponentModel.DataAnnotations;
namespace hOps.web.Models
{
    public enum LostFoundType
    {
        Found = 0,
        Lost = 1
    }

    public enum LostFoundStatus
    {
        Logged = 0,
        ReturnedToGuest = 1,
        DisposedOf = 2
    }

    public class LostFoundEntry
    {
        public int Id { get; set; }

        [Required]
        public LostFoundType Type { get; set; }

        [Required]
        public LostFoundStatus Status { get; set; } = LostFoundStatus.Logged;

        [Required]
        public int PropertyId { get; set; }

        public Property? Property { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateFound { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateReportedLost { get; set; }

        [MaxLength(256)]
        public string? FoundBy { get; set; }

        [MaxLength(256)]
        public string? GuestName { get; set; }

        [MaxLength(50)]
        public string? GuestPhone { get; set; }

        [MaxLength(512)]
        public string? GuestAddress { get; set; }

        [MaxLength(256)]
        public string? Location { get; set; }

        [MaxLength(256)]
        public string? ItemFound { get; set; }

        [MaxLength(256)]
        public string? ItemLost { get; set; }

        [MaxLength(1024)]
        public string? Notes { get; set; }

        [MaxLength(256)]
        public string? Stored { get; set; }

        [MaxLength(512)]
        public string? PhotoPath { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        public ApplicationUser? CreatedByUser { get; set; }

        [MaxLength(256)]
        public string CreatedByDisplayName { get; set; } = string.Empty;
    }
}

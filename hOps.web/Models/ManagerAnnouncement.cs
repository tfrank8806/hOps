using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class ManagerAnnouncement
    {
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Content { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string UpdatedById { get; set; } = string.Empty;
        public ApplicationUser? UpdatedBy { get; set; }

        public ICollection<ManagerAnnouncementAttachment> Attachments { get; set; } = new List<ManagerAnnouncementAttachment>();
    }
}

using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class ManagerAnnouncementAttachment
    {
        public int Id { get; set; }

        [Required]
        public int ManagerAnnouncementId { get; set; }
        public ManagerAnnouncement ManagerAnnouncement { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? OriginalFileName { get; set; }
    }
}

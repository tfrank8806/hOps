using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class BulletinPostAttachment
    {
        public int Id { get; set; }

        [Required]
        public int BulletinPostId { get; set; }
        public BulletinPost BulletinPost { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? OriginalFileName { get; set; }
    }
}

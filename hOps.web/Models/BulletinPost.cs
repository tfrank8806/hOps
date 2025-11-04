using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class BulletinPost
    {
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string CreatedById { get; set; } = string.Empty;
        public ApplicationUser? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UpdatedById { get; set; }
        public ApplicationUser? UpdatedBy { get; set; }

        public ICollection<BulletinPostAttachment> Attachments { get; set; } = new List<BulletinPostAttachment>();
    }
}

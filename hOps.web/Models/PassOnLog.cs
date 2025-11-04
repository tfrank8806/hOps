using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace hOps.web.Models
{
    public class PassOnLog
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        public ApplicationUser? CreatedBy { get; set; }

        [InverseProperty(nameof(PassOnLogProperty.PassOnLog))]
        public ICollection<PassOnLogProperty> Properties { get; set; } = new List<PassOnLogProperty>();

        public ICollection<PassOnLogComment> Comments { get; set; } = new List<PassOnLogComment>();

        public ICollection<PassOnLogView> Views { get; set; } = new List<PassOnLogView>();

        public ICollection<PassOnLogAttachment> Attachments { get; set; } = new List<PassOnLogAttachment>();
    }
}

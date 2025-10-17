using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PassOnLogComment
    {
        public int Id { get; set; }

        [Required]
        public int PassOnLogId { get; set; }

        public PassOnLog PassOnLog { get; set; } = default!;

        [Required]
        [MaxLength(2000)]
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        public ApplicationUser? CreatedBy { get; set; }
    }
}

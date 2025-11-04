using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class PassOnLogAttachment
    {
        public int Id { get; set; }

        [Required]
        public int PassOnLogId { get; set; }
        public PassOnLog PassOnLog { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? OriginalFileName { get; set; }
    }
}

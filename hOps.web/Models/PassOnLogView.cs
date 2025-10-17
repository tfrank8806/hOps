namespace hOps.web.Models
{
    public class PassOnLogView
    {
        public int PassOnLogId { get; set; }

        public PassOnLog PassOnLog { get; set; } = default!;

        public string ViewerId { get; set; } = string.Empty;

        public ApplicationUser? Viewer { get; set; }

        public DateTime ViewedAt { get; set; }
    }
}

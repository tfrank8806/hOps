namespace hOps.Mobile.Models
{
    public class PassOnLogListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}

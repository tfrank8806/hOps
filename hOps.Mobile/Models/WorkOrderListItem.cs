namespace hOps.Mobile.Models
{
    public class WorkOrderListItem
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? DueDateUtc { get; set; }
    }
}

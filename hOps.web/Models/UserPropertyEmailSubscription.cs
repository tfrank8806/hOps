namespace hOps.web.Models
{
    public class UserPropertyEmailSubscription
    {
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        public bool IncludeInLogAlerts { get; set; } = true;
        public bool IncludeInDailySummary { get; set; } = true;
        public bool IncludeInWorkOrderAlerts { get; set; } = true;
    }
}

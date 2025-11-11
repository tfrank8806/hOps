namespace hOps.web.Services
{
    public record SchedulePublicationResult(bool Success, string? ErrorMessage = null)
    {
        public static SchedulePublicationResult Ok() => new(true, null);
        public static SchedulePublicationResult Failure(string message) => new(false, message);
    }
}

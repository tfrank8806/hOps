namespace hOps.web.Models
{
    public class CalendarEventProperty
    {
        public int CalendarEventId { get; set; }
        public CalendarEvent CalendarEvent { get; set; } = default!;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;
    }
}

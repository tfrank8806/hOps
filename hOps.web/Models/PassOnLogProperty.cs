namespace hOps.web.Models
{
    public class PassOnLogProperty
    {
        public int PassOnLogId { get; set; }

        public PassOnLog PassOnLog { get; set; } = default!;

        public int PropertyId { get; set; }

        public Property Property { get; set; } = default!;
    }
}

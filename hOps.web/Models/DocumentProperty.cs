namespace hOps.web.Models
{
    public class DocumentProperty
    {
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
    }
}

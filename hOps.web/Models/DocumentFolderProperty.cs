namespace hOps.web.Models
{
    public class DocumentFolderProperty
    {
        public int DocumentFolderId { get; set; }
        public DocumentFolder DocumentFolder { get; set; } = null!;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
    }
}

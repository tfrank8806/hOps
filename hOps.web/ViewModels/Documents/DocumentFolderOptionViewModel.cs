namespace hOps.web.ViewModels.Documents
{
    public class DocumentFolderOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public bool IsSelected { get; set; }
    }
}

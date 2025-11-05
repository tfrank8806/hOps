namespace hOps.web.ViewModels.Documents
{
    public class DocumentFolderListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
    }
}

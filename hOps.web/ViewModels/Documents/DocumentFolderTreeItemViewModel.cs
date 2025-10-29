namespace hOps.web.ViewModels.Documents
{
    public class DocumentFolderTreeItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public int DocumentCount { get; set; }
        public bool IsSelected { get; set; }
        public int? ParentFolderId { get; set; }
    }
}

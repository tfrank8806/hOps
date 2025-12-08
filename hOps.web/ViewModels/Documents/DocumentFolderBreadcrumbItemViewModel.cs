namespace hOps.web.ViewModels.Documents
{
    public class DocumentFolderBreadcrumbItemViewModel
    {
        public int? FolderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }
}

using System.Collections.Generic;

namespace hOps.web.ViewModels.Documents
{
    public class DocumentsIndexViewModel
    {
        public List<DocumentListItemViewModel> Documents { get; set; } = new();
        public DocumentUploadFormViewModel Form { get; set; } = new();
        public List<DocumentPropertyOptionViewModel> PropertyOptions { get; set; } = new();
        public List<DocumentFolderTreeItemViewModel> FolderTree { get; set; } = new();
        public DocumentFolderFormViewModel FolderForm { get; set; } = new();
        public List<DocumentFolderOptionViewModel> FolderOptions { get; set; } = new();
        public int? SelectedFolderId { get; set; }
        public bool ShowingUnassignedOnly { get; set; }
        public int UnassignedDocumentCount { get; set; }
        public int TotalDocumentCount { get; set; }
        public int? CurrentPropertyId { get; set; }
        public string? CurrentPropertyName { get; set; }
        public bool HasPropertyAccess => PropertyOptions.Count > 0;
    }
}

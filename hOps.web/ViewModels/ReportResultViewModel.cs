using System.Collections.Generic;

namespace hOps.web.ViewModels
{
    public class ReportResultViewModel
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Headers { get; set; } = new();
        public List<IReadOnlyList<string>> Rows { get; set; } = new();

        public int RowCount => Rows.Count;
    }
}

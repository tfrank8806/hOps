using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels
{
    public class ReportRequestViewModel
    {
        public string? SelectedReportType { get; set; }

        public bool IncludeAllProperties { get; set; } = true;

        public List<int> SelectedPropertyIds { get; set; } = new();

        public List<SelectListItem> AvailableReports { get; set; } = new();

        public List<SelectListItem> AvailableProperties { get; set; } = new();

        public string? SelectedReportDescription { get; set; }

        public bool SelectedReportSupportsPropertyFilter { get; set; } = true;

        public ReportResultViewModel? Result { get; set; }
    }
}

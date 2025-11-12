using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

        public bool SelectedReportSupportsDateRange { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public ReportResultViewModel? Result { get; set; }

        public bool HasResults => Result?.Rows.Any() ?? false;

        public bool HasFilters => SelectedReportSupportsPropertyFilter || SelectedReportSupportsDateRange;
    }
}

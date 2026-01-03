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

        public bool RefreshFiltersOnly { get; set; }

        public List<int> SelectedPropertyIds { get; set; } = new();

        public List<SelectListItem> AvailableReports { get; set; } = new();

        public List<SelectListItem> AvailableProperties { get; set; } = new();

        public string? SelectedReportDescription { get; set; }

        public List<ReportMetadataViewModel> ReportMetadata { get; set; } = new();

        public bool SelectedReportSupportsPropertyFilter { get; set; } = true;

        public bool SelectedReportSupportsDateRange { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public ReportResultViewModel? Result { get; set; }

        public bool HasResults => Result?.Rows.Any() ?? false;

        public bool HasFilters => SelectedReportSupportsPropertyFilter || SelectedReportSupportsDateRange;

        public bool ShowWorkOrderFilters { get; set; }
        public List<string> SelectedWorkOrderLocations { get; set; } = new();
        public List<SelectListItem> WorkOrderLocationOptions { get; set; } = new();
        public List<int> SelectedWorkOrderTypeIds { get; set; } = new();
        public List<SelectListItem> WorkOrderTypeOptions { get; set; } = new();
        public List<int> SelectedWorkOrderTypePropertyIds { get; set; } = new();
        public List<SelectListItem> WorkOrderTypePropertyOptions { get; set; } = new();
        public List<int> SelectedWorkOrderDepartmentIds { get; set; } = new();
        public List<SelectListItem> WorkOrderDepartmentOptions { get; set; } = new();
        public List<string> SelectedWorkOrderStatuses { get; set; } = new();
        public List<SelectListItem> WorkOrderStatusOptions { get; set; } = new();

        public bool ShowCalendarFilters { get; set; }
        public List<int> SelectedCalendarCategoryIds { get; set; } = new();
        public List<SelectListItem> CalendarCategoryOptions { get; set; } = new();
        public List<string> SelectedCalendarRecurrenceValues { get; set; } = new();
        public List<SelectListItem> CalendarRecurrenceOptions { get; set; } = new();

        public bool ShowPassOnLogFilters { get; set; }
        public List<string> SelectedPassOnLogCreatorIds { get; set; } = new();
        public List<SelectListItem> PassOnLogCreatorOptions { get; set; } = new();

        public bool ShowPhonebookFilters { get; set; }
        public List<int> SelectedPhonebookTypeIds { get; set; } = new();
        public List<SelectListItem> PhonebookTypeOptions { get; set; } = new();
    }

    public class ReportMetadataViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

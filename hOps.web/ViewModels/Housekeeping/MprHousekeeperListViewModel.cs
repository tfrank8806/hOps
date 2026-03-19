using System;
using System.Collections.Generic;

namespace hOps.web.ViewModels.Housekeeping
{
    public class MprHousekeeperListViewModel
    {
        public List<HousekeeperOptionViewModel> Housekeepers { get; set; } = new();
        public bool CanManageHousekeepers { get; set; }
        public bool HasPropertySelected { get; set; }
        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FilterPeriod { get; set; }
        public int? FilterMonth { get; set; }
        public int? FilterYear { get; set; }
        public DateTime? FilterStart { get; set; }
        public DateTime? FilterEnd { get; set; }
        public string? FilterStartString => FilterStart?.ToString("yyyy-MM-dd");
        public string? FilterEndString => FilterEnd?.ToString("yyyy-MM-dd");
    }
}

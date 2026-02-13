#nullable enable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;
using hOps.web.ViewModels.PreventiveMaintenance;

namespace hOps.web.ViewModels.Maintenance
{
    public class MaintenancePmChecklistsPageViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public IReadOnlyList<Property> AccessibleProperties { get; set; } = new List<Property>();
        public IReadOnlyList<MaintenancePmChecklistSummaryViewModel> Checklists { get; set; } = new List<MaintenancePmChecklistSummaryViewModel>();
        public MaintenancePmChecklistEditorViewModel ChecklistEditor { get; set; } = new();
    }

    public class MaintenancePmChecklistSummaryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PreventiveMaintenanceChecklistType ChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;
        public bool IsActive { get; set; }
        public int TaskCount { get; set; }
        public int SessionCount { get; set; }
        public IReadOnlyList<string> AreaOptions { get; set; } = new List<string>();
    }

    public class MaintenancePmChecklistEditorViewModel
    {
        public int? Id { get; set; }
        public int PropertyId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Checklist Type")]
        public PreventiveMaintenanceChecklistType ChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;

        public bool IsActive { get; set; } = true;

        [Display(Name = "Suggested Areas")]
        public string? AreaOptionsText { get; set; }

        public bool CanChangeType { get; set; } = true;
        public bool HasExistingSessions { get; set; }
    }

    public class MaintenancePmChecklistTasksViewModel
    {
        public int ChecklistId { get; set; }
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string ChecklistName { get; set; } = string.Empty;
        public PreventiveMaintenanceChecklistType ChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;
        public IReadOnlyList<PmSetupTaskRow> Tasks { get; set; } = new List<PmSetupTaskRow>();
        public IReadOnlyList<string> AreaOptions { get; set; } = new List<string>();
    }

    public class MaintenancePmChecklistSaveRequest
    {
        public int? Id { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public PreventiveMaintenanceChecklistType ChecklistType { get; set; } = PreventiveMaintenanceChecklistType.Room;

        public bool IsActive { get; set; } = true;

        public string? AreaOptionsText { get; set; }
    }
}

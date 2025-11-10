using System;
using System.Collections.Generic;

namespace hOps.web.ViewModels.WorkOrders
{
    public class DepartmentWorkOrderTaskViewModel
    {
        public int WorkOrderId { get; set; }
        public string Issue { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string? PropertyName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public bool HasDueDate { get; set; }
        public string? Location { get; set; }
    }

    public class UserToDoItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    public class ToDoSidebarViewModel
    {
        public List<DepartmentWorkOrderTaskViewModel> DepartmentTasks { get; set; } = new();
        public List<UserToDoItemViewModel> ActivePersonalToDos { get; set; } = new();
        public List<UserToDoItemViewModel> CompletedPersonalToDos { get; set; } = new();
        public bool HasDepartmentAssignments { get; set; }
        public bool HasItems => DepartmentTasks.Count > 0 || ActivePersonalToDos.Count > 0 || CompletedPersonalToDos.Count > 0;
    }
}

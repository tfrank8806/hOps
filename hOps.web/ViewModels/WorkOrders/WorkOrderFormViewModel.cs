using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace hOps.web.ViewModels.WorkOrders
{
    public class WorkOrderFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Location")]
        public string? Location { get; set; }

        [Display(Name = "Work Order Type")]
        public int? WorkOrderTypeId { get; set; }

        [Required]
        [Display(Name = "Issue")]
        public string Issue { get; set; } = string.Empty;

        [Display(Name = "Details")]
        public string? Details { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.UtcNow.Date.AddDays(1);

        [Display(Name = "Assign To")]
        public int? DepartmentId { get; set; }

        [Display(Name = "Photo Upload")]
        public List<IFormFile>? Photos { get; set; }

        [Display(Name = "Properties")]
        public List<int> SelectedPropertyIds { get; set; } = new();
    }
}

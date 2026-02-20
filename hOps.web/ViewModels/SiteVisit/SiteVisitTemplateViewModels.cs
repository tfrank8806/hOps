using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace hOps.web.ViewModels.SiteVisit
{
    public class SiteVisitTemplateSummaryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ItemCount { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string? CreatedByName { get; set; }
    }

    public class SiteVisitTemplateUploadViewModel
    {
        [Required]
        [StringLength(200)]
        [Display(Name = "Template name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description (optional)")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Template file (.csv)")]
        public IFormFile? CsvFile { get; set; }
    }

    public class SiteVisitTemplateManagerViewModel
    {
        public List<SiteVisitTemplateSummaryViewModel> Templates { get; set; } = new();
        public SiteVisitTemplateUploadViewModel Upload { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

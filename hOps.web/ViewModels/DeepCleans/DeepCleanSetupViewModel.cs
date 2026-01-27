#nullable enable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using hOps.web.Models;

namespace hOps.web.ViewModels.DeepCleans
{
    public class DeepCleanSetupViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;

        [Range(1, 52)]
        public int FrequencyPerYear { get; set; } = 1;

        public List<DeepCleanSetupTaskRow> Tasks { get; set; } = new();
        public List<Property> AccessibleProperties { get; set; } = new();
    }

    public class DeepCleanSetupTaskRow
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Task { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
    }
}

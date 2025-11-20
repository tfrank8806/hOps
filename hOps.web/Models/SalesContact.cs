using System;
using System.ComponentModel.DataAnnotations;

#nullable enable

namespace hOps.web.Models
{
    public class SalesContact
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        public int PropertyId { get; set; }
        public Property Property { get; set; } = default!;

        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<SalesLeadSubmission> SalesLeads { get; set; } = new List<SalesLeadSubmission>();
    }
}

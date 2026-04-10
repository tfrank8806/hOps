using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public class MasterEmployee
    {
        public int Id { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Position { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}

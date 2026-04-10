using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.Models
{
    public enum AttendanceRecordType
    {
        Tardy = 0,

        [Display(Name = "Left Early")]
        LeftEarly = 1,

        [Display(Name = "Call Off")]
        CallOff = 2,

        [Display(Name = "No Call/No Show")]
        NoCallNoShow = 3,

        Sick = 4,
        Vacation = 5,
        Personal = 6,
        Bereavement = 7
    }

    public class AttendanceRecord
    {
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;

        [Required]
        public int MasterEmployeeId { get; set; }
        public MasterEmployee MasterEmployee { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateTime AttendanceDate { get; set; }

        [Required]
        public AttendanceRecordType AttendanceType { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;
        public ApplicationUser CreatedByUser { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedByUser { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}

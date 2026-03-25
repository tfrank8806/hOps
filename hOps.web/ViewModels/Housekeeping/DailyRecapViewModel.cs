using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Housekeeping
{
    public class DailyRecapViewModel
    {
        private const int DefaultOutOfOrderRows = 5;
        private const int DefaultRoomExceptionsRows = 5;
        private const int DefaultMaintenanceRows = 5;
        private const int DefaultInspectionRows = 5;

        [Required(ErrorMessage = "Report date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Report Date")]
        public DateTime? ReportDate { get; set; }

        [Required(ErrorMessage = "Housekeeping manager or supervisor is required.")]
        [Display(Name = "Housekeeping Manager/Supervisor")]
        public string ManagerName { get; set; } = string.Empty;

        [Display(Name = "Occupancy %")]
        public string? OccupancyPercent { get; set; }

        [Display(Name = "Check-outs")]
        public string? CheckOuts { get; set; }

        [Display(Name = "Stayovers")]
        public string? Stayovers { get; set; }

        [Display(Name = "Rooms OOO (Start)")]
        public string? RoomsOutOfOrderStart { get; set; }

        [Display(Name = "Vacant Clean")]
        public string? VacantClean { get; set; }

        [Display(Name = "Vacant Dirty")]
        public string? VacantDirty { get; set; }

        [Display(Name = "Deep Cleans Completed")]
        public string? DeepCleansCompleted { get; set; }

        [Display(Name = "Rooms OOO (End)")]
        public string? RoomsOutOfOrderEnd { get; set; }

        public List<DailyRecapStaffingRow> Staffing { get; set; } = new();
        public List<DailyRecapOutOfOrderRoomRow> OutOfOrderRooms { get; set; } = new();
        public List<DailyRecapRoomNotCleanedRow> RoomsNotCleaned { get; set; } = new();
        public List<DailyRecapMaintenanceIssueRow> MaintenanceIssues { get; set; } = new();
        public List<DailyRecapInspectionFailureRow> InspectionFailures { get; set; } = new();
        public List<DailyRecapPublicAreaRow> PublicAreas { get; set; } = new();

        [Display(Name = "Public Areas Attendant")]
        public string? PublicAreasAttendant { get; set; }

        [Display(Name = "Laundry Attendant")]
        public string? LaundryAttendant { get; set; }

        [Display(Name = "Top performers / recognition")]
        public string? PerformanceHighlights { get; set; }

        [Display(Name = "Coaching needed")]
        public string? PerformanceCoaching { get; set; }

        [Display(Name = "Main operational challenges")]
        public string? OperationalChallenges { get; set; }

        [Display(Name = "Plan for tomorrow")]
        public string? TomorrowPlan { get; set; }

        [Display(Name = "Additional Notes")]
        public string? AdditionalNotes { get; set; }

        public static DailyRecapViewModel CreateDefault(DateTime defaultDate)
        {
            var model = new DailyRecapViewModel
            {
                ReportDate = defaultDate,
                Staffing = new List<DailyRecapStaffingRow>
                {
                    new DailyRecapStaffingRow { Area = "Room Attendants" },
                    new DailyRecapStaffingRow { Area = "Houseperson" },
                    new DailyRecapStaffingRow { Area = "Laundry" }
                },
                OutOfOrderRooms = CreateRows(() => new DailyRecapOutOfOrderRoomRow(), DefaultOutOfOrderRows),
                RoomsNotCleaned = CreateRows(() => new DailyRecapRoomNotCleanedRow(), DefaultRoomExceptionsRows),
                MaintenanceIssues = CreateRows(() => new DailyRecapMaintenanceIssueRow(), DefaultMaintenanceRows),
                InspectionFailures = CreateRows(() => new DailyRecapInspectionFailureRow(), DefaultInspectionRows),
                PublicAreas = new List<DailyRecapPublicAreaRow>
                {
                    new DailyRecapPublicAreaRow { Area = "Lobby" },
                    new DailyRecapPublicAreaRow { Area = "Restrooms" },
                    new DailyRecapPublicAreaRow { Area = "Hallways / Elevators" },
                    new DailyRecapPublicAreaRow { Area = "Parking Lot" },
                    new DailyRecapPublicAreaRow { Area = "Stripped Rooms" }
                }
            };

            return model;
        }

        public void EnsureCollectionIntegrity()
        {
            Staffing ??= new List<DailyRecapStaffingRow>();
            OutOfOrderRooms ??= new List<DailyRecapOutOfOrderRoomRow>();
            RoomsNotCleaned ??= new List<DailyRecapRoomNotCleanedRow>();
            MaintenanceIssues ??= new List<DailyRecapMaintenanceIssueRow>();
            InspectionFailures ??= new List<DailyRecapInspectionFailureRow>();
            PublicAreas ??= new List<DailyRecapPublicAreaRow>();
        }

        private static List<T> CreateRows<T>(Func<T> factory, int count)
        {
            var rows = new List<T>(count);
            for (var i = 0; i < count; i++)
            {
                rows.Add(factory());
            }

            return rows;
        }
    }

    public class DailyRecapStaffingRow
    {
        public string Area { get; set; } = string.Empty;
        public string? Scheduled { get; set; }
        public string? CallOffs { get; set; }
        public string? Tardies { get; set; }
        public string? Notes { get; set; }
    }

    public class DailyRecapOutOfOrderRoomRow
    {
        [Display(Name = "Room Number")]
        public string? RoomNumber { get; set; }

        [Display(Name = "OOO / OOS")]
        public string? Status { get; set; }

        public string? Issue { get; set; }

        [Display(Name = "Clean or Dirty")]
        public string? CleanStatus { get; set; }

        [Display(Name = "If dirty, why left dirty?")]
        public string? ReasonLeftDirty { get; set; }
    }

    public class DailyRecapRoomNotCleanedRow
    {
        [Display(Name = "Room Number")]
        public string? RoomNumber { get; set; }

        [Display(Name = "Room Status")]
        public string? Status { get; set; }

        [Display(Name = "Reason Not Cleaned")]
        public string? Reason { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [Display(Name = "Action Plan / Next Step")]
        public string? ActionPlan { get; set; }
    }

    public class DailyRecapMaintenanceIssueRow
    {
        [Display(Name = "Room / Area")]
        public string? Area { get; set; }

        [Display(Name = "Issue Found")]
        public string? Issue { get; set; }

        [Display(Name = "Work Order Submitted?")]
        public string? WorkOrderSubmitted { get; set; }

        [Display(Name = "Room Status")]
        public string? RoomStatus { get; set; }

        public string? Notes { get; set; }
    }

    public class DailyRecapInspectionFailureRow
    {
        [Display(Name = "Room / Area")]
        public string? Area { get; set; }

        public string? Issue { get; set; }

        [Display(Name = "Associate Responsible")]
        public string? ResponsibleAssociate { get; set; }

        [Display(Name = "Coaching Given?")]
        public string? CoachingGiven { get; set; }

        public string? Notes { get; set; }
    }

    public class DailyRecapPublicAreaRow
    {
        [Display(Name = "Area / Item")]
        public string Area { get; set; } = string.Empty;

        [Display(Name = "Completed?")]
        public string? Completed { get; set; }

        [Display(Name = "Issue / Shortage")]
        public string? Issues { get; set; }

        [Display(Name = "Items Need Order")]
        public string? ItemsToOrder { get; set; }

        public string? Notes { get; set; }
    }
}

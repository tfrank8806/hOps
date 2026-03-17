using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Housekeeping
{
    public class MprTrackerViewModel
    {
        public const int DefaultDepartureMinutes = 30;
        public const int DefaultLinenChangeMinutes = 20;
        public const int DefaultStayoverMinutes = 15;

        [Display(Name = "Housekeeper")]
        public int? SelectedHousekeeperId { get; set; }

        [Display(Name = "Entry date")]
        [DataType(DataType.Date)]
        public DateTime EntryDate { get; set; } = DateTime.Today;

        [Display(Name = "Checkout rooms cleaned")]
        [Range(0, 500, ErrorMessage = "Enter a value of 0 or greater.")]
        public int CheckoutRooms { get; set; }

        [Display(Name = "Linen change rooms cleaned")]
        [Range(0, 500, ErrorMessage = "Enter a value of 0 or greater.")]
        public int LinenChangeRooms { get; set; }

        [Display(Name = "Stayover rooms cleaned")]
        [Range(0, 500, ErrorMessage = "Enter a value of 0 or greater.")]
        public int StayoverRooms { get; set; }

        [Display(Name = "DND / No service rooms")]
        [Range(0, 500, ErrorMessage = "Enter a value of 0 or greater.")]
        public int DndRooms { get; set; }

        [Display(Name = "Total hours worked")]
        [Range(0, 24, ErrorMessage = "Enter the total hours worked between 0 and 24.")]
        public decimal HoursWorked { get; set; }

        [Display(Name = "Checkout target (minutes)")]
        [Range(1, 180, ErrorMessage = "Use a value between 1 and 180 minutes.")]
        public decimal DepartureStandardMinutes { get; set; } = DefaultDepartureMinutes;

        [Display(Name = "Linen change target (minutes)")]
        [Range(1, 180, ErrorMessage = "Use a value between 1 and 180 minutes.")]
        public decimal LinenChangeStandardMinutes { get; set; } = DefaultLinenChangeMinutes;

        [Display(Name = "Stayover target (minutes)")]
        [Range(1, 180, ErrorMessage = "Use a value between 1 and 180 minutes.")]
        public decimal StayoverStandardMinutes { get; set; } = DefaultStayoverMinutes;

        public bool CanEditStandards { get; set; }
        public bool CanManageHousekeepers { get; set; }
        public bool HasResults { get; private set; }
        public bool EntrySaved { get; set; }
        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public List<HousekeeperOptionViewModel> Housekeepers { get; set; } = new();
        public MprTrackerLogFilterViewModel LogFilter { get; set; } = new();
        public List<DateTime> LogDates { get; set; } = new();
        public List<MprTrackerLogRowViewModel> LogRows { get; set; } = new();

        public decimal TotalMinutesWorked => Math.Round(HoursWorked * 60m, 2, MidpointRounding.AwayFromZero);

        public int TotalRoomsCleaned => CheckoutRooms + LinenChangeRooms + StayoverRooms;

        public int TotalRoomsTrackedForMpr => CheckoutRooms + StayoverRooms;

        public decimal? MinutesPerRoom { get; private set; }

        public decimal DepartureGuidelineTotalMinutes => CheckoutRooms * DepartureStandardMinutes;

        public decimal LinenChangeGuidelineTotalMinutes => LinenChangeRooms * LinenChangeStandardMinutes;

        public decimal StayoverGuidelineTotalMinutes => StayoverRooms * StayoverStandardMinutes;

        public decimal TotalGuidelineMinutes => DepartureGuidelineTotalMinutes
                                                + LinenChangeGuidelineTotalMinutes
                                                + StayoverGuidelineTotalMinutes;

        public decimal VarianceFromGuideline => TotalMinutesWorked - TotalGuidelineMinutes;

        public void Calculate()
        {
            HasResults = true;
            NormalizeStandards();
            var roomsForMpr = TotalRoomsTrackedForMpr;

            if (roomsForMpr > 0 && HoursWorked > 0)
            {
                MinutesPerRoom = Math.Round((HoursWorked * 60m) / roomsForMpr, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                MinutesPerRoom = null;
            }
        }

        public void ResetStandardsToDefaults()
        {
            DepartureStandardMinutes = DefaultDepartureMinutes;
            LinenChangeStandardMinutes = DefaultLinenChangeMinutes;
            StayoverStandardMinutes = DefaultStayoverMinutes;
        }

        private void NormalizeStandards()
        {
            if (DepartureStandardMinutes <= 0)
            {
                DepartureStandardMinutes = DefaultDepartureMinutes;
            }

            if (LinenChangeStandardMinutes <= 0)
            {
                LinenChangeStandardMinutes = DefaultLinenChangeMinutes;
            }

            if (StayoverStandardMinutes <= 0)
            {
                StayoverStandardMinutes = DefaultStayoverMinutes;
            }
        }
    }

    public class HousekeeperOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }

    public class MprTrackerLogFilterViewModel
    {
        public const string PeriodMonth = "Month";
        public const string PeriodYearToDate = "YearToDate";
        public const string PeriodCustom = "Custom";

        public string PeriodType { get; set; } = PeriodMonth;
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CustomStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CustomEndDate { get; set; }

        public (DateTime Start, DateTime End) GetDateRange(DateTime referenceDate)
        {
            var safeReference = referenceDate.Date;
            var year = SelectedYear <= 0 ? safeReference.Year : SelectedYear;

            switch (PeriodType)
            {
                case PeriodYearToDate:
                    var ytdStart = new DateTime(year, 1, 1);
                    var ytdEnd = year == safeReference.Year
                        ? safeReference
                        : new DateTime(year, 12, 31);
                    return (ytdStart, ytdEnd);
                case PeriodCustom:
                    if (CustomStartDate.HasValue && CustomEndDate.HasValue && CustomEndDate.Value.Date >= CustomStartDate.Value.Date)
                    {
                        return (CustomStartDate.Value.Date, CustomEndDate.Value.Date);
                    }
                    break;
            }

            var month = SelectedMonth is >= 1 and <= 12 ? SelectedMonth : safeReference.Month;
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            return (start, end);
        }
    }

    public class MprTrackerLogRowViewModel
    {
        public int? HousekeeperId { get; set; }
        public string HousekeeperName { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public MprTrackerLogSummaryViewModel Summary { get; set; } = new();
        public Dictionary<DateTime, MprTrackerLogCellViewModel> Cells { get; set; } = new();
    }

    public class MprTrackerLogSummaryViewModel
    {
        public int CheckoutRooms { get; set; }
        public int LinenChangeRooms { get; set; }
        public int StayoverRooms { get; set; }
        public int DndRooms { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal TotalMinutesWorked { get; set; }
        public decimal? MinutesPerRoom { get; set; }
        public int TotalRoomsTracked => CheckoutRooms + StayoverRooms;
    }

    public class MprTrackerLogCellViewModel
    {
        public int CheckoutRooms { get; set; }
        public int LinenChangeRooms { get; set; }
        public int StayoverRooms { get; set; }
        public int DndRooms { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal TotalMinutesWorked { get; set; }
        public decimal? MinutesPerRoom { get; set; }

        public void RecalculateMinutesPerRoom()
        {
            var roomsTracked = CheckoutRooms + StayoverRooms;
            MinutesPerRoom = roomsTracked > 0 && HoursWorked > 0
                ? Math.Round((HoursWorked * 60m) / roomsTracked, 2, MidpointRounding.AwayFromZero)
                : null;
        }
    }
}

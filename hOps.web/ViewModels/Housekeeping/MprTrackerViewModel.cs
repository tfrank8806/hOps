using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Housekeeping
{
    public class MprTrackerViewModel
    {
        public const int DefaultDepartureMinutes = 30;
        public const int DefaultLinenChangeMinutes = 20;
        public const int DefaultStayoverMinutes = 15;

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

        public bool HasResults { get; private set; }

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
}

using System;
using System.ComponentModel.DataAnnotations;

namespace hOps.web.ViewModels.Housekeeping
{
    public class MprTrackerViewModel
    {
        private const int DepartureGuidelineMinutes = 30;
        private const int LinenChangeGuidelineMinutes = 20;
        private const int StayoverGuidelineMinutes = 15;

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

        public bool HasResults { get; private set; }

        public decimal TotalMinutesWorked => Math.Round(HoursWorked * 60m, 2, MidpointRounding.AwayFromZero);

        public int TotalRoomsCleaned => CheckoutRooms + LinenChangeRooms + StayoverRooms;

        public int TotalRoomsTrackedForMpr => CheckoutRooms + StayoverRooms;

        public decimal? MinutesPerRoom { get; private set; }

        public decimal DepartureGuidelineTotalMinutes => CheckoutRooms * DepartureGuidelineMinutes;

        public decimal LinenChangeGuidelineTotalMinutes => LinenChangeRooms * LinenChangeGuidelineMinutes;

        public decimal StayoverGuidelineTotalMinutes => StayoverRooms * StayoverGuidelineMinutes;

        public decimal TotalGuidelineMinutes => DepartureGuidelineTotalMinutes
                                                + LinenChangeGuidelineTotalMinutes
                                                + StayoverGuidelineTotalMinutes;

        public decimal VarianceFromGuideline => TotalMinutesWorked - TotalGuidelineMinutes;

        public void Calculate()
        {
            HasResults = true;
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
    }
}

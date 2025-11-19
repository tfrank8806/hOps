using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace hOps.web.ViewModels.Sales
{
    public class SalesLeadFormViewModel
    {
        public const string GroupRoomsInquiryKey = "group_rooms";
        public const string CorporateRateInquiryKey = "corporate_rate";
        public const string MeetingRoomInquiryKey = "meeting_room";
        public const string OtherInquiryKey = "other";

        public static IReadOnlyList<SalesInquiryOption> InquiryOptions { get; } =
            new[]
            {
                new SalesInquiryOption(GroupRoomsInquiryKey, "Group Rooms", "Room blocks for 10+ rooms."),
                new SalesInquiryOption(CorporateRateInquiryKey, "Corporate Rate", "Negotiated company rate."),
                new SalesInquiryOption(MeetingRoomInquiryKey, "Meeting Room", "Meeting, conference, or event space."),
                new SalesInquiryOption(OtherInquiryKey, "Other", "Tell us more about the request.")
            };

        [Required]
        [Display(Name = "Sales Contact")]
        public int? SalesContactId { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Name of Group / Company")]
        public string GroupName { get; set; } = string.Empty;

        [Required]
        [StringLength(160)]
        [Display(Name = "Contact Name")]
        public string ContactName { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Contact Phone Number")]
        [DataType(DataType.PhoneNumber)]
        public string? ContactPhone { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        [Display(Name = "Contact Email Address")]
        public string ContactEmail { get; set; } = string.Empty;

        [Display(Name = "Type of inquiry")]
        public List<string> InquiryTypes { get; set; } = new();

        [StringLength(500)]
        [Display(Name = "If Other, please describe")]
        public string? InquiryOtherDetails { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Event Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Event End Date")]
        public DateTime? EndDate { get; set; }

        [Range(typeof(decimal), "0", "1000000000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Budget Minimum")]
        public decimal? BudgetMinimum { get; set; }

        [Range(typeof(decimal), "0", "1000000000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Budget Maximum")]
        public decimal? BudgetMaximum { get; set; }

        [StringLength(4000)]
        [Display(Name = "Additional Details")]
        public string? AdditionalDetails { get; set; }

        public static bool IsValidInquiryKey(string key) =>
            !string.IsNullOrWhiteSpace(key) &&
            InquiryOptions.Any(option => option.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        public static string GetInquiryLabel(string key)
        {
            var option = InquiryOptions.FirstOrDefault(o =>
                o.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            return option?.Label ?? key;
        }
    }

    public sealed record SalesInquiryOption(string Key, string Label, string Description);
}

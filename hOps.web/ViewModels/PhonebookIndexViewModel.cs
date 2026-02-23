using hOps.web.Models;
using System.Collections.Generic;

namespace hOps.web.ViewModels
{
    public class PhonebookIndexViewModel
    {
        public List<PhonebookContact> Contacts { get; set; } = new();

        public List<PhonebookType> Types { get; set; } = new();

        public string? SearchTerm { get; set; }

        public int? SelectedTypeId { get; set; }

        public string SortOption { get; set; } = PhonebookSortOptions.LastName;
    }

    public static class PhonebookSortOptions
    {
        public const string LastName = "lastName";
        public const string FirstName = "firstName";
        public const string Company = "company";
    }
}

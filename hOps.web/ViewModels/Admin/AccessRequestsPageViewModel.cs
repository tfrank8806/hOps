using System.Collections.Generic;
using hOps.web.Models;

namespace hOps.web.ViewModels.Admin
{
    public class AccessRequestsPageViewModel
    {
        public IReadOnlyList<UserAccessRequest> Requests { get; set; } = new List<UserAccessRequest>();
        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public int TotalCount { get; set; }

        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public int PreviousPage => HasPreviousPage ? PageNumber - 1 : 1;
        public int NextPage => HasNextPage ? PageNumber + 1 : TotalPages;
    }
}

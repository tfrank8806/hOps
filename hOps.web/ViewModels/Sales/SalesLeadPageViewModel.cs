using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hOps.web.ViewModels.Sales
{
    public class SalesLeadPageViewModel
    {
        public SalesLeadFormViewModel Form { get; set; } = new();
        public List<SelectListItem> SalesContactOptions { get; set; } = new();

        public int? CurrentPropertyId { get; set; }
        public string? CurrentPropertyName { get; set; }
        public string? CurrentPropertyCode { get; set; }

        public bool SubmittedSuccessfully { get; set; }

        public bool HasCurrentProperty => CurrentPropertyId.HasValue;
        public bool HasSalesContacts => SalesContactOptions.Count > 0;
    }
}

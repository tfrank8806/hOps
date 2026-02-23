using hOps.web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace hOps.web.ViewModels
{
    public class PhonebookContactFormViewModel
    {
        public PhonebookContact Contact { get; set; } = new();

        public List<PhonebookType> Types { get; set; } = new();

        public bool IsEdit { get; set; }

        public string Title => IsEdit ? "Edit Contact" : "Add Contact";

        public IEnumerable<SelectListItem> TypeOptions =>
            Types
                .OrderBy(t => t.Name)
                .Select(type => new SelectListItem
                {
                    Value = type.Id.ToString(),
                    Text = type.Name,
                    Selected = Contact?.PhonebookTypeId == type.Id
                })
                .Prepend(new SelectListItem
                {
                    Value = string.Empty,
                    Text = "Select a type",
                    Disabled = true,
                    Selected = Contact?.PhonebookTypeId == null
                });
    }
}

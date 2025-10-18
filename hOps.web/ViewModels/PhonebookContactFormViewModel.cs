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

        public IEnumerable<SelectListItem> TypeOptions
        {
            get
            {
                var options = Types
                    .Select(type => new SelectListItem
                    {
                        Value = type.Name,
                        Text = type.Name,
                        Selected = Contact?.TypeName != null &&
                            string.Equals(Contact.TypeName, type.Name, StringComparison.OrdinalIgnoreCase)
                    })
                    .ToList();

                var hasSelection = options.Any(option => option.Selected);

                options.Insert(0, new SelectListItem
                {
                    Value = string.Empty,
                    Text = "Select a type",
                    Disabled = true,
                    Selected = !hasSelection
                });

                return options;
            }
        }
    }
}

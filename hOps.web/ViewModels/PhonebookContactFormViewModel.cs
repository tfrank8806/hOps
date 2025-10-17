using hOps.web.Models;
using System.Collections.Generic;

namespace hOps.web.ViewModels
{
    public class PhonebookContactFormViewModel
    {
        public PhonebookContact Contact { get; set; } = new();

        public List<PhonebookType> Types { get; set; } = new();

        public bool IsEdit { get; set; }

        public string Title => IsEdit ? "Edit Contact" : "Add Contact";
    }
}

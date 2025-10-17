using hOps.web.Models;
using System.Collections.Generic;

namespace hOps.web.ViewModels
{
    public class PhonebookIndexViewModel
    {
        public List<PhonebookContact> Contacts { get; set; } = new();

        public List<PhonebookType> Types { get; set; } = new();
    }
}

using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace hOps.web.Controllers
{
    [Authorize]
    public class PhonebookController : Controller
    {
        private readonly ApplicationDbContext _db;

        public PhonebookController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var contacts = await _db.PhonebookContacts
                .Include(c => c.PhonebookType)
                .OrderBy(c => c.TypeName)
                .ThenBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ThenBy(c => c.Company)
                .ToListAsync();

            var types = await _db.PhonebookTypes
                .OrderBy(t => t.Name)
                .ToListAsync();

            var vm = new PhonebookIndexViewModel
            {
                Contacts = contacts,
                Types = types
            };

            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            var vm = new PhonebookContactFormViewModel
            {
                Types = await LoadTypesAsync(),
                IsEdit = false
            };

            return View("Form", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PhonebookContactFormViewModel vm)
        {
            await ValidateTypeAsync(vm.Contact);

            if (!ModelState.IsValid)
            {
                vm.Types = await LoadTypesAsync();
                vm.IsEdit = false;
                return View("Form", vm);
            }

            _db.PhonebookContacts.Add(vm.Contact);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var contact = await _db.PhonebookContacts.FindAsync(id);
            if (contact == null)
            {
                return NotFound();
            }

            var vm = new PhonebookContactFormViewModel
            {
                Contact = contact,
                Types = await LoadTypesAsync(),
                IsEdit = true
            };

            return View("Form", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PhonebookContactFormViewModel vm)
        {
            if (id != vm.Contact.Id)
            {
                return NotFound();
            }

            await ValidateTypeAsync(vm.Contact);

            if (!ModelState.IsValid)
            {
                vm.Types = await LoadTypesAsync();
                vm.IsEdit = true;
                return View("Form", vm);
            }

            var existing = await _db.PhonebookContacts.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.TypeName = vm.Contact.TypeName;
            existing.PhonebookTypeId = vm.Contact.PhonebookTypeId;
            existing.FirstName = vm.Contact.FirstName;
            existing.LastName = vm.Contact.LastName;
            existing.Company = vm.Contact.Company;
            existing.Title = vm.Contact.Title;
            existing.PhoneNumber = vm.Contact.PhoneNumber;
            existing.Email = vm.Contact.Email;
            existing.Fax = vm.Contact.Fax;
            existing.Website = vm.Contact.Website;
            existing.Address = vm.Contact.Address;
            existing.Notes = vm.Contact.Notes;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<PhonebookType>> LoadTypesAsync()
        {
            return await _db.PhonebookTypes
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        private async Task ValidateTypeAsync(PhonebookContact contact)
        {
            contact.TypeName = contact.TypeName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(contact.TypeName))
            {
                ModelState.AddModelError("Contact.TypeName", "Type is required.");
                contact.PhonebookTypeId = null;
                return;
            }

            var normalizedType = contact.TypeName.ToLowerInvariant();
            var match = await _db.PhonebookTypes
                .FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedType);

            if (match != null)
            {
                contact.PhonebookTypeId = match.Id;
                contact.TypeName = match.Name;
            }
            else
            {
                contact.PhonebookTypeId = null;
            }
        }
    }
}

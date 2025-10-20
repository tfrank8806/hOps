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

        public async Task<IActionResult> Index(string? search, int? typeId)
        {
            var contactsQuery = _db.PhonebookContacts
                .Include(c => c.PhonebookType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                var likeTerm = $"%{trimmedSearch}%";

                contactsQuery = contactsQuery.Where(c =>
                    (c.FirstName != null && EF.Functions.Like(c.FirstName, likeTerm)) ||
                    (c.LastName != null && EF.Functions.Like(c.LastName, likeTerm)) ||
                    (c.Company != null && EF.Functions.Like(c.Company, likeTerm)) ||
                    (c.Title != null && EF.Functions.Like(c.Title, likeTerm)) ||
                    (c.PhoneNumber != null && EF.Functions.Like(c.PhoneNumber, likeTerm)) ||
                    (c.Email != null && EF.Functions.Like(c.Email, likeTerm)) ||
                    (c.Address != null && EF.Functions.Like(c.Address, likeTerm)) ||
                    (c.Notes != null && EF.Functions.Like(c.Notes, likeTerm)) ||
                    (c.TypeName != null && EF.Functions.Like(c.TypeName, likeTerm))
                );
            }

            if (typeId.HasValue)
            {
                contactsQuery = contactsQuery.Where(c => c.PhonebookTypeId == typeId);
            }

            var contacts = await contactsQuery
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
                Types = types,
                SearchTerm = search,
                SelectedTypeId = typeId
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

            if (match == null)
            {
                ModelState.AddModelError("Contact.TypeName", "Selected type is no longer available.");
                contact.PhonebookTypeId = null;
                return;
            }

            contact.PhonebookTypeId = match.Id;
            contact.TypeName = match.Name;
        }
    }
}

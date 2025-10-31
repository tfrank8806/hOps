using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace hOps.web.Controllers
{
    [Authorize]
    public class PhonebookController : BaseController
    {
        public PhonebookController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        public async Task<IActionResult> Index(string? search, int? typeId)
        {
            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                return View(new PhonebookIndexViewModel
                {
                    SearchTerm = search,
                    SelectedTypeId = null
                });
            }

            var contactsQuery = _context.PhonebookContacts
                .Include(c => c.PhonebookType)
                .Where(c => c.PhonebookType != null && c.PhonebookType.PropertyId == currentProperty.Id)
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

            var types = await _context.PhonebookTypes
                .Where(t => t.PropertyId == currentProperty.Id)
                .OrderBy(t => t.Name)
                .ToListAsync();

            if (typeId.HasValue && types.Any(t => t.Id == typeId.Value))
            {
                contactsQuery = contactsQuery.Where(c => c.PhonebookTypeId == typeId);
            }
            else
            {
                typeId = null;
            }

            var contacts = await contactsQuery
                .AsNoTracking()
                .OrderBy(c => c.TypeName)
                .ThenBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ThenBy(c => c.Company)
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
            var propertyId = GetCurrentPropertyId();
            if (!propertyId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }

            var vm = new PhonebookContactFormViewModel
            {
                Types = await LoadTypesAsync(propertyId.Value),
                IsEdit = false
            };

            return View("Form", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PhonebookContactFormViewModel vm)
        {
            var propertyId = GetCurrentPropertyId();
            if (!propertyId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }

            await ValidateTypeAsync(vm.Contact, propertyId.Value);

            if (!ModelState.IsValid)
            {
                vm.Types = await LoadTypesAsync(propertyId.Value);
                vm.IsEdit = false;
                return View("Form", vm);
            }

            _context.PhonebookContacts.Add(vm.Contact);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var propertyId = GetCurrentPropertyId();
            if (!propertyId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }

            var contact = await _context.PhonebookContacts
                .Include(c => c.PhonebookType)
                .FirstOrDefaultAsync(c => c.Id == id && c.PhonebookType != null && c.PhonebookType.PropertyId == propertyId.Value);

            if (contact == null)
            {
                return NotFound();
            }

            var vm = new PhonebookContactFormViewModel
            {
                Contact = contact,
                Types = await LoadTypesAsync(propertyId.Value),
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

            var propertyId = GetCurrentPropertyId();
            if (!propertyId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }

            await ValidateTypeAsync(vm.Contact, propertyId.Value);

            if (!ModelState.IsValid)
            {
                vm.Types = await LoadTypesAsync(propertyId.Value);
                vm.IsEdit = true;
                return View("Form", vm);
            }

            var existing = await _context.PhonebookContacts
                .Include(c => c.PhonebookType)
                .FirstOrDefaultAsync(c => c.Id == id && c.PhonebookType != null && c.PhonebookType.PropertyId == propertyId.Value);
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

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<PhonebookType>> LoadTypesAsync(int propertyId)
        {
            return await _context.PhonebookTypes
                .Where(t => t.PropertyId == propertyId)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        private async Task ValidateTypeAsync(PhonebookContact contact, int propertyId)
        {
            contact.TypeName = contact.TypeName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(contact.TypeName))
            {
                ModelState.AddModelError("Contact.TypeName", "Type is required.");
                contact.PhonebookTypeId = null;
                return;
            }

            var normalizedType = contact.TypeName.ToLowerInvariant();
            var match = await _context.PhonebookTypes
                .FirstOrDefaultAsync(t => t.PropertyId == propertyId && t.Name.ToLower() == normalizedType);

            if (match == null)
            {
                ModelState.AddModelError("Contact.TypeName", "Selected type is no longer available.");
                contact.PhonebookTypeId = null;
                return;
            }

            contact.PhonebookTypeId = match.Id;
            contact.TypeName = match.Name;
        }

        private int? GetCurrentPropertyId()
        {
            return (ViewBag.CurrentProperty as Property)?.Id;
        }
    }
}

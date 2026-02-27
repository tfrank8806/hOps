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

        public async Task<IActionResult> Index(string? search, int? typeId, string? sort)
        {
            var sortOption = NormalizeSortOption(sort);
            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                return View(new PhonebookIndexViewModel
                {
                    SearchTerm = search,
                    SelectedTypeId = null,
                    SortOption = sortOption
                });
            }

            var contactsQuery = _context.PhonebookContacts
                .Include(c => c.PhonebookType)
                .Where(c => c.PhonebookType != null && c.PhonebookType.PropertyId == currentProperty.Id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLowerInvariant();
                var likeTerm = $"%{normalizedSearch}%";

                contactsQuery = contactsQuery.Where(c =>
                    (c.FirstName != null && EF.Functions.Like(c.FirstName.ToLower(), likeTerm)) ||
                    (c.LastName != null && EF.Functions.Like(c.LastName.ToLower(), likeTerm)) ||
                    (c.Company != null && EF.Functions.Like(c.Company.ToLower(), likeTerm)) ||
                    (c.Title != null && EF.Functions.Like(c.Title.ToLower(), likeTerm)) ||
                    (c.PhoneNumber != null && EF.Functions.Like(c.PhoneNumber.ToLower(), likeTerm)) ||
                    (c.PhoneNumberTypes != null && EF.Functions.Like(c.PhoneNumberTypes.ToLower(), likeTerm)) ||
                    (c.Email != null && EF.Functions.Like(c.Email.ToLower(), likeTerm)) ||
                    (c.Fax != null && EF.Functions.Like(c.Fax.ToLower(), likeTerm)) ||
                    (c.Website != null && EF.Functions.Like(c.Website.ToLower(), likeTerm)) ||
                    (c.Address != null && EF.Functions.Like(c.Address.ToLower(), likeTerm)) ||
                    (c.Notes != null && EF.Functions.Like(c.Notes.ToLower(), likeTerm)) ||
                    (c.TypeName != null && EF.Functions.Like(c.TypeName.ToLower(), likeTerm))
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

            IQueryable<PhonebookContact> orderedQuery = sortOption switch
            {
                PhonebookSortOptions.FirstName => contactsQuery
                    .OrderBy(c => c.FirstName ?? string.Empty)
                    .ThenBy(c => c.LastName ?? string.Empty)
                    .ThenBy(c => c.Company ?? string.Empty),
                PhonebookSortOptions.Company => contactsQuery
                    .OrderBy(c => c.Company ?? string.Empty)
                    .ThenBy(c => c.LastName ?? string.Empty)
                    .ThenBy(c => c.FirstName ?? string.Empty),
                _ => contactsQuery
                    .OrderBy(c => c.LastName ?? string.Empty)
                    .ThenBy(c => c.FirstName ?? string.Empty)
                    .ThenBy(c => c.Company ?? string.Empty)
            };

            var contacts = await orderedQuery
                .AsNoTracking()
                .ToListAsync();

            var vm = new PhonebookIndexViewModel
            {
                Contacts = contacts,
                Types = types,
                SearchTerm = search,
                SelectedTypeId = typeId,
                SortOption = sortOption
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
            existing.PhoneNumberTypes = vm.Contact.PhoneNumberTypes;
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
            if (!contact.PhonebookTypeId.HasValue)
            {
                ModelState.AddModelError("Contact.PhonebookTypeId", "Type is required.");
                contact.TypeName = string.Empty;
                return;
            }

            var match = await _context.PhonebookTypes
                .FirstOrDefaultAsync(t => t.Id == contact.PhonebookTypeId.Value && t.PropertyId == propertyId);

            if (match == null)
            {
                ModelState.AddModelError("Contact.PhonebookTypeId", "Selected type is no longer available.");
                contact.TypeName = string.Empty;
                contact.PhonebookTypeId = null;
                return;
            }

            contact.TypeName = match.Name;
        }

        private int? GetCurrentPropertyId()
        {
            return (ViewBag.CurrentProperty as Property)?.Id;
        }

        private static string NormalizeSortOption(string? sort)
        {
            return sort?.Trim().ToLowerInvariant() switch
            {
                PhonebookSortOptions.FirstName => PhonebookSortOptions.FirstName,
                PhonebookSortOptions.Company => PhonebookSortOptions.Company,
                _ => PhonebookSortOptions.LastName
            };
        }
    }
}

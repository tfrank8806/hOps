using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public SettingsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private async Task<List<Property>> GetEditablePropertiesAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return new List<Property>();
            }

            var roles = (await _userManager.GetRolesAsync(currentUser))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!)
                .ToList();

            if (roles.Contains("Admin"))
            {
                return await _db.Properties
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }

            var accessibleIds = await _db.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == currentUser.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();

            return await _db.Properties
                .Where(p => accessibleIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        private List<SelectListItem> BuildPropertySelectList(IEnumerable<Property> properties, int? selectedPropertyId, bool includeGlobal = true)
        {
            var items = new List<SelectListItem>();
            if (includeGlobal)
            {
                items.Add(new SelectListItem("Global (all properties)", "", selectedPropertyId == null));
            }

            foreach (var property in properties)
            {
                items.Add(new SelectListItem(
                    $"{property.Name} ({property.Code})",
                    property.Id.ToString(),
                    selectedPropertyId == property.Id));
            }

            return items;
        }

        private List<SelectListItem> BuildPropertyFilterOptions(string actionName, IEnumerable<Property> properties, int? propertyId, bool onlyGlobal)
        {
            var options = new List<SelectListItem>
            {
                new SelectListItem("All entries", Url.Action(actionName, new { }), !propertyId.HasValue && !onlyGlobal),
                new SelectListItem("Global (all properties)", Url.Action(actionName, new { onlyGlobal = true }), !propertyId.HasValue && onlyGlobal)
            };

            foreach (var property in properties)
            {
                options.Add(new SelectListItem(
                    $"{property.Name} ({property.Code})",
                    Url.Action(actionName, new { propertyId = property.Id }) ?? string.Empty,
                    propertyId == property.Id));
            }

            return options;
        }

        private IActionResult RedirectToSettingsList(string actionName, int? propertyId, bool onlyGlobal)
        {
            if (propertyId.HasValue && propertyId.Value > 0)
            {
                return RedirectToAction(actionName, new { propertyId = propertyId.Value });
            }

            if (onlyGlobal)
            {
                return RedirectToAction(actionName, new { onlyGlobal = true });
            }

            return RedirectToAction(actionName);
        }

        // — Departments CRUD —

        public async Task<IActionResult> Departments(int? propertyId = null, bool onlyGlobal = false)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (propertyId.HasValue && propertyId.Value > 0 && !editablePropertyIds.Contains(propertyId.Value))
            {
                return Forbid();
            }

            ViewBag.PropertyFilterOptions = BuildPropertyFilterOptions(nameof(Departments), properties, propertyId, onlyGlobal);
            ViewBag.SelectedPropertyId = propertyId;
            ViewBag.OnlyGlobal = onlyGlobal;

            var query = _db.Departments.Include(d => d.Property).AsNoTracking().AsQueryable();
            if (onlyGlobal)
            {
                query = query.Where(d => d.PropertyId == null);
            }
            else if (propertyId.HasValue && propertyId.Value > 0)
            {
                query = query.Where(d => d.PropertyId == propertyId.Value);
            }

            var departments = await query
                .OrderBy(d => d.Name)
                .ToListAsync();

            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
            ViewBag.CreateDepartmentUrl = propertyId.HasValue && propertyId.Value > 0
                ? Url.Action(nameof(CreateDepartment), new { propertyId = propertyId.Value, returnUrl = currentUrl })
                : (onlyGlobal
                    ? Url.Action(nameof(CreateDepartment), new { onlyGlobal = true, returnUrl = currentUrl })
                    : Url.Action(nameof(CreateDepartment), new { returnUrl = currentUrl }));

            return View(departments);
        }

        public async Task<IActionResult> CreateDepartment(int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, normalizedPropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(Departments))
                : returnUrl;

            var model = new Department
            {
                PropertyId = normalizedPropertyId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDepartment(Department model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(Departments))
                    : returnUrl;
                return View(model);
            }

            _db.Departments.Add(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(Departments), model.PropertyId, onlyGlobal);
        }

        public async Task<IActionResult> EditDepartment(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (dept.PropertyId.HasValue && dept.PropertyId.Value > 0 && !editablePropertyIds.Contains(dept.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, dept.PropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(Departments))
                : returnUrl;

            return View(dept);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDepartment(Department model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var existing = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == model.Id);
            if (existing == null)
            {
                return NotFound();
            }

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (existing.PropertyId.HasValue && existing.PropertyId.Value > 0 && !editablePropertyIds.Contains(existing.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(Departments))
                    : returnUrl;
                return View(model);
            }

            _db.Departments.Update(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(Departments), model.PropertyId, onlyGlobal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartment(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            if (dept.PropertyId.HasValue && dept.PropertyId.Value > 0 && !editablePropertyIds.Contains(dept.PropertyId.Value))
            {
                return Forbid();
            }

            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(Departments), propertyId, onlyGlobal);
        }

        // — WorkOrderTypes CRUD —

        public async Task<IActionResult> WorkOrderTypes(int? propertyId = null, bool onlyGlobal = false)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (propertyId.HasValue && propertyId.Value > 0 && !editablePropertyIds.Contains(propertyId.Value))
            {
                return Forbid();
            }

            ViewBag.PropertyFilterOptions = BuildPropertyFilterOptions(nameof(WorkOrderTypes), properties, propertyId, onlyGlobal);
            ViewBag.SelectedPropertyId = propertyId;
            ViewBag.OnlyGlobal = onlyGlobal;

            var query = _db.WorkOrderTypes.Include(t => t.Property).AsNoTracking().AsQueryable();
            if (onlyGlobal)
            {
                query = query.Where(t => t.PropertyId == null);
            }
            else if (propertyId.HasValue && propertyId.Value > 0)
            {
                query = query.Where(t => t.PropertyId == propertyId.Value);
            }

            var types = await query
                .OrderBy(t => t.Name)
                .ToListAsync();

            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
            ViewBag.CreateWorkOrderTypeUrl = propertyId.HasValue && propertyId.Value > 0
                ? Url.Action(nameof(CreateWorkOrderType), new { propertyId = propertyId.Value, returnUrl = currentUrl })
                : (onlyGlobal
                    ? Url.Action(nameof(CreateWorkOrderType), new { onlyGlobal = true, returnUrl = currentUrl })
                    : Url.Action(nameof(CreateWorkOrderType), new { returnUrl = currentUrl }));

            return View(types);
        }

        public async Task<IActionResult> CreateWorkOrderType(int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            ViewData["FormAction"] = nameof(CreateWorkOrderType);
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, normalizedPropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(WorkOrderTypes))
                : returnUrl;

            var model = new WorkOrderType
            {
                PropertyId = normalizedPropertyId
            };

            return View("WorkOrderTypeForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWorkOrderType(WorkOrderType model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewData["FormAction"] = nameof(CreateWorkOrderType);
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(WorkOrderTypes))
                    : returnUrl;
                return View("WorkOrderTypeForm", model);
            }

            _db.WorkOrderTypes.Add(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(WorkOrderTypes), model.PropertyId, onlyGlobal);
        }

        public async Task<IActionResult> EditWorkOrderType(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var item = await _db.WorkOrderTypes.FindAsync(id);
            if (item == null) return NotFound();
            ViewData["FormAction"] = nameof(EditWorkOrderType);

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (item.PropertyId.HasValue && item.PropertyId.Value > 0 && !editablePropertyIds.Contains(item.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, item.PropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(WorkOrderTypes))
                : returnUrl;

            return View("WorkOrderTypeForm", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWorkOrderType(WorkOrderType model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var existing = await _db.WorkOrderTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.Id);
            if (existing == null)
            {
                return NotFound();
            }

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (existing.PropertyId.HasValue && existing.PropertyId.Value > 0 && !editablePropertyIds.Contains(existing.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewData["FormAction"] = nameof(EditWorkOrderType);
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(WorkOrderTypes))
                    : returnUrl;
                return View("WorkOrderTypeForm", model);
            }

            _db.WorkOrderTypes.Update(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(WorkOrderTypes), model.PropertyId, onlyGlobal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkOrderType(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var item = await _db.WorkOrderTypes.FindAsync(id);
            if (item == null) return NotFound();

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            if (item.PropertyId.HasValue && item.PropertyId.Value > 0 && !editablePropertyIds.Contains(item.PropertyId.Value))
            {
                return Forbid();
            }

            _db.WorkOrderTypes.Remove(item);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(WorkOrderTypes), propertyId, onlyGlobal);
        }

        // — PhonebookTypes CRUD —

        public async Task<IActionResult> PhonebookTypes(int? propertyId = null, bool onlyGlobal = false)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (propertyId.HasValue && propertyId.Value > 0 && !editablePropertyIds.Contains(propertyId.Value))
            {
                return Forbid();
            }

            ViewBag.PropertyFilterOptions = BuildPropertyFilterOptions(nameof(PhonebookTypes), properties, propertyId, onlyGlobal);
            ViewBag.SelectedPropertyId = propertyId;
            ViewBag.OnlyGlobal = onlyGlobal;

            var query = _db.PhonebookTypes.Include(t => t.Property).AsNoTracking().AsQueryable();
            if (onlyGlobal)
            {
                query = query.Where(t => t.PropertyId == null);
            }
            else if (propertyId.HasValue && propertyId.Value > 0)
            {
                query = query.Where(t => t.PropertyId == propertyId.Value);
            }

            var list = await query
                .OrderBy(t => t.Name)
                .ToListAsync();

            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
            ViewBag.CreatePhonebookTypeUrl = propertyId.HasValue && propertyId.Value > 0
                ? Url.Action(nameof(CreatePhonebookType), new { propertyId = propertyId.Value, returnUrl = currentUrl })
                : (onlyGlobal
                    ? Url.Action(nameof(CreatePhonebookType), new { onlyGlobal = true, returnUrl = currentUrl })
                    : Url.Action(nameof(CreatePhonebookType), new { returnUrl = currentUrl }));

            return View(list);
        }

        public async Task<IActionResult> CreatePhonebookType(int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            ViewData["FormAction"] = nameof(CreatePhonebookType);
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, normalizedPropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(PhonebookTypes))
                : returnUrl;

            var model = new PhonebookType
            {
                PropertyId = normalizedPropertyId
            };

            return View("PhonebookTypeForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePhonebookType(PhonebookType model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewData["FormAction"] = nameof(CreatePhonebookType);
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(PhonebookTypes))
                    : returnUrl;
                return View("PhonebookTypeForm", model);
            }

            _db.PhonebookTypes.Add(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(PhonebookTypes), model.PropertyId, onlyGlobal);
        }

        public async Task<IActionResult> EditPhonebookType(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var type = await _db.PhonebookTypes.FindAsync(id);
            if (type == null) return NotFound();
            ViewData["FormAction"] = nameof(EditPhonebookType);

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (type.PropertyId.HasValue && type.PropertyId.Value > 0 && !editablePropertyIds.Contains(type.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, type.PropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(PhonebookTypes))
                : returnUrl;

            return View("PhonebookTypeForm", type);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPhonebookType(PhonebookType model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var existing = await _db.PhonebookTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.Id);
            if (existing == null)
            {
                return NotFound();
            }

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (existing.PropertyId.HasValue && existing.PropertyId.Value > 0 && !editablePropertyIds.Contains(existing.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewData["FormAction"] = nameof(EditPhonebookType);
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(PhonebookTypes))
                    : returnUrl;
                return View("PhonebookTypeForm", model);
            }

            _db.PhonebookTypes.Update(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(PhonebookTypes), model.PropertyId, onlyGlobal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhonebookType(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var type = await _db.PhonebookTypes.FindAsync(id);
            if (type == null) return NotFound();

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            if (type.PropertyId.HasValue && type.PropertyId.Value > 0 && !editablePropertyIds.Contains(type.PropertyId.Value))
            {
                return Forbid();
            }

            _db.PhonebookTypes.Remove(type);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(PhonebookTypes), propertyId, onlyGlobal);
        }

        // — CalendarCategories CRUD —

        public async Task<IActionResult> CalendarCategories(int? propertyId = null, bool onlyGlobal = false)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (propertyId.HasValue && propertyId.Value > 0 && !editablePropertyIds.Contains(propertyId.Value))
            {
                return Forbid();
            }

            ViewBag.PropertyFilterOptions = BuildPropertyFilterOptions(nameof(CalendarCategories), properties, propertyId, onlyGlobal);
            ViewBag.SelectedPropertyId = propertyId;
            ViewBag.OnlyGlobal = onlyGlobal;

            var query = _db.CalendarCategories.Include(c => c.Property).AsNoTracking().AsQueryable();
            if (onlyGlobal)
            {
                query = query.Where(c => c.PropertyId == null);
            }
            else if (propertyId.HasValue && propertyId.Value > 0)
            {
                query = query.Where(c => c.PropertyId == propertyId.Value);
            }

            var list = await query
                .OrderBy(c => c.Name)
                .ToListAsync();

            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
            ViewBag.CreateCalendarCategoryUrl = propertyId.HasValue && propertyId.Value > 0
                ? Url.Action(nameof(CreateCalendarCategory), new { propertyId = propertyId.Value, returnUrl = currentUrl })
                : (onlyGlobal
                    ? Url.Action(nameof(CreateCalendarCategory), new { onlyGlobal = true, returnUrl = currentUrl })
                    : Url.Action(nameof(CreateCalendarCategory), new { returnUrl = currentUrl }));

            return View(list);
        }

        public async Task<IActionResult> CreateCalendarCategory(int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            ViewData["FormAction"] = nameof(CreateCalendarCategory);
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, normalizedPropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(CalendarCategories))
                : returnUrl;

            var model = new CalendarCategory
            {
                PropertyId = normalizedPropertyId
            };

            return View("CalendarCategoryForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCalendarCategory(CalendarCategory model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewData["FormAction"] = nameof(CreateCalendarCategory);
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(CalendarCategories))
                    : returnUrl;
                return View("CalendarCategoryForm", model);
            }

            _db.CalendarCategories.Add(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(CalendarCategories), model.PropertyId, onlyGlobal);
        }

        public async Task<IActionResult> EditCalendarCategory(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var item = await _db.CalendarCategories.FindAsync(id);
            if (item == null) return NotFound();

            ViewData["FormAction"] = nameof(EditCalendarCategory);
            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (item.PropertyId.HasValue && item.PropertyId.Value > 0 && !editablePropertyIds.Contains(item.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (propertyId.HasValue && propertyId.Value > 0 && editablePropertyIds.Contains(propertyId.Value))
            {
                normalizedPropertyId = propertyId.Value;
            }

            ViewBag.PropertyOptions = BuildPropertySelectList(properties, item.PropertyId);
            ViewBag.SelectedPropertyId = normalizedPropertyId;
            ViewBag.OnlyGlobal = onlyGlobal;
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action(nameof(CalendarCategories))
                : returnUrl;

            return View("CalendarCategoryForm", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCalendarCategory(CalendarCategory model, int? selectedPropertyId, bool onlyGlobal, string? returnUrl)
        {
            var existing = await _db.CalendarCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == model.Id);
            if (existing == null)
            {
                return NotFound();
            }

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();

            if (existing.PropertyId.HasValue && existing.PropertyId.Value > 0 && !editablePropertyIds.Contains(existing.PropertyId.Value))
            {
                return Forbid();
            }

            int? normalizedPropertyId = null;
            if (selectedPropertyId.HasValue && selectedPropertyId.Value > 0)
            {
                if (editablePropertyIds.Contains(selectedPropertyId.Value))
                {
                    normalizedPropertyId = selectedPropertyId.Value;
                }
                else
                {
                    ModelState.AddModelError("PropertyId", "You do not have access to that property.");
                }
            }

            model.PropertyId = normalizedPropertyId;

            if (!ModelState.IsValid)
            {
                ViewData["FormAction"] = nameof(EditCalendarCategory);
                ViewBag.PropertyOptions = BuildPropertySelectList(properties, model.PropertyId);
                ViewBag.SelectedPropertyId = normalizedPropertyId;
                ViewBag.OnlyGlobal = onlyGlobal;
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action(nameof(CalendarCategories))
                    : returnUrl;
                return View("CalendarCategoryForm", model);
            }

            _db.CalendarCategories.Update(model);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(CalendarCategories), model.PropertyId, onlyGlobal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCalendarCategory(int id, int? propertyId = null, bool onlyGlobal = false, string? returnUrl = null)
        {
            var item = await _db.CalendarCategories.FindAsync(id);
            if (item == null) return NotFound();

            var properties = await GetEditablePropertiesAsync();
            var editablePropertyIds = properties.Select(p => p.Id).ToHashSet();
            if (item.PropertyId.HasValue && item.PropertyId.Value > 0 && !editablePropertyIds.Contains(item.PropertyId.Value))
            {
                return Forbid();
            }

            _db.CalendarCategories.Remove(item);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToSettingsList(nameof(CalendarCategories), propertyId, onlyGlobal);
        }

        // - Layout Editor & Save -

        public async Task<IActionResult> LayoutEditor(int? propertyId = null, int? floor = null)
        {
            var properties = await GetEditablePropertiesAsync();
            if (!properties.Any())
            {
                return Forbid();
            }

            var selectedPropertyId = propertyId ?? properties.First().Id;
            if (properties.All(p => p.Id != selectedPropertyId))
            {
                selectedPropertyId = properties.First().Id;
            }

            var rooms = await _db.Rooms
                .Where(r => r.PropertyId == selectedPropertyId)
                .OrderBy(r => r.Floor)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();

            var layouts = await _db.RoomLayouts
                .Where(l => l.PropertyId == selectedPropertyId)
                .ToListAsync();

            var floors = rooms
                .Select(r => r.Floor)
                .Union(layouts.Select(l => l.Floor))
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            var selectedFloor = floor.HasValue && floors.Contains(floor.Value)
                ? floor.Value
                : (floors.Any() ? floors.First() : 1);

            var layoutDtos = layouts
                .Select(l => new LayoutEditorRoomLayoutViewModel
                {
                    Id = l.Id,
                    PropertyId = l.PropertyId,
                    RoomId = l.RoomId,
                    Floor = l.Floor,
                    X = l.X,
                    Y = l.Y,
                    Width = l.Width,
                    Height = l.Height,
                    Label = l.Label,
                    ShapeType = l.ShapeType,
                    ShapeData = l.ShapeData
                })
                .ToList();

            var layoutsByFloor = layoutDtos
                .GroupBy(l => l.Floor)
                .ToDictionary(g => g.Key, g => g.ToList());

            var roomDtos = rooms
                .Select(r => new LayoutEditorRoomViewModel
                {
                    Id = r.Id,
                    RoomNumber = string.IsNullOrWhiteSpace(r.RoomNumber) ? $"Room {r.Id}" : r.RoomNumber,
                    Floor = r.Floor
                })
                .ToList();

            var propertyOptions = properties
                .Select(p => new SelectListItem
                {
                    Text = $"{p.Name} ({p.Code})",
                    Value = Url.Action(nameof(LayoutEditor), new { propertyId = p.Id, floor = selectedFloor }) ?? "#",
                    Selected = p.Id == selectedPropertyId
                })
                .ToList();

            var vm = new LayoutEditorViewModel
            {
                PropertyId = selectedPropertyId,
                PropertyName = properties.First(p => p.Id == selectedPropertyId).Name,
                SelectedFloor = selectedFloor,
                AllFloors = floors,
                Rooms = roomDtos,
                Layouts = layoutsByFloor.TryGetValue(selectedFloor, out var floorLayouts)
                    ? floorLayouts
                    : new List<LayoutEditorRoomLayoutViewModel>(),
                LayoutsByFloor = layoutsByFloor,
                PropertyOptions = propertyOptions
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveLayout([FromBody] LayoutSaveRequest request)
        {
            if (request == null || request.PropertyId <= 0)
            {
                return BadRequest();
            }

            var properties = await GetEditablePropertiesAsync();
            if (properties.All(p => p.Id != request.PropertyId))
            {
                return Forbid();
            }

            var propertyId = request.PropertyId;
            var floor = request.Floor;
            var layoutsOnFloor = await _db.RoomLayouts
                .Where(l => l.PropertyId == propertyId && l.Floor == floor)
                .ToListAsync();

            var keepIds = new HashSet<int>();
            var orderedLayouts = new List<RoomLayout>();

            foreach (var dto in request.Layouts ?? new List<RoomLayoutDto>())
            {
                var trimmedLabel = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label!.Trim();
                var normalizedShapeType = string.IsNullOrWhiteSpace(dto.ShapeType) ? null : dto.ShapeType!.Trim();
                var normalizedShapeData = string.IsNullOrWhiteSpace(dto.ShapeData) ? null : dto.ShapeData!.Trim();

                RoomLayout? layoutEntity = null;
                if (dto.Id > 0)
                {
                    layoutEntity = layoutsOnFloor.FirstOrDefault(l => l.Id == dto.Id);
                }

                if (layoutEntity == null && dto.RoomId > 0)
                {
                    layoutEntity = layoutsOnFloor.FirstOrDefault(l => l.RoomId == dto.RoomId);
                }

                if (layoutEntity == null)
                {
                    layoutEntity = new RoomLayout
                    {
                        PropertyId = propertyId,
                        RoomId = dto.RoomId,
                        Floor = floor
                    };
                    _db.RoomLayouts.Add(layoutEntity);
                    layoutsOnFloor.Add(layoutEntity);
                }

                layoutEntity.RoomId = dto.RoomId;
                layoutEntity.Floor = floor;
                layoutEntity.X = dto.X;
                layoutEntity.Y = dto.Y;
                layoutEntity.Width = dto.Width;
                layoutEntity.Height = dto.Height;
                layoutEntity.Label = trimmedLabel;
                layoutEntity.ShapeType = normalizedShapeType;
                layoutEntity.ShapeData = normalizedShapeData;

                orderedLayouts.Add(layoutEntity);
                if (layoutEntity.Id > 0)
                {
                    keepIds.Add(layoutEntity.Id);
                }
            }

            foreach (var existing in layoutsOnFloor.ToList())
            {
                if (existing.Id > 0 && !keepIds.Contains(existing.Id))
                {
                    _db.RoomLayouts.Remove(existing);
                }
            }

            await _db.SaveChangesAsync();

            var responseLayouts = orderedLayouts
                .Select(l => new
                {
                    id = l.Id,
                    roomId = l.RoomId,
                    label = l.Label ?? string.Empty,
                    x = l.X,
                    y = l.Y,
                    width = l.Width,
                    height = l.Height,
                    shapeType = string.IsNullOrWhiteSpace(l.ShapeType) ? "rectangle" : l.ShapeType,
                    shapeData = l.ShapeData ?? string.Empty
                })
                .ToList();

            return Json(new { success = true, layouts = responseLayouts });
        }
    }
}




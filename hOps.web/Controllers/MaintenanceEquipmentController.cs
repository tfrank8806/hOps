#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hOps.web.Services.Localization;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Maintenance/Equipment")]
    public class MaintenanceEquipmentController : BaseController
    {
        private const string EquipmentIndexView = "~/Views/Maintenance/Equipment/Index.cshtml";
        private const string EquipmentEditView = "~/Views/Maintenance/Equipment/Edit.cshtml";
        private const string EquipmentDetailsView = "~/Views/Maintenance/Equipment/Details.cshtml";

        private readonly ApplicationDbContext _db;
        private readonly ITranslationService _translationService;

        public MaintenanceEquipmentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITranslationService translationService)
            : base(context, userManager)
        {
            _db = context;
            _translationService = translationService;
        }

        private string GetActiveLanguage()
            => HttpContext?.Items?["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;

        private bool IsDefaultLanguage(string language)
            => string.Equals(language, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);

        private string Translate(string key, string? fallback = null)
            => _translationService.Translate(key, GetActiveLanguage(), fallback ?? key);

        private CancellationToken RequestCancellationToken
            => HttpContext?.RequestAborted ?? CancellationToken.None;

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property to view equipment.");
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = UserCanManage(roles);

            var items = await _db.EquipmentItems
                .Where(e => e.PropertyId == property.Id)
                .OrderBy(e => e.Name)
                .ThenBy(e => e.Category)
                .Select(e => new MaintenanceEquipmentListItemViewModel
                {
                    Id = e.Id,
                    Name = e.Name,
                    Category = e.Category,
                    Location = e.Location,
                    Brand = e.Brand,
                    Model = e.Model,
                    SerialNumber = e.SerialNumber,
                    InstalledOn = e.InstalledOn,
                    WarrantyEndsOn = e.WarrantyEndsOn,
                    UpdatedAtUtc = e.UpdatedAtUtc
                })
                .ToListAsync();

            var viewModel = new MaintenanceEquipmentIndexViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = canManage,
                Items = items
            };

            ViewBag.EquipmentMessage = TempData["EquipmentMessage"];
            ViewBag.EquipmentError = TempData["EquipmentError"];

            return View(EquipmentIndexView, viewModel);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property before adding equipment.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var viewModel = new MaintenanceEquipmentEditorViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = true
            };

            return View(EquipmentEditView, viewModel);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(MaintenanceEquipmentEditorViewModel viewModel)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property before adding equipment.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            viewModel.PropertyId = property.Id;
            viewModel.PropertyName = property.Name;
            viewModel.CanManage = true;

            if (!ModelState.IsValid)
            {
                return View(EquipmentEditView, viewModel);
            }

            var now = DateTime.UtcNow;
            var entity = new EquipmentItem
            {
                PropertyId = property.Id,
                Name = viewModel.Name.Trim(),
                Category = Normalize(viewModel.Category),
                Location = Normalize(viewModel.Location),
                Brand = Normalize(viewModel.Brand),
                Model = Normalize(viewModel.Model),
                SerialNumber = Normalize(viewModel.SerialNumber),
                VendorName = Normalize(viewModel.VendorName),
                VendorPhone = Normalize(viewModel.VendorPhone),
                VendorEmail = Normalize(viewModel.VendorEmail),
                InstalledOn = viewModel.InstalledOn,
                WarrantyEndsOn = viewModel.WarrantyEndsOn,
                Notes = viewModel.Notes?.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.EquipmentItems.Add(entity);
            await _db.SaveChangesAsync();

            TempData["EquipmentMessage"] = Translate("Equipment item created.");
            return RedirectToAction(nameof(Details), new { id = entity.Id });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property to view equipment.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = UserCanManage(roles);

            var entity = await _db.EquipmentItems
                .FirstOrDefaultAsync(e => e.Id == id && e.PropertyId == property.Id);

            if (entity == null)
            {
                return NotFound();
            }

            var viewModel = new MaintenanceEquipmentDetailsViewModel
            {
                Id = entity.Id,
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = canManage,
                Name = entity.Name,
                Category = entity.Category,
                Location = entity.Location,
                Brand = entity.Brand,
                Model = entity.Model,
                SerialNumber = entity.SerialNumber,
                VendorName = entity.VendorName,
                VendorPhone = entity.VendorPhone,
                VendorEmail = entity.VendorEmail,
                InstalledOn = entity.InstalledOn,
                WarrantyEndsOn = entity.WarrantyEndsOn,
                Notes = entity.Notes,
                CreatedAtUtc = entity.CreatedAtUtc,
                UpdatedAtUtc = entity.UpdatedAtUtc
            };

            ViewBag.EquipmentMessage = TempData["EquipmentMessage"];
            ViewBag.EquipmentError = TempData["EquipmentError"];

            return View(EquipmentDetailsView, viewModel);
        }

        [HttpGet("{id:int}/Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property before editing equipment.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var entity = await _db.EquipmentItems
                .FirstOrDefaultAsync(e => e.Id == id && e.PropertyId == property.Id);
            if (entity == null)
            {
                return NotFound();
            }

            var viewModel = new MaintenanceEquipmentEditorViewModel
            {
                Id = entity.Id,
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = true,
                Name = entity.Name,
                Category = entity.Category,
                Location = entity.Location,
                Brand = entity.Brand,
                Model = entity.Model,
                SerialNumber = entity.SerialNumber,
                VendorName = entity.VendorName,
                VendorPhone = entity.VendorPhone,
                VendorEmail = entity.VendorEmail,
                InstalledOn = entity.InstalledOn,
                WarrantyEndsOn = entity.WarrantyEndsOn,
                Notes = entity.Notes
            };

            return View("Equipment/Edit", viewModel);
        }

        [HttpPost("{id:int}/Edit")]
        public async Task<IActionResult> Edit(int id, MaintenanceEquipmentEditorViewModel viewModel)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property before editing equipment.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var entity = await _db.EquipmentItems
                .FirstOrDefaultAsync(e => e.Id == id && e.PropertyId == property.Id);
            if (entity == null)
            {
                return NotFound();
            }

            viewModel.PropertyId = property.Id;
            viewModel.PropertyName = property.Name;
            viewModel.CanManage = true;
            viewModel.Id = id;

            if (!ModelState.IsValid)
            {
                return View(EquipmentEditView, viewModel);
            }

            entity.Name = viewModel.Name.Trim();
            entity.Category = Normalize(viewModel.Category);
            entity.Location = Normalize(viewModel.Location);
            entity.Brand = Normalize(viewModel.Brand);
            entity.Model = Normalize(viewModel.Model);
            entity.SerialNumber = Normalize(viewModel.SerialNumber);
            entity.VendorName = Normalize(viewModel.VendorName);
            entity.VendorPhone = Normalize(viewModel.VendorPhone);
            entity.VendorEmail = Normalize(viewModel.VendorEmail);
            entity.InstalledOn = viewModel.InstalledOn;
            entity.WarrantyEndsOn = viewModel.WarrantyEndsOn;
            entity.Notes = viewModel.Notes?.Trim();
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["EquipmentMessage"] = Translate("Equipment item updated.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property before editing equipment.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var entity = await _db.EquipmentItems
                .FirstOrDefaultAsync(e => e.Id == id && e.PropertyId == property.Id);

            if (entity == null)
            {
                TempData["EquipmentError"] = Translate("Equipment item not found.");
                return RedirectToAction(nameof(Index));
            }

            _db.EquipmentItems.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["EquipmentMessage"] = Translate("Equipment item deleted.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Export.csv")]
        public async Task<IActionResult> Export()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = Translate("Select a property before exporting equipment.");
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var items = await _db.EquipmentItems
                .Where(e => e.PropertyId == property.Id)
                .OrderBy(e => e.Name)
                .ThenBy(e => e.Category)
                .ToListAsync();

            var activeLanguage = GetActiveLanguage();
            var isDefaultLanguage = IsDefaultLanguage(activeLanguage);
            var cancellationToken = RequestCancellationToken;

            var headers = new[]
            {
                "Name",
                "Category",
                "Location",
                "Brand",
                "Model",
                "Serial Number",
                "Vendor Name",
                "Vendor Phone",
                "Vendor Email",
                "Installed On",
                "Warranty Ends",
                "Notes"
            };

            var builder = new StringBuilder();
            var translatedHeaders = headers
                .Select(header => Csv(_translationService.Translate(header, activeLanguage, header)));
            builder.AppendLine(string.Join(",", translatedHeaders));

            foreach (var item in items)
            {
                var name = item.Name ?? string.Empty;
                var category = item.Category ?? string.Empty;
                var location = item.Location ?? string.Empty;
                var brand = item.Brand ?? string.Empty;
                var model = item.Model ?? string.Empty;
                var serialNumber = item.SerialNumber ?? string.Empty;
                var vendorName = item.VendorName ?? string.Empty;
                var notes = item.Notes ?? string.Empty;

                if (!isDefaultLanguage)
                {
                    var entityId = item.Id.ToString(CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "Name",
                            name,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            name = translated;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "Category",
                            category,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            category = translated;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(location))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "Location",
                            location,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            location = translated;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(brand))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "Brand",
                            brand,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            brand = translated;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(model))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "Model",
                            model,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            model = translated;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(serialNumber))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "SerialNumber",
                            serialNumber,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            serialNumber = translated;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(vendorName))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "VendorName",
                            vendorName,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            vendorName = translated;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(notes))
                    {
                        var translated = await _translationService.TranslateDynamicAsync(
                            "EquipmentItem",
                            entityId,
                            "Notes",
                            notes,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            notes = translated;
                        }
                    }
                }

                var row = new[]
                {
                    Csv(name),
                    Csv(category),
                    Csv(location),
                    Csv(brand),
                    Csv(model),
                    Csv(serialNumber),
                    Csv(vendorName),
                    Csv(item.VendorPhone),
                    Csv(item.VendorEmail),
                    Csv(item.InstalledOn?.ToString("yyyy-MM-dd")),
                    Csv(item.WarrantyEndsOn?.ToString("yyyy-MM-dd")),
                    Csv(notes)
                };

                builder.AppendLine(string.Join(",", row));
            }

            var fileName = $"{SanitizeFileName(property.Name)}-equipment-{DateTime.UtcNow:yyyyMMdd}.csv";
            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv", fileName);
        }

        private static bool UserCanManage(IList<string> roles)
        {
            return roles.Any(role =>
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n');
            var sanitized = value.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{sanitized}\"" : sanitized;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sanitized = new string(value
                .Select(ch => invalid.Contains(ch) ? '-' : ch)
                .ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "equipment" : sanitized;
        }
    }
}

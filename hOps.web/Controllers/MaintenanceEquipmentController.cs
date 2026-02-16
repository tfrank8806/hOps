#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Maintenance/Equipment")]
    public class MaintenanceEquipmentController : BaseController
    {
        private readonly ApplicationDbContext _db;

        public MaintenanceEquipmentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _db = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = "Select a property to view equipment.";
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

            return View("Equipment/Index", viewModel);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = "Select a property before adding equipment.";
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

            var viewModel = new MaintenanceEquipmentEditorViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = true
            };

            return View("Equipment/Edit", viewModel);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(MaintenanceEquipmentEditorViewModel viewModel)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = "Select a property before adding equipment.";
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

            viewModel.PropertyId = property.Id;
            viewModel.PropertyName = property.Name;
            viewModel.CanManage = true;

            if (!ModelState.IsValid)
            {
                return View("Equipment/Edit", viewModel);
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

            TempData["EquipmentMessage"] = "Equipment item created.";
            return RedirectToAction(nameof(Details), new { id = entity.Id });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = "Select a property to view equipment.";
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

            return View("Equipment/Details", viewModel);
        }

        [HttpGet("{id:int}/Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = "Select a property before editing equipment.";
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
                TempData["EquipmentError"] = "Select a property before editing equipment.";
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
                return View("Equipment/Edit", viewModel);
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

            TempData["EquipmentMessage"] = "Equipment item updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = "Select a property before editing equipment.";
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
                TempData["EquipmentError"] = "Equipment item not found.";
                return RedirectToAction(nameof(Index));
            }

            _db.EquipmentItems.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["EquipmentMessage"] = "Equipment item deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Export.csv")]
        public async Task<IActionResult> Export()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["EquipmentError"] = "Select a property before exporting equipment.";
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

            var builder = new StringBuilder();
            builder.AppendLine("Name,Category,Location,Brand,Model,Serial Number,Vendor Name,Vendor Phone,Vendor Email,Installed On,Warranty Ends,Notes");
            foreach (var item in items)
            {
                builder.AppendLine(string.Join(",", new[]
                {
                    Csv(item.Name),
                    Csv(item.Category),
                    Csv(item.Location),
                    Csv(item.Brand),
                    Csv(item.Model),
                    Csv(item.SerialNumber),
                    Csv(item.VendorName),
                    Csv(item.VendorPhone),
                    Csv(item.VendorEmail),
                    Csv(item.InstalledOn?.ToString("yyyy-MM-dd")),
                    Csv(item.WarrantyEndsOn?.ToString("yyyy-MM-dd")),
                    Csv(item.Notes)
                }));
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

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Utilities;
using hOps.web.ViewModels.Housekeeping.LinenInventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class LinenInventoryController : BaseController
    {
        private readonly IUserTimeZoneService _timeZoneService;
        private readonly ILogger<LinenInventoryController> _logger;
        private readonly IWebHostEnvironment _environment;

        public LinenInventoryController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUserTimeZoneService timeZoneService,
            ILogger<LinenInventoryController> logger,
            IWebHostEnvironment environment) : base(context, userManager)
        {
            _timeZoneService = timeZoneService;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before opening the Linen Inventory workbook.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var viewModel = await BuildPageViewModelAsync(property, user, null);
            viewModel.FlashMessage = TempData["LinenInventoryMessage"] as string ?? viewModel.FlashMessage;
            viewModel.ErrorMessage = TempData["LinenInventoryError"] as string ?? viewModel.ErrorMessage;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Setup()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before opening the linen setup.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var viewModel = await BuildPageViewModelAsync(property, user, null);
            viewModel.FlashMessage = TempData["LinenInventoryMessage"] as string ?? viewModel.FlashMessage;
            viewModel.ErrorMessage = TempData["LinenInventoryError"] as string ?? viewModel.ErrorMessage;

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SaveInventory(LinenInventoryEntryForm entry)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before saving a linen count.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            entry ??= new LinenInventoryEntryForm();
            entry.Rows = entry.Rows?.Where(row => row != null).ToList() ?? new List<LinenInventoryEntryRowInput>();

            if (entry.Rows.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Add at least one inventory item before saving.");
            }

            var requestedItemIds = entry.Rows.Select(r => r.ItemId).Distinct().ToList();
            var items = await _context.LinenInventoryItems
                .Include(i => i.Requirements)
                .ThenInclude(r => r.RoomType)
                .Where(i => i.PropertyId == property.Id && requestedItemIds.Contains(i.Id) && !i.IsArchived)
                .ToListAsync();

            if (items.Count != requestedItemIds.Count)
            {
                ModelState.AddModelError(string.Empty, "One or more items were not found. Refresh the page and try again.");
            }

            if (!ModelState.IsValid)
            {
                var invalidViewModel = await BuildPageViewModelAsync(property, user, entry);
                invalidViewModel.ErrorMessage = "We could not save the linen inventory. Fix the errors and try again.";
                return View(nameof(Index), invalidViewModel);
            }

            var itemLookup = items.ToDictionary(i => i.Id);
            var inventoryDate = entry.InventoryDate == default
                ? _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date
                : entry.InventoryDate.Date;

            var session = new LinenInventorySession
            {
                PropertyId = property.Id,
                InventoryDate = inventoryDate,
                Month = inventoryDate.Month,
                Year = inventoryDate.Year,
                MonthlyBudget = entry.MonthlyBudget < 0 ? 0 : entry.MonthlyBudget,
                PerformedBy = string.IsNullOrWhiteSpace(entry.PerformedBy)
                    ? BuildUserDisplayName(user)
                    : entry.PerformedBy!.Trim(),
                CreatedByUserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            foreach (var row in entry.Rows)
            {
                if (!itemLookup.TryGetValue(row.ItemId, out var item))
                {
                    continue;
                }

                var inRooms = ComputeInRooms(item);
                var budgetedPar = Math.Round(inRooms * (item.ParLevelTarget <= 0 ? 1 : item.ParLevelTarget), 2, MidpointRounding.AwayFromZero);
                var totalOnHand = Math.Round(row.LaundryClean + row.LaundryDirty + row.InStorage + row.OnCarts, 2, MidpointRounding.AwayFromZero);
                var orderRecommendation = Math.Round(Math.Max(0, budgetedPar - totalOnHand), 2, MidpointRounding.AwayFromZero);
                var normalizedCaseCount = item.OrderCaseCount <= 0 ? 1 : item.OrderCaseCount;
                var casesToOrder = Math.Round(orderRecommendation / normalizedCaseCount, 2, MidpointRounding.AwayFromZero);
                var needCost = Math.Round(casesToOrder * item.OrderCasePrice, 2, MidpointRounding.AwayFromZero);
                var orderCost = Math.Round(row.CasesPurchased * item.OrderCasePrice, 2, MidpointRounding.AwayFromZero);
                var actToPar = budgetedPar > 0
                    ? Math.Round(totalOnHand / budgetedPar, 4, MidpointRounding.AwayFromZero)
                    : 0;

                session.Items.Add(new LinenInventorySessionItem
                {
                    InventoryItemId = item.Id,
                    LaundryClean = row.LaundryClean,
                    LaundryDirty = row.LaundryDirty,
                    InStorage = row.InStorage,
                    OnCarts = row.OnCarts,
                    TotalOnHand = totalOnHand,
                    LastMonthActuals = row.LastMonthActuals,
                    InRoomsQuantity = inRooms,
                    BudgetedPar = budgetedPar,
                    OrderRecommendation = orderRecommendation,
                    ActToParRatio = actToPar,
                    CasesToOrder = casesToOrder,
                    NeedCost = needCost,
                    CasesPurchased = row.CasesPurchased,
                    OrderCost = orderCost
                });
            }

            session.ProjectedNeedCost = session.Items.Sum(i => i.NeedCost);
            session.TotalCost = session.Items.Sum(i => i.OrderCost);

            _context.LinenInventorySessions.Add(session);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save linen inventory for property {PropertyId}", property.Id);
                var invalidViewModel = await BuildPageViewModelAsync(property, user, entry);
                invalidViewModel.ErrorMessage = "We could not save the linen inventory. Please try again.";
                return View(nameof(Index), invalidViewModel);
            }

            TempData["LinenInventoryMessage"] = "Saved the linen inventory snapshot.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SaveRoomTypes(LinenInventoryRoomTypeCollectionInput input)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before adjusting the setup information.";
                return RedirectToAction(nameof(Setup));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserCanEditSetupAsync(user))
            {
                return Forbid();
            }

            var roomTypeRows = input.RoomTypes?.Where(r => r != null).ToList() ?? new List<LinenInventoryRoomTypeForm>();

            var settings = await _context.LinenInventorySettings.FirstOrDefaultAsync(s => s.PropertyId == property.Id);
            if (settings == null)
            {
                settings = new LinenInventorySettings
                {
                    PropertyId = property.Id
                };
                _context.LinenInventorySettings.Add(settings);
            }

            settings.PropertyLabel = string.IsNullOrWhiteSpace(input.PropertyLabel)
                ? null
                : input.PropertyLabel!.Trim();
            settings.DefaultMonthlyBudget = input.DefaultMonthlyBudget < 0 ? 0 : input.DefaultMonthlyBudget;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedByUserId = user.Id;

            var existingRoomTypes = await _context.LinenInventoryRoomTypes
                .Where(rt => rt.PropertyId == property.Id)
                .ToListAsync();

            foreach (var row in roomTypeRows)
            {
                var trimmedName = (row.Name ?? string.Empty).Trim();

                if (row.Id.HasValue && (row.IsDeleted || string.IsNullOrWhiteSpace(trimmedName)))
                {
                    var roomTypeEntity = existingRoomTypes.FirstOrDefault(rt => rt.Id == row.Id.Value);
                    if (roomTypeEntity != null)
                    {
                        _context.LinenInventoryRoomTypes.Remove(roomTypeEntity);
                    }

                    continue;
                }

                if (!row.Id.HasValue && (row.IsDeleted || string.IsNullOrWhiteSpace(trimmedName)))
                {
                    continue;
                }

                if (row.Id.HasValue)
                {
                    var roomTypeEntity = existingRoomTypes.FirstOrDefault(rt => rt.Id == row.Id.Value);
                    if (roomTypeEntity == null)
                    {
                        continue;
                    }

                    roomTypeEntity.Name = trimmedName;
                    roomTypeEntity.TotalRooms = row.TotalRooms < 0 ? 0 : row.TotalRooms;
                    roomTypeEntity.SortOrder = row.SortOrder;
                    continue;
                }

                _context.LinenInventoryRoomTypes.Add(new LinenInventoryRoomType
                {
                    PropertyId = property.Id,
                    Name = trimmedName,
                    TotalRooms = row.TotalRooms < 0 ? 0 : row.TotalRooms,
                    SortOrder = row.SortOrder
                });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update linen setup for property {PropertyId}", property.Id);
                TempData["LinenInventoryError"] = "We could not save the setup information. Please try again.";
                return RedirectToAction(nameof(Setup));
            }

            TempData["LinenInventoryMessage"] = "Updated the setup information.";
            return RedirectToAction(nameof(Setup));
        }

        [HttpPost]
        public async Task<IActionResult> SaveItems(LinenInventoryItemCollectionInput input)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before editing the linen items.";
                return RedirectToAction(nameof(Setup));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserCanEditSetupAsync(user))
            {
                return Forbid();
            }

            var roomTypes = await _context.LinenInventoryRoomTypes
                .Where(rt => rt.PropertyId == property.Id)
                .OrderBy(rt => rt.SortOrder)
                .ThenBy(rt => rt.Name)
                .ToListAsync();

            if (roomTypes.Count == 0)
            {
                TempData["LinenInventoryError"] = "Add at least one room type before configuring items.";
                return RedirectToAction(nameof(Setup));
            }

            var roomTypeLookup = roomTypes.ToDictionary(rt => rt.Id);

            var existingItems = await _context.LinenInventoryItems
                .Include(i => i.Requirements)
                .Where(i => i.PropertyId == property.Id)
                .ToListAsync();

            foreach (var row in input.Items ?? new List<LinenInventoryItemForm>())
            {
                var trimmedName = (row.Name ?? string.Empty).Trim();
                var shouldRemove = row.IsDeleted || string.IsNullOrWhiteSpace(trimmedName);

                if (row.Id.HasValue && shouldRemove)
                {
                    var existingItem = existingItems.FirstOrDefault(i => i.Id == row.Id.Value);
                    if (existingItem != null)
                    {
                        _context.LinenInventoryItems.Remove(existingItem);
                    }

                    continue;
                }

                if (!row.Id.HasValue && shouldRemove)
                {
                    continue;
                }

                LinenInventoryItem entity;
                if (row.Id.HasValue)
                {
                    entity = existingItems.FirstOrDefault(i => i.Id == row.Id.Value);
                    if (entity == null)
                    {
                        continue;
                    }
                }
                else
                {
                    entity = new LinenInventoryItem
                    {
                        PropertyId = property.Id
                    };
                    _context.LinenInventoryItems.Add(entity);
                }

                entity.Name = trimmedName;
                entity.OrderItemNumber = string.IsNullOrWhiteSpace(row.OrderItemNumber) ? null : row.OrderItemNumber!.Trim();
                entity.OrderCaseCount = row.OrderCaseCount <= 0 ? 1 : row.OrderCaseCount;
                entity.OrderCasePrice = row.OrderCasePrice < 0 ? 0 : row.OrderCasePrice;
                entity.ParLevelTarget = row.ParLevelTarget <= 0 ? 1 : row.ParLevelTarget;
                entity.SortOrder = row.SortOrder;
                entity.IsArchived = false;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                entity.UpdatedByUserId = user.Id;

                var existingRequirements = entity.Requirements.ToDictionary(r => r.RoomTypeId);

                foreach (var roomType in roomTypes)
                {
                    var requirementForm = row.Requirements?.FirstOrDefault(r => r.RoomTypeId == roomType.Id);
                    var units = requirementForm?.UnitsPerRoom ?? 0;
                    units = units < 0 ? 0 : units;

                    if (existingRequirements.TryGetValue(roomType.Id, out var requirement))
                    {
                        requirement.UnitsPerRoom = units;
                        existingRequirements.Remove(roomType.Id);
                    }
                    else if (units > 0)
                    {
                        entity.Requirements.Add(new LinenInventoryItemRequirement
                        {
                            RoomTypeId = roomType.Id,
                            UnitsPerRoom = units
                        });
                    }
                }

                foreach (var obsoleteRequirement in existingRequirements.Values)
                {
                    _context.LinenInventoryItemRequirements.Remove(obsoleteRequirement);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update linen items for property {PropertyId}", property.Id);
                TempData["LinenInventoryError"] = "We could not save the updated items. Please try again.";
                return RedirectToAction(nameof(Setup));
            }

            TempData["LinenInventoryMessage"] = "Updated the linen items.";
            return RedirectToAction(nameof(Setup));
        }

        [HttpPost]
        public async Task<IActionResult> LoadDefaults()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before loading the defaults.";
                return RedirectToAction(nameof(Setup));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserCanEditSetupAsync(user))
            {
                return Forbid();
            }

            var hasAnyRoomTypes = await _context.LinenInventoryRoomTypes.AnyAsync(rt => rt.PropertyId == property.Id);
            var hasAnyItems = await _context.LinenInventoryItems.AnyAsync(i => i.PropertyId == property.Id);
            if (hasAnyRoomTypes || hasAnyItems)
            {
                TempData["LinenInventoryError"] = "Clear the existing setup before loading the template.";
                return RedirectToAction(nameof(Setup));
            }

            var template = LinenInventorySeedData.Template;
            var createdRoomTypes = new List<LinenInventoryRoomType>();
            var order = 0;

            foreach (var templateRoom in template.RoomTypes)
            {
                var entity = new LinenInventoryRoomType
                {
                    PropertyId = property.Id,
                    Name = templateRoom.Name,
                    TotalRooms = templateRoom.Rooms,
                    SortOrder = order++
                };
                createdRoomTypes.Add(entity);
                _context.LinenInventoryRoomTypes.Add(entity);
            }

            await _context.SaveChangesAsync();

            var roomTypeLookup = createdRoomTypes.ToDictionary(rt => rt.Name, StringComparer.OrdinalIgnoreCase);
            order = 0;

            foreach (var templateItem in template.Items)
            {
                var entity = new LinenInventoryItem
                {
                    PropertyId = property.Id,
                    Name = templateItem.Name,
                    OrderItemNumber = templateItem.OrderItemNumber,
                    OrderCaseCount = templateItem.CaseCount <= 0 ? 1 : templateItem.CaseCount,
                    OrderCasePrice = templateItem.CasePrice < 0 ? 0 : templateItem.CasePrice,
                    ParLevelTarget = templateItem.ParLevelTarget <= 0 ? 1 : templateItem.ParLevelTarget,
                    SortOrder = order++,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedByUserId = user.Id
                };

                foreach (var requirement in templateItem.Requirements)
                {
                    if (!roomTypeLookup.TryGetValue(requirement.Key, out var roomType))
                    {
                        continue;
                    }

                    if (requirement.Value <= 0)
                    {
                        continue;
                    }

                    entity.Requirements.Add(new LinenInventoryItemRequirement
                    {
                        RoomTypeId = roomType.Id,
                        UnitsPerRoom = requirement.Value
                    });
                }

                _context.LinenInventoryItems.Add(entity);
            }

            var settings = await _context.LinenInventorySettings.FirstOrDefaultAsync(s => s.PropertyId == property.Id);
            if (settings == null)
            {
                settings = new LinenInventorySettings
                {
                    PropertyId = property.Id
                };
                _context.LinenInventorySettings.Add(settings);
            }

            settings.PropertyLabel = property.Name;
            settings.DefaultMonthlyBudget = 0;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedByUserId = user.Id;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load default linen inventory data for property {PropertyId}", property.Id);
                TempData["LinenInventoryError"] = "We could not load the default data. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            TempData["LinenInventoryMessage"] = "Loaded the Aimbridge template items.";
            return RedirectToAction(nameof(Setup));
        }

        [HttpGet]
        public IActionResult DownloadSetupTemplate()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before downloading the template.";
                return RedirectToAction(nameof(Index));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Section,Name,TotalRooms,ItemNumber,CaseCount,CasePrice,ParTarget,SortOrder,RequirementRoomType,RequirementUnits");
            builder.AppendLine("RoomType,Standard King,60,,,,,,1,");
            builder.AppendLine("RoomType,Standard Double,80,,,,,,2,");
            builder.AppendLine("RoomType,Suites,10,,,,,,3,");
            builder.AppendLine("Item,Bath Towels,,BT-100,50,115,3,1,Standard King,4");
            builder.AppendLine("Item,Bath Towels,,BT-100,50,115,3,1,Standard Double,6");
            builder.AppendLine("Item,Bath Towels,,BT-100,50,115,3,1,Suites,6");
            builder.AppendLine("Item,Bath Mats,,BM-42,40,72,2,2,Standard King,2");
            builder.AppendLine("Item,Bath Mats,,BM-42,40,72,2,2,Standard Double,2");
            builder.AppendLine("Item,Pillow Cases,,PC-15,120,96.5,3,3,Standard King,4");
            builder.AppendLine("Item,Pillow Cases,,PC-15,120,96.5,3,3,Standard Double,6");

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv", "linen-inventory-setup-template.csv");
        }

        [HttpPost]
        public async Task<IActionResult> ImportSetupCsv(IFormFile? csvFile)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before importing a setup.";
                return RedirectToAction(nameof(Setup));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserCanEditSetupAsync(user))
            {
                return Forbid();
            }

            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["LinenInventoryError"] = "Select a CSV file to upload.";
                return RedirectToAction(nameof(Setup));
            }

            List<SetupCsvRoomType> roomTypes;
            List<SetupCsvItem> items;
            try
            {
                (roomTypes, items) = await ParseSetupCsvAsync(csvFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse linen setup CSV for property {PropertyId}", property.Id);
                TempData["LinenInventoryError"] = ex.Message;
                return RedirectToAction(nameof(Setup));
            }

            if (!roomTypes.Any())
            {
                TempData["LinenInventoryError"] = "Add at least one room type row to the CSV.";
                return RedirectToAction(nameof(Setup));
            }

            if (!items.Any())
            {
                TempData["LinenInventoryError"] = "Add at least one linen item row to the CSV.";
                return RedirectToAction(nameof(Setup));
            }

            var existingItems = await _context.LinenInventoryItems
                .Include(i => i.Requirements)
                .Where(i => i.PropertyId == property.Id)
                .ToListAsync();

            if (existingItems.Any())
            {
                var requirements = existingItems.SelectMany(i => i.Requirements).ToList();
                if (requirements.Any())
                {
                    _context.LinenInventoryItemRequirements.RemoveRange(requirements);
                }

                _context.LinenInventoryItems.RemoveRange(existingItems);
            }

            var existingRoomTypes = await _context.LinenInventoryRoomTypes
                .Where(rt => rt.PropertyId == property.Id)
                .ToListAsync();

            if (existingRoomTypes.Any())
            {
                _context.LinenInventoryRoomTypes.RemoveRange(existingRoomTypes);
            }

            await _context.SaveChangesAsync();

            var roomTypeEntities = new List<LinenInventoryRoomType>();
            var seenRoomTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in roomTypes.OrderBy(rt => rt.SortOrder).ThenBy(rt => rt.Name))
            {
                var trimmedName = row.Name.Trim();
                if (string.IsNullOrWhiteSpace(trimmedName) || !seenRoomTypeNames.Add(trimmedName))
                {
                    continue;
                }

                var entity = new LinenInventoryRoomType
                {
                    PropertyId = property.Id,
                    Name = trimmedName,
                    TotalRooms = row.TotalRooms < 0 ? 0 : row.TotalRooms,
                    SortOrder = row.SortOrder,
                    IsActive = true
                };
                roomTypeEntities.Add(entity);
                _context.LinenInventoryRoomTypes.Add(entity);
            }

            if (roomTypeEntities.Count == 0)
            {
                TempData["LinenInventoryError"] = "The CSV file did not include any valid room type rows.";
                return RedirectToAction(nameof(Setup));
            }

            await _context.SaveChangesAsync();

            var roomTypeLookup = roomTypeEntities.ToDictionary(rt => rt.Name, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            foreach (var item in items.OrderBy(i => i.SortOrder).ThenBy(i => i.Name))
            {
                var entity = new LinenInventoryItem
                {
                    PropertyId = property.Id,
                    Name = item.Name.Trim(),
                    OrderItemNumber = string.IsNullOrWhiteSpace(item.ItemNumber) ? null : item.ItemNumber.Trim(),
                    OrderCaseCount = item.CaseCount <= 0 ? 1 : Math.Round(item.CaseCount, 2, MidpointRounding.AwayFromZero),
                    OrderCasePrice = item.CasePrice < 0 ? 0 : Math.Round(item.CasePrice, 2, MidpointRounding.AwayFromZero),
                    ParLevelTarget = item.ParLevelTarget <= 0 ? 1 : Math.Round(item.ParLevelTarget, 2, MidpointRounding.AwayFromZero),
                    SortOrder = item.SortOrder,
                    IsArchived = false,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = user.Id
                };

                foreach (var requirement in item.Requirements)
                {
                    if (requirement.UnitsPerRoom <= 0)
                    {
                        continue;
                    }

                    if (!roomTypeLookup.TryGetValue(requirement.RoomTypeName, out var roomType))
                    {
                        continue;
                    }

                    entity.Requirements.Add(new LinenInventoryItemRequirement
                    {
                        RoomTypeId = roomType.Id,
                        UnitsPerRoom = Math.Round(requirement.UnitsPerRoom, 2, MidpointRounding.AwayFromZero)
                    });
                }

                _context.LinenInventoryItems.Add(entity);
            }

            await _context.SaveChangesAsync();

            TempData["LinenInventoryMessage"] = $"Imported {roomTypes.Count} room type{(roomTypes.Count == 1 ? string.Empty : "s")} and {items.Count} item{(items.Count == 1 ? string.Empty : "s")} from the CSV template.";
            return RedirectToAction(nameof(Setup));
        }

        [HttpGet]
        public async Task<IActionResult> Supply()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before opening Supply Inventory.";
                return RedirectToAction("Index", "Home");
            }

            var settings = await _context.LinenInventorySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.PropertyId == property.Id);

            var viewModel = new SupplyInventoryPageViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                DefaultMonthlyBudget = settings?.DefaultMonthlyBudget ?? 0,
                TemplateItems = LoadSupplyTemplate()
            };

            return View(viewModel);
        }

        private async Task<LinenInventoryPageViewModel> BuildPageViewModelAsync(Property property, ApplicationUser user, LinenInventoryEntryForm? entryOverride)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var canEditSetup = roles.Any(role =>
                role.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            var settings = await _context.LinenInventorySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.PropertyId == property.Id);

            var roomTypes = await _context.LinenInventoryRoomTypes
                .AsNoTracking()
                .Where(rt => rt.PropertyId == property.Id && rt.IsActive)
                .OrderBy(rt => rt.SortOrder)
                .ThenBy(rt => rt.Name)
                .ToListAsync();

            var items = await _context.LinenInventoryItems
                .AsNoTracking()
                .Include(i => i.Requirements)
                .ThenInclude(r => r.RoomType)
                .Where(i => i.PropertyId == property.Id && !i.IsArchived)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Name)
                .ToListAsync();

            var lastSession = await _context.LinenInventorySessions
                .AsNoTracking()
                .Include(s => s.Items)
                .Where(s => s.PropertyId == property.Id)
                .OrderByDescending(s => s.InventoryDate)
                .ThenByDescending(s => s.CreatedAtUtc)
                .FirstOrDefaultAsync();

            var lastTotals = lastSession?.Items.ToDictionary(i => i.InventoryItemId, i => i.TotalOnHand) ?? new Dictionary<int, decimal>();

            var entryForm = entryOverride ?? new LinenInventoryEntryForm();
            if (entryOverride == null)
            {
                var localDate = _timeZoneService.ConvertToUserTime(DateTime.UtcNow).Date;
                entryForm.InventoryDate = localDate;
                entryForm.PerformedBy = BuildUserDisplayName(user);
                entryForm.MonthlyBudget = settings?.DefaultMonthlyBudget ?? lastSession?.MonthlyBudget ?? 0;
            }

            var inventoryRows = new List<LinenInventoryItemRowViewModel>();
            var normalizedRows = new List<LinenInventoryEntryRowInput>();

            foreach (var item in items)
            {
                var formRow = entryForm.Rows.FirstOrDefault(r => r.ItemId == item.Id);
                if (formRow == null)
                {
                    formRow = new LinenInventoryEntryRowInput
                    {
                        ItemId = item.Id,
                        LastMonthActuals = lastTotals.TryGetValue(item.Id, out var previous) ? previous : 0
                    };
                }

                normalizedRows.Add(formRow);

                var inRooms = ComputeInRooms(item);
                var budgetedPar = Math.Round(inRooms * (item.ParLevelTarget <= 0 ? 1 : item.ParLevelTarget), 2, MidpointRounding.AwayFromZero);

                inventoryRows.Add(new LinenInventoryItemRowViewModel
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    OrderItemNumber = item.OrderItemNumber,
                    OrderCaseCount = item.OrderCaseCount <= 0 ? 1 : item.OrderCaseCount,
                    OrderCasePrice = item.OrderCasePrice,
                    ParLevelTarget = item.ParLevelTarget <= 0 ? 1 : item.ParLevelTarget,
                    InRooms = inRooms,
                    BudgetedPar = budgetedPar,
                    LastMonthActuals = formRow.LastMonthActuals
                });
            }

            entryForm.Rows = normalizedRows;

            var setupViewModel = BuildSetupViewModel(settings, roomTypes, items);

            return new LinenInventoryPageViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanEditSetup = canEditSetup,
                Entry = entryForm,
                InventoryRows = inventoryRows,
                Setup = setupViewModel,
                LastInventoryDate = lastSession?.InventoryDate,
                LastSessionProjectedNeed = lastSession?.ProjectedNeedCost,
                LastSessionTotalCost = lastSession?.TotalCost
            };
        }

        private static LinenInventorySetupViewModel BuildSetupViewModel(
            LinenInventorySettings? settings,
            List<LinenInventoryRoomType> roomTypes,
            List<LinenInventoryItem> items)
        {
            var setup = new LinenInventorySetupViewModel
            {
                PropertyLabel = settings?.PropertyLabel,
                DefaultMonthlyBudget = settings?.DefaultMonthlyBudget ?? 0
            };

            setup.RoomTypes = roomTypes
                .Select(rt => new LinenInventoryRoomTypeForm
                {
                    Id = rt.Id,
                    Name = rt.Name,
                    TotalRooms = rt.TotalRooms,
                    SortOrder = rt.SortOrder
                })
                .ToList();

            foreach (var item in items)
            {
                var form = new LinenInventoryItemForm
                {
                    Id = item.Id,
                    Name = item.Name,
                    OrderItemNumber = item.OrderItemNumber,
                    OrderCaseCount = item.OrderCaseCount <= 0 ? 1 : item.OrderCaseCount,
                    OrderCasePrice = item.OrderCasePrice,
                    ParLevelTarget = item.ParLevelTarget <= 0 ? 1 : item.ParLevelTarget,
                    SortOrder = item.SortOrder
                };

                foreach (var roomType in roomTypes)
                {
                    var requirement = item.Requirements.FirstOrDefault(r => r.RoomTypeId == roomType.Id);
                    form.Requirements.Add(new LinenInventoryItemRequirementForm
                    {
                        RoomTypeId = roomType.Id,
                        UnitsPerRoom = requirement?.UnitsPerRoom ?? 0
                    });
                }

                setup.Items.Add(form);
            }

            return setup;
        }

        private decimal ComputeInRooms(LinenInventoryItem item)
        {
            if (item.Requirements == null || item.Requirements.Count == 0)
            {
                return 0;
            }

            decimal total = 0;
            foreach (var requirement in item.Requirements)
            {
                if (requirement.RoomType == null)
                {
                    continue;
                }

                total += requirement.UnitsPerRoom * requirement.RoomType.TotalRooms;
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }

        private async Task<bool> UserCanEditSetupAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Any(role =>
                role.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildUserDisplayName(ApplicationUser user)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName))
            {
                parts.Add(user.FirstName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(user.LastName))
            {
                parts.Add(user.LastName.Trim());
            }

            if (parts.Count > 0)
            {
                return string.Join(" ", parts);
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }

            return user.UserName ?? "Manager";
        }

        private async Task<(List<SetupCsvRoomType> RoomTypes, List<SetupCsvItem> Items)> ParseSetupCsvAsync(IFormFile csvFile)
        {
            var roomTypes = new List<SetupCsvRoomType>();
            var items = new Dictionary<string, SetupCsvItem>(StringComparer.OrdinalIgnoreCase);

            using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? headerLine = await reader.ReadLineAsync();
            if (headerLine == null)
            {
                throw new InvalidOperationException("The CSV file was empty.");
            }

            var headers = SplitCsvLine(headerLine);
            var headerLookup = headers
                .Select((name, index) => new { Name = name?.Trim(), Index = index })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToDictionary(x => x.Name!, x => x.Index, StringComparer.OrdinalIgnoreCase);

            if (!headerLookup.ContainsKey("Section") || !headerLookup.ContainsKey("Name"))
            {
                throw new InvalidOperationException("The CSV header must include at least the Section and Name columns.");
            }

            static int ParseInt(string? text, int fallback = 0)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return fallback;
                }

                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
            }

            static decimal ParseDecimal(string? text, decimal fallback = 0)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return fallback;
                }

                return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : fallback;
            }

            int nextRoomTypeOrder = 1;
            int nextItemOrder = 1;

            string ReadValue(List<string> cells, string header)
            {
                if (!headerLookup.TryGetValue(header, out var index))
                {
                    return string.Empty;
                }

                return index < cells.Count ? cells[index]?.Trim() ?? string.Empty : string.Empty;
            }

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var cells = SplitCsvLine(line);
                if (cells.Count == 0)
                {
                    continue;
                }

                var section = ReadValue(cells, "Section");
                var name = ReadValue(cells, "Name");
                if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (section.Equals("RoomType", StringComparison.OrdinalIgnoreCase))
                {
                    var totalRooms = ParseInt(ReadValue(cells, "TotalRooms"));
                    var sortOrder = ParseInt(ReadValue(cells, "SortOrder"), nextRoomTypeOrder);
                    roomTypes.Add(new SetupCsvRoomType
                    {
                        Name = name.Trim(),
                        TotalRooms = totalRooms < 0 ? 0 : totalRooms,
                        SortOrder = sortOrder
                    });
                    nextRoomTypeOrder = sortOrder >= nextRoomTypeOrder ? sortOrder + 1 : nextRoomTypeOrder + 1;
                    continue;
                }

                if (section.Equals("Item", StringComparison.OrdinalIgnoreCase))
                {
                    if (!items.TryGetValue(name, out var item))
                    {
                        item = new SetupCsvItem
                        {
                            Name = name.Trim()
                        };
                        items[name] = item;
                    }

                    var sortOrder = ParseInt(ReadValue(cells, "SortOrder"), nextItemOrder);
                    if (item.SortOrder == 0 || sortOrder < item.SortOrder)
                    {
                        item.SortOrder = sortOrder;
                    }

                    var itemNumber = ReadValue(cells, "ItemNumber");
                    if (!string.IsNullOrWhiteSpace(itemNumber))
                    {
                        item.ItemNumber = itemNumber.Trim();
                    }

                    var caseCount = ParseDecimal(ReadValue(cells, "CaseCount"));
                    if (caseCount > 0)
                    {
                        item.CaseCount = caseCount;
                    }

                    var casePrice = ParseDecimal(ReadValue(cells, "CasePrice"));
                    if (casePrice > 0)
                    {
                        item.CasePrice = casePrice;
                    }

                    var parTarget = ParseDecimal(ReadValue(cells, "ParTarget"));
                    if (parTarget > 0)
                    {
                        item.ParLevelTarget = parTarget;
                    }

                    var requirementRoomType = ReadValue(cells, "RequirementRoomType");
                    var requirementUnits = ParseDecimal(ReadValue(cells, "RequirementUnits"));
                    if (!string.IsNullOrWhiteSpace(requirementRoomType) && requirementUnits > 0)
                    {
                        item.Requirements.Add(new SetupCsvRequirement
                        {
                            RoomTypeName = requirementRoomType.Trim(),
                            UnitsPerRoom = requirementUnits
                        });
                    }

                    nextItemOrder = sortOrder >= nextItemOrder ? sortOrder + 1 : nextItemOrder + 1;
                }
            }

            var normalizedItems = items.Values.ToList();
            foreach (var item in normalizedItems)
            {
                if (item.CaseCount <= 0)
                {
                    item.CaseCount = 1;
                }

                if (item.ParLevelTarget <= 0)
                {
                    item.ParLevelTarget = 1;
                }

                if (item.SortOrder <= 0)
                {
                    item.SortOrder = nextItemOrder++;
                }
            }

            foreach (var roomType in roomTypes.Where(rt => rt.SortOrder <= 0))
            {
                roomType.SortOrder = nextRoomTypeOrder++;
            }

            return (roomTypes, normalizedItems);
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            if (line == null)
            {
                return values;
            }

            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var character = line[i];

                if (character == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (character == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(character);
            }

            values.Add(current.ToString());
            return values;
        }

        private sealed class SetupCsvRoomType
        {
            public string Name { get; set; } = string.Empty;
            public int TotalRooms { get; set; }
            public int SortOrder { get; set; }
        }

        private sealed class SetupCsvItem
        {
            public string Name { get; set; } = string.Empty;
            public string? ItemNumber { get; set; }
            public decimal CaseCount { get; set; } = 1;
            public decimal CasePrice { get; set; }
            public decimal ParLevelTarget { get; set; } = 1;
            public int SortOrder { get; set; }
            public List<SetupCsvRequirement> Requirements { get; } = new();
        }

        private sealed class SetupCsvRequirement
        {
            public string RoomTypeName { get; set; } = string.Empty;
            public decimal UnitsPerRoom { get; set; }
        }

        private List<SupplyInventoryItemViewModel> LoadSupplyTemplate()
        {
            var items = new List<SupplyInventoryItemViewModel>();
            try
            {
                var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                    ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                    : _environment.WebRootPath!;

                var templatePath = Path.Combine(webRoot, "data", "supply-inventory-template.json");
                if (!System.IO.File.Exists(templatePath))
                {
                    _logger.LogWarning("Supply inventory template not found at {TemplatePath}", templatePath);
                    return items;
                }

                var json = System.IO.File.ReadAllText(templatePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return items;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var templateItems = JsonSerializer.Deserialize<List<SupplyInventoryTemplateItem>>(json, options);
                if (templateItems == null)
                {
                    return items;
                }

                foreach (var entry in templateItems)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var description = string.IsNullOrWhiteSpace(entry.Description)
                        ? (entry.Item ?? string.Empty)
                        : entry.Description.Trim();

                    items.Add(new SupplyInventoryItemViewModel
                    {
                        Item = string.IsNullOrWhiteSpace(entry.Item) ? description : entry.Item.Trim(),
                        Description = description,
                        PartNumber = entry.PartNumber ?? string.Empty,
                        Price = entry.Price,
                        QuantityPerCase = entry.QuantityPerCase
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load the supply inventory template.");
            }

            return items;
        }

        private sealed class SupplyInventoryTemplateItem
        {
            public string? Item { get; set; }
            public string? Description { get; set; }
            public string? PartNumber { get; set; }
            public decimal Price { get; set; }
            public decimal QuantityPerCase { get; set; }
        }
    }
}

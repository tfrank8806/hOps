#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Utilities;
using hOps.web.ViewModels.Housekeeping.LinenInventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class LinenInventoryController : BaseController
    {
        private readonly IUserTimeZoneService _timeZoneService;
        private readonly ILogger<LinenInventoryController> _logger;

        public LinenInventoryController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUserTimeZoneService timeZoneService,
            ILogger<LinenInventoryController> logger) : base(context, userManager)
        {
            _timeZoneService = timeZoneService;
            _logger = logger;
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
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
            }

            TempData["LinenInventoryMessage"] = "Updated the setup information.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SaveItems(LinenInventoryItemCollectionInput input)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before editing the linen items.";
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
            }

            TempData["LinenInventoryMessage"] = "Updated the linen items.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> LoadDefaults()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before loading the defaults.";
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Supply()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["LinenInventoryError"] = "Select a property before opening Supply Inventory.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.PropertyName = property.Name;
            return View();
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
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    public class ReportsController : BaseController
    {
        private const string PropertiesReportKey = "properties";
        private const string RoomsReportKey = "rooms";
        private const string RoomLayoutsReportKey = "roomLayouts";
        private const string UserAccessReportKey = "userAccess";
        private const string DepartmentsReportKey = "departments";
        private const string WorkOrderTypesReportKey = "workOrderTypes";
        private const string PhonebookTypesReportKey = "phonebookTypes";
        private const string CalendarCategoriesReportKey = "calendarCategories";
        private const string LostFoundReportKey = "lostFound";
        private const string WorkOrdersReportKey = "workOrders";
        private const string CalendarEventsReportKey = "calendarEvents";
        private const string PassOnLogsReportKey = "passOnLogs";
        private const string PhonebookReportKey = "phonebook";
        private const string BookmarksReportKey = "bookmarks";
        private const string PackageLogReportKey = "packageLog";

        private readonly IReadOnlyList<ReportDefinition> _reportDefinitions;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _reportDefinitions = new List<ReportDefinition>
            {
                new(PropertiesReportKey, "Property Directory", true, false,
                    "Summarizes the properties you can access, including location and room counts."),
                new(RoomsReportKey, "Room Inventory", true, false,
                    "Lists rooms for the selected properties with floor and type details."),
                new(RoomLayoutsReportKey, "Room Layout Coordinates", true, false,
                    "Shows layout positions for rooms that have been placed on a floor plan."),
                new(UserAccessReportKey, "User Property Access", true, false,
                    "Identifies which teammates can access each property."),
                new(LostFoundReportKey, "Lost & Found Report", true, true,
                    "Lost and found items captured across your selected properties."),
                new(WorkOrdersReportKey, "Work Orders Report", true, true,
                    "Detailed work order activity with property coverage and key metadata."),
                new(CalendarEventsReportKey, "Calendar Report", true, true,
                    "Calendar events scheduled for the chosen properties."),
                new(PassOnLogsReportKey, "Pass On Logs Report", true, true,
                    "Shift pass on logs including author and discussion threads."),
                new(PhonebookReportKey, "Phonebook Report", true, false,
                    "Phonebook contacts organized by property and type."),
                new(BookmarksReportKey, "Bookmarks Report", true, false,
                    "Property, team, and personal bookmarks available to you."),
                new(PackageLogReportKey, "Package & Mail Log Report", true, true,
                    "Package and mail tracking activity for your properties."),
                new(DepartmentsReportKey, "Departments", false, false,
                    "Reference list of departments configured for work orders and assignments."),
                new(WorkOrderTypesReportKey, "Work Order Types", false, false,
                    "Lookup values for work order categorization and routing."),
                new(PhonebookTypesReportKey, "Phonebook Types", false, false,
                    "Categories available when organizing the property phonebook."),
                new(CalendarCategoriesReportKey, "Calendar Categories", false, false,
                    "Shared calendar color codes for scheduling and events.")
            };
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var viewModel = await BuildViewModelAsync();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ReportRequestViewModel form)
        {
            if (form.StartDate.HasValue && form.EndDate.HasValue && form.StartDate > form.EndDate)
            {
                ModelState.AddModelError(nameof(form.EndDate), "End date must be on or after the start date.");
            }

            var selectedPropertyIds = form.SelectedPropertyIds ?? new List<int>();
            var viewModel = await BuildViewModelAsync(
                form.SelectedReportType,
                selectedPropertyIds,
                form.IncludeAllProperties,
                form.StartDate,
                form.EndDate,
                ModelState.IsValid);

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(ReportRequestViewModel form)
        {
            if (form.StartDate.HasValue && form.EndDate.HasValue && form.StartDate > form.EndDate)
            {
                ModelState.AddModelError(nameof(form.EndDate), "End date must be on or after the start date.");
            }

            var selectedPropertyIds = form.SelectedPropertyIds ?? new List<int>();
            var viewModel = await BuildViewModelAsync(
                form.SelectedReportType,
                selectedPropertyIds,
                form.IncludeAllProperties,
                form.StartDate,
                form.EndDate,
                ModelState.IsValid);

            if (viewModel.Result == null || !viewModel.Result.Rows.Any())
            {
                ModelState.AddModelError(string.Empty, "There is no data to export for the selected filters.");
                return View(nameof(Index), viewModel);
            }

            var fileName = $"{CreateSafeFileName(viewModel.Result.Title)}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            var content = BuildWorkbook(viewModel.Result);

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private async Task<ReportRequestViewModel> BuildViewModelAsync(
            string? selectedReportType = null,
            IEnumerable<int>? selectedPropertyIds = null,
            bool includeAllProperties = true,
            DateTime? startDate = null,
            DateTime? endDate = null,
            bool runReport = true)
        {
            var user = await _userManager.GetUserAsync(User);

            var accessibleProperties = new List<Property>();
            if (user != null)
            {
                accessibleProperties = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == user.Id)
                    .Include(upa => upa.Property)
                    .Select(upa => upa.Property)
                    .Where(property => property != null)
                    .Cast<Property>()
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }

            var accessiblePropertyIds = accessibleProperties.Select(p => p.Id).ToList();

            var definition = _reportDefinitions.FirstOrDefault(r => r.Key == selectedReportType);
            var supportsPropertyFilter = definition?.SupportsPropertyFilter ?? true;
            var supportsDateRange = definition?.SupportsDateRange ?? false;

            List<int> scopedPropertyIds;
            if (!supportsPropertyFilter)
            {
                includeAllProperties = true;
                scopedPropertyIds = accessiblePropertyIds;
            }
            else if (includeAllProperties || selectedPropertyIds == null)
            {
                scopedPropertyIds = accessiblePropertyIds;
            }
            else
            {
                scopedPropertyIds = selectedPropertyIds
                    .Where(accessiblePropertyIds.Contains)
                    .Distinct()
                    .ToList();
            }

            DateTime? normalizedStart = null;
            DateTime? normalizedEnd = null;
            if (supportsDateRange)
            {
                normalizedStart = startDate;
                normalizedEnd = endDate;

                if (normalizedStart.HasValue && normalizedEnd.HasValue && normalizedStart > normalizedEnd)
                {
                    (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
                }

                if (normalizedStart.HasValue && !normalizedEnd.HasValue)
                {
                    normalizedEnd = normalizedStart;
                }
                else if (!normalizedStart.HasValue && normalizedEnd.HasValue)
                {
                    normalizedStart = normalizedEnd;
                }

                if (!normalizedStart.HasValue && !normalizedEnd.HasValue && !string.IsNullOrWhiteSpace(selectedReportType))
                {
                    normalizedEnd = DateTime.UtcNow.Date;
                    normalizedStart = normalizedEnd.Value.AddDays(-29);
                }

                normalizedStart = normalizedStart?.Date;
                normalizedEnd = normalizedEnd?.Date;
            }

            var viewModel = new ReportRequestViewModel
            {
                SelectedReportType = selectedReportType,
                IncludeAllProperties = includeAllProperties,
                SelectedPropertyIds = scopedPropertyIds,
                AvailableReports = _reportDefinitions
                    .Select(r => new SelectListItem
                    {
                        Value = r.Key,
                        Text = r.DisplayName,
                        Selected = r.Key == selectedReportType
                    })
                    .ToList(),
                AvailableProperties = accessibleProperties
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id.ToString(CultureInfo.InvariantCulture),
                        Text = $"{p.Name} ({p.Code})",
                        Selected = scopedPropertyIds.Contains(p.Id)
                    })
                    .ToList(),
                SelectedReportDescription = definition?.Description,
                SelectedReportSupportsPropertyFilter = supportsPropertyFilter,
                SelectedReportSupportsDateRange = supportsDateRange,
                StartDate = normalizedStart,
                EndDate = normalizedEnd
            };

            if (runReport && !string.IsNullOrWhiteSpace(selectedReportType) && definition != null)
            {
                viewModel.Result = await RunReportAsync(definition, scopedPropertyIds, normalizedStart, normalizedEnd, user);
            }

            return viewModel;
        }

        private async Task<ReportResultViewModel> RunReportAsync(
            ReportDefinition definition,
            IReadOnlyCollection<int> propertyIds,
            DateTime? startDate,
            DateTime? endDate,
            ApplicationUser? currentUser)
        {
            switch (definition.Key)
            {
                case PropertiesReportKey:
                    return await BuildPropertiesReport(definition, propertyIds);
                case RoomsReportKey:
                    return await BuildRoomsReport(definition, propertyIds);
                case RoomLayoutsReportKey:
                    return await BuildRoomLayoutsReport(definition, propertyIds);
                case UserAccessReportKey:
                    return await BuildUserAccessReport(definition, propertyIds);
                case DepartmentsReportKey:
                    return await BuildDepartmentsReport(definition);
                case WorkOrderTypesReportKey:
                    return await BuildWorkOrderTypesReport(definition);
                case PhonebookTypesReportKey:
                    return await BuildPhonebookTypesReport(definition);
                case CalendarCategoriesReportKey:
                    return await BuildCalendarCategoriesReport(definition);
                case LostFoundReportKey:
                    return await BuildLostFoundReport(definition, propertyIds, startDate, endDate);
                case WorkOrdersReportKey:
                    return await BuildWorkOrdersReport(definition, propertyIds, startDate, endDate);
                case CalendarEventsReportKey:
                    return await BuildCalendarEventsReport(definition, propertyIds, startDate, endDate);
                case PassOnLogsReportKey:
                    return await BuildPassOnLogsReport(definition, propertyIds, startDate, endDate);
                case PhonebookReportKey:
                    return await BuildPhonebookReport(definition, propertyIds);
                case BookmarksReportKey:
                    return await BuildBookmarksReport(definition, propertyIds, currentUser);
                case PackageLogReportKey:
                    return await BuildPackageLogReport(definition, propertyIds, startDate, endDate);
                default:
                    return new ReportResultViewModel
                    {
                        Title = definition.DisplayName,
                        Headers = new List<string>(),
                        Rows = new List<IReadOnlyList<string>>()
                    };
            }
        }

        private async Task<ReportResultViewModel> BuildPropertiesReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var headers = new List<string> { "Property", "Code", "Address", "Rooms" };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var data = await _context.Properties
                .AsNoTracking()
                .Where(p => propertyIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Name,
                    p.Code,
                    p.Address,
                    RoomCount = p.Rooms.Count
                })
                .OrderBy(p => p.Name)
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = data
                    .Select(p => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(p.Name),
                        NormalizeText(p.Code),
                        NormalizeText(p.Address),
                        p.RoomCount.ToString(CultureInfo.InvariantCulture)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildRoomsReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var headers = new List<string> { "Property", "Code", "Room", "Floor", "Type", "Description" };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var data = await _context.Rooms
                .Include(r => r.Property)
                .AsNoTracking()
                .Where(r => propertyIds.Contains(r.PropertyId))
                .OrderBy(r => r.Property!.Name)
                .ThenBy(r => r.RoomNumber)
                .Select(r => new
                {
                    Property = r.Property!.Name,
                    r.Property!.Code,
                    r.RoomNumber,
                    r.Floor,
                    r.RoomType,
                    r.Description
                })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = data
                    .Select(r => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(r.Property),
                        NormalizeText(r.Code),
                        NormalizeText(r.RoomNumber),
                        r.Floor.ToString(CultureInfo.InvariantCulture),
                        NormalizeText(r.RoomType),
                        NormalizeText(r.Description)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildRoomLayoutsReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var headers = new List<string> { "Property", "Code", "Room", "Floor", "X", "Y", "Width", "Height", "Label" };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var data = await _context.RoomLayouts
                .AsNoTracking()
                .Where(rl => propertyIds.Contains(rl.PropertyId))
                .Join(_context.Rooms,
                    layout => layout.RoomId,
                    room => room.Id,
                    (layout, room) => new { layout, room })
                .Join(_context.Properties,
                    combined => combined.layout.PropertyId,
                    property => property.Id,
                    (combined, property) => new { combined.layout, combined.room, property })
                .OrderBy(x => x.property.Name)
                .ThenBy(x => x.room.RoomNumber)
                .Select(x => new
                {
                    Property = x.property.Name,
                    x.property.Code,
                    x.room.RoomNumber,
                    x.layout.Floor,
                    x.layout.X,
                    x.layout.Y,
                    x.layout.Width,
                    x.layout.Height,
                    x.layout.Label
                })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = data
                    .Select(x => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(x.Property),
                        NormalizeText(x.Code),
                        NormalizeText(x.RoomNumber),
                        x.Floor.ToString(CultureInfo.InvariantCulture),
                        x.X.ToString(CultureInfo.InvariantCulture),
                        x.Y.ToString(CultureInfo.InvariantCulture),
                        x.Width.ToString(CultureInfo.InvariantCulture),
                        x.Height.ToString(CultureInfo.InvariantCulture),
                        NormalizeText(x.Label)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildUserAccessReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var headers = new List<string> { "Property", "Code", "First Name", "Last Name", "Email" };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var data = await _context.UserPropertyAccesses
                .AsNoTracking()
                .Include(upa => upa.Property)
                .Include(upa => upa.ApplicationUser)
                .Where(upa => propertyIds.Contains(upa.PropertyId))
                .OrderBy(upa => upa.Property!.Name)
                .ThenBy(upa => upa.ApplicationUser!.LastName)
                .ThenBy(upa => upa.ApplicationUser!.FirstName)
                .Select(upa => new
                {
                    Property = upa.Property!.Name,
                    upa.Property!.Code,
                    upa.ApplicationUser!.FirstName,
                    upa.ApplicationUser!.LastName,
                    upa.ApplicationUser!.Email
                })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = data
                    .Select(x => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(x.Property),
                        NormalizeText(x.Code),
                        NormalizeText(x.FirstName),
                        NormalizeText(x.LastName),
                        NormalizeText(x.Email)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildDepartmentsReport(ReportDefinition definition)
        {
            var data = await _context.Departments
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new { d.Name, d.Color })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = new List<string> { "Department", "Color" },
                Rows = data
                    .Select(d => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(d.Name),
                        NormalizeText(d.Color)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildWorkOrderTypesReport(ReportDefinition definition)
        {
            var data = await _context.WorkOrderTypes
                .AsNoTracking()
                .OrderBy(w => w.Name)
                .Select(w => new { w.Name, w.Color })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = new List<string> { "Work Order Type", "Color" },
                Rows = data
                    .Select(w => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(w.Name),
                        NormalizeText(w.Color)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildPhonebookTypesReport(ReportDefinition definition)
        {
            var data = await _context.PhonebookTypes
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new { p.Name, p.Color })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = new List<string> { "Phonebook Type", "Color" },
                Rows = data
                    .Select(p => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(p.Name),
                        NormalizeText(p.Color)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildCalendarCategoriesReport(ReportDefinition definition)
        {
            var data = await _context.CalendarCategories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Name, c.Color })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = new List<string> { "Calendar Category", "Color" },
                Rows = data
                    .Select(c => (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(c.Name),
                        NormalizeText(c.Color)
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildLostFoundReport(
            ReportDefinition definition,
            IReadOnlyCollection<int> propertyIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            var headers = new List<string>
            {
                "Property",
                "Date Found/Lost",
                "Item Found/Lost",
                "Location",
                "Found By",
                "Storage Location",
                "Guest Name",
                "Guest Email",
                "Guest Phone",
                "Guest Address",
                "Notes"
            };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var (startBoundary, endBoundary) = ToRangeBounds(startDate, endDate);

            var query = _context.LostFoundEntries
                .Include(e => e.Property)
                .AsNoTracking()
                .Where(e => propertyIds.Contains(e.PropertyId));

            if (startBoundary.HasValue)
            {
                var startValue = startBoundary.Value;
                query = query.Where(e =>
                    (e.Type == LostFoundType.Found && e.DateFound.HasValue && e.DateFound.Value >= startValue) ||
                    (e.Type == LostFoundType.Lost && e.DateReportedLost.HasValue && e.DateReportedLost.Value >= startValue) ||
                    ((!e.DateFound.HasValue && !e.DateReportedLost.HasValue) && e.CreatedAt >= startValue));
            }

            if (endBoundary.HasValue)
            {
                var endValue = endBoundary.Value;
                query = query.Where(e =>
                    (e.Type == LostFoundType.Found && e.DateFound.HasValue && e.DateFound.Value < endValue) ||
                    (e.Type == LostFoundType.Lost && e.DateReportedLost.HasValue && e.DateReportedLost.Value < endValue) ||
                    ((!e.DateFound.HasValue && !e.DateReportedLost.HasValue) && e.CreatedAt < endValue));
            }

            var entries = await query
                .OrderBy(e => e.Property!.Name)
                .ThenByDescending(e => e.CreatedAt)
                .ToListAsync();

            var rows = entries
                .Select(e =>
                {
                    var relevantDate = e.Type == LostFoundType.Found
                        ? e.DateFound ?? e.CreatedAt
                        : e.DateReportedLost ?? e.CreatedAt;

                    var item = e.Type == LostFoundType.Found ? e.ItemFound : e.ItemLost;

                    return (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(e.Property?.Name),
                        FormatDate(relevantDate),
                        NormalizeText(item),
                        NormalizeText(e.Location),
                        NormalizeText(e.FoundBy),
                        NormalizeText(e.Stored),
                        NormalizeText(e.GuestName),
                        NormalizeText(e.GuestEmail),
                        NormalizeText(e.GuestPhone),
                        NormalizeText(e.GuestAddress),
                        NormalizeText(e.Notes)
                    };
                })
                .ToList();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = rows
            };
        }

        private async Task<ReportResultViewModel> BuildWorkOrdersReport(
            ReportDefinition definition,
            IReadOnlyCollection<int> propertyIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            var headers = new List<string>
            {
                "Property",
                "Location",
                "Type",
                "Date Created",
                "Issue",
                "Description",
                "Department",
                "Due Date",
                "Creator"
            };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var (startBoundary, endBoundary) = ToRangeBounds(startDate, endDate);

            var query = _context.WorkOrders
                .Include(wo => wo.Properties).ThenInclude(p => p.Property)
                .Include(wo => wo.WorkOrderType)
                .Include(wo => wo.Department)
                .Include(wo => wo.CreatedBy)
                .AsNoTracking()
                .Where(wo => wo.Properties.Any(p => propertyIds.Contains(p.PropertyId)));

            if (startBoundary.HasValue)
            {
                var startValue = startBoundary.Value;
                query = query.Where(wo => wo.CreatedAt >= startValue);
            }

            if (endBoundary.HasValue)
            {
                var endValue = endBoundary.Value;
                query = query.Where(wo => wo.CreatedAt < endValue);
            }

            var orders = await query
                .OrderBy(wo => wo.Properties
                    .Where(p => p.Property != null)
                    .Select(p => p.Property!.Name)
                    .OrderBy(name => name)
                    .FirstOrDefault())
                .ThenByDescending(wo => wo.CreatedAt)
                .ToListAsync();

            var rows = orders
                .Select(wo =>
                {
                    var propertyNames = wo.Properties
                        .Where(p => p.Property != null)
                        .Select(p => p.Property!.Name)
                        .Distinct()
                        .OrderBy(name => name)
                        .ToList();

                    var departmentName = wo.Department?.Name;
                    var creator = FormatUser(wo.CreatedBy);

                    return (IReadOnlyList<string>)new List<string>
                    {
                        propertyNames.Any() ? string.Join(Environment.NewLine, propertyNames) : "—",
                        NormalizeText(wo.Location),
                        NormalizeText(wo.WorkOrderType?.Name),
                        FormatDate(wo.CreatedAt),
                        NormalizeText(wo.Issue),
                        NormalizeText(wo.Details),
                        NormalizeText(departmentName),
                        FormatDate(wo.DueDate),
                        creator
                    };
                })
                .ToList();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = rows
            };
        }

        private async Task<ReportResultViewModel> BuildCalendarEventsReport(
            ReportDefinition definition,
            IReadOnlyCollection<int> propertyIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            var headers = new List<string>
            {
                "Property",
                "Category",
                "Title",
                "Start Date",
                "Start Time",
                "End Date",
                "End Time",
                "Recurring",
                "Details"
            };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var (startBoundary, endBoundary) = ToRangeBounds(startDate, endDate);

            var query = _context.CalendarEvents
                .Include(e => e.Category)
                .Include(e => e.EventProperties).ThenInclude(ep => ep.Property)
                .AsNoTracking()
                .Where(e => e.EventProperties.Any(ep => propertyIds.Contains(ep.PropertyId)));

            if (startBoundary.HasValue)
            {
                var startValue = startBoundary.Value;
                query = query.Where(e => e.StartDate >= startValue);
            }

            if (endBoundary.HasValue)
            {
                var endValue = endBoundary.Value;
                query = query.Where(e => e.StartDate < endValue);
            }

            var events = await query
                .OrderBy(e => e.StartDate)
                .ThenBy(e => e.Title)
                .ToListAsync();

            var rows = events
                .Select(e =>
                {
                    var properties = e.EventProperties
                        .Where(ep => ep.Property != null)
                        .Select(ep => ep.Property!.Name)
                        .Distinct()
                        .OrderBy(name => name)
                        .ToList();

                    return (IReadOnlyList<string>)new List<string>
                    {
                        properties.Any() ? string.Join(Environment.NewLine, properties) : "—",
                        NormalizeText(e.Category?.Name),
                        NormalizeText(e.Title),
                        FormatDate(e.StartDate),
                        FormatTime(e.StartTime),
                        FormatDate(e.EndDate),
                        FormatTime(e.EndTime),
                        GetEnumDisplayName(e.Recurrence),
                        NormalizeText(e.Details)
                    };
                })
                .ToList();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = rows
            };
        }

        private async Task<ReportResultViewModel> BuildPassOnLogsReport(
            ReportDefinition definition,
            IReadOnlyCollection<int> propertyIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            var headers = new List<string>
            {
                "Property",
                "Date Created",
                "Log Title",
                "Body",
                "Creator",
                "Comments"
            };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var (startBoundary, endBoundary) = ToRangeBounds(startDate, endDate);

            var query = _context.PassOnLogs
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.CreatedBy)
                .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                .AsNoTracking()
                .Where(l => l.Properties.Any(lp => propertyIds.Contains(lp.PropertyId)));

            if (startBoundary.HasValue)
            {
                var startValue = startBoundary.Value;
                query = query.Where(l => l.CreatedAt >= startValue);
            }

            if (endBoundary.HasValue)
            {
                var endValue = endBoundary.Value;
                query = query.Where(l => l.CreatedAt < endValue);
            }

            var logs = await query
                .OrderByDescending(l => l.CreatedAt)
                .ThenBy(l => l.Title)
                .ToListAsync();

            var rows = logs
                .Select(l =>
                {
                    var propertyNames = l.Properties
                        .Where(lp => lp.Property != null)
                        .Select(lp => lp.Property!.Name)
                        .Distinct()
                        .OrderBy(name => name)
                        .ToList();

                    var comments = l.Comments
                        .OrderBy(c => c.CreatedAt)
                        .Select(c =>
                        {
                            var author = FormatUser(c.CreatedBy);
                            return $"{FormatDateTime(c.CreatedAt)} - {author}: {c.Body}";
                        })
                        .ToList();

                    return (IReadOnlyList<string>)new List<string>
                    {
                        propertyNames.Any() ? string.Join(Environment.NewLine, propertyNames) : "—",
                        FormatDate(l.CreatedAt),
                        NormalizeText(l.Title),
                        NormalizeText(l.Body),
                        FormatUser(l.CreatedBy),
                        comments.Any() ? string.Join(Environment.NewLine, comments) : "—"
                    };
                })
                .ToList();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = rows
            };
        }

        private async Task<ReportResultViewModel> BuildPhonebookReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var headers = new List<string>
            {
                "Property",
                "Type",
                "Company",
                "First Name",
                "Last Name",
                "Title",
                "Email",
                "Phone Number",
                "Fax Number",
                "Website",
                "Address",
                "Notes",
                "Created by"
            };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var contacts = await _context.PhonebookContacts
                .Include(c => c.PhonebookType).ThenInclude(t => t.Property)
                .AsNoTracking()
                .Where(c => c.PhonebookType != null && c.PhonebookType.PropertyId.HasValue && propertyIds.Contains(c.PhonebookType.PropertyId.Value))
                .OrderBy(c => c.PhonebookType != null && c.PhonebookType.Property != null ? c.PhonebookType.Property.Name : string.Empty)
                .ThenBy(c => c.TypeName)
                .ThenBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToListAsync();

            var rows = contacts
                .Select(c => (IReadOnlyList<string>)new List<string>
                {
                    NormalizeText(c.PhonebookType?.Property?.Name),
                    NormalizeText(c.TypeName),
                    NormalizeText(c.Company),
                    NormalizeText(c.FirstName),
                    NormalizeText(c.LastName),
                    NormalizeText(c.Title),
                    NormalizeText(c.Email),
                    NormalizeText(c.PhoneNumber),
                    NormalizeText(c.Fax),
                    NormalizeText(c.Website),
                    NormalizeText(c.Address),
                    NormalizeText(c.Notes),
                    "—"
                })
                .ToList();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = rows
            };
        }

        private async Task<ReportResultViewModel> BuildBookmarksReport(
            ReportDefinition definition,
            IReadOnlyCollection<int> propertyIds,
            ApplicationUser? currentUser)
        {
            var headers = new List<string>
            {
                "Property",
                "Property Bookmarks",
                "Description",
                "URL",
                "Team Bookmarks",
                "Description",
                "URL",
                "Your Bookmarks",
                "Description",
                "URL"
            };

            var query = _context.Bookmarks
                .Include(b => b.Property)
                .Include(b => b.CreatedBy)
                .AsNoTracking()
                .AsQueryable();

            if (propertyIds.Any())
            {
                query = query.Where(b => !b.PropertyId.HasValue || propertyIds.Contains(b.PropertyId.Value));
            }
            else
            {
                query = query.Where(b => !b.PropertyId.HasValue);
            }

            if (currentUser != null)
            {
                query = query.Where(b => b.Section != BookmarkSection.User || b.CreatedById == currentUser.Id);
            }
            else
            {
                query = query.Where(b => b.Section != BookmarkSection.User);
            }

            var bookmarks = await query
                .OrderBy(b => b.Property == null ? 1 : 0)
                .ThenBy(b => b.Property!.Name)
                .ThenBy(b => b.Section)
                .ThenBy(b => b.Name)
                .ToListAsync();

            var rows = bookmarks
                .Select(b =>
                {
                    var propertyName = b.Property?.Name ?? (b.Section == BookmarkSection.User ? "Personal" : "—");

                    return (IReadOnlyList<string>)new List<string>
                    {
                        NormalizeText(propertyName),
                        FormatValueOrEmpty(b.Section == BookmarkSection.Property, b.Name),
                        FormatValueOrEmpty(b.Section == BookmarkSection.Property, b.Description),
                        FormatValueOrEmpty(b.Section == BookmarkSection.Property, b.Url),
                        FormatValueOrEmpty(b.Section == BookmarkSection.Team, b.Name),
                        FormatValueOrEmpty(b.Section == BookmarkSection.Team, b.Description),
                        FormatValueOrEmpty(b.Section == BookmarkSection.Team, b.Url),
                        FormatValueOrEmpty(b.Section == BookmarkSection.User, b.Name),
                        FormatValueOrEmpty(b.Section == BookmarkSection.User, b.Description),
                        FormatValueOrEmpty(b.Section == BookmarkSection.User, b.Url)
                    };
                })
                .ToList();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = rows
            };
        }

        private async Task<ReportResultViewModel> BuildPackageLogReport(
            ReportDefinition definition,
            IReadOnlyCollection<int> propertyIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            var headers = new List<string>
            {
                "Property",
                "Room Number",
                "Recipient",
                "Arrival Date",
                "Departure Date",
                "Carrier",
                "Tracking Number",
                "Storage Location",
                "Notes",
                "Delivered?",
                "Delivered Date/Time"
            };

            if (!propertyIds.Any())
            {
                return new ReportResultViewModel
                {
                    Title = definition.DisplayName,
                    Headers = headers,
                    Rows = new List<IReadOnlyList<string>>()
                };
            }

            var (startBoundary, endBoundary) = ToRangeBounds(startDate, endDate);

            var query = _context.PackageLogEntries
                .Include(e => e.Property)
                .AsNoTracking()
                .Where(e => propertyIds.Contains(e.PropertyId));

            if (startBoundary.HasValue)
            {
                var startValue = startBoundary.Value;
                query = query.Where(e => e.LoggedAt >= startValue);
            }

            if (endBoundary.HasValue)
            {
                var endValue = endBoundary.Value;
                query = query.Where(e => e.LoggedAt < endValue);
            }

            var entries = await query
                .OrderByDescending(e => e.LoggedAt)
                .ThenBy(e => e.RecipientName)
                .ToListAsync();

            var rows = entries
                .Select(e => (IReadOnlyList<string>)new List<string>
                {
                    NormalizeText(e.Property?.Name),
                    NormalizeText(e.RoomNumber),
                    NormalizeText(e.RecipientName),
                    FormatDate(e.ArrivalDate),
                    FormatDate(e.DepartureDate),
                    NormalizeText(e.Carrier),
                    NormalizeText(e.TrackingNumber),
                    NormalizeText(e.StorageLocation),
                    NormalizeText(e.Notes),
                    e.Delivered ? "Yes" : "No",
                    FormatDateTime(e.DeliveredAt)
                })
                .ToList();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = headers,
                Rows = rows
            };
        }

        private static byte[] BuildWorkbook(ReportResultViewModel result)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(TruncateWorksheetName(result.Title));

            for (var i = 0; i < result.Headers.Count; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = result.Headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }

            for (var rowIndex = 0; rowIndex < result.Rows.Count; rowIndex++)
            {
                var row = result.Rows[rowIndex];
                for (var columnIndex = 0; columnIndex < result.Headers.Count && columnIndex < row.Count; columnIndex++)
                {
                    var cell = worksheet.Cell(rowIndex + 2, columnIndex + 1);
                    cell.Value = row[columnIndex];
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    cell.Style.Alignment.WrapText = true;
                }
            }

            var lastRow = Math.Max(1, result.Rows.Count + 1);
            var lastColumn = Math.Max(1, result.Headers.Count);
            var usedRange = worksheet.Range(1, 1, lastRow, lastColumn);
            usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            usedRange.SetAutoFilter();
            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns(1, lastColumn).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private static string FormatValueOrEmpty(bool include, string? value)
        {
            return include ? NormalizeText(value) : string.Empty;
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—";
        }

        private static string FormatDateTime(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "—";
        }

        private static string FormatTime(TimeSpan? value)
        {
            return value.HasValue ? value.Value.ToString(@"hh\:mm", CultureInfo.InvariantCulture) : "—";
        }

        private static string FormatUser(ApplicationUser? user)
        {
            if (user == null)
            {
                return "—";
            }

            var parts = new[] { user.FirstName, user.LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .ToArray();

            if (parts.Length > 0)
            {
                return string.Join(" ", parts);
            }

            return string.IsNullOrWhiteSpace(user.Email) ? "—" : user.Email!;
        }

        private static string CreateSafeFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "report";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = title
                .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
                .ToArray();

            var cleaned = new string(sanitized)
                .Trim()
                .Replace(' ', '-');

            cleaned = string.Join("-", cleaned
                .Split('-', StringSplitOptions.RemoveEmptyEntries));

            return string.IsNullOrWhiteSpace(cleaned) ? "report" : cleaned.ToLowerInvariant();
        }

        private static string TruncateWorksheetName(string title)
        {
            var safe = CreateSafeFileName(string.IsNullOrWhiteSpace(title) ? "Report" : title);
            return safe.Length <= 31 ? safe : safe.Substring(0, 31);
        }

        private static string GetEnumDisplayName(Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            if (member != null)
            {
                var displayAttribute = member.GetCustomAttribute<DisplayAttribute>();
                if (!string.IsNullOrWhiteSpace(displayAttribute?.Name))
                {
                    return displayAttribute!.Name!;
                }
            }

            return value.ToString();
        }

        private static (DateTime? StartInclusive, DateTime? EndExclusive) ToRangeBounds(DateTime? start, DateTime? end)
        {
            if (start.HasValue && end.HasValue && start > end)
            {
                (start, end) = (end, start);
            }

            var normalizedStart = start?.Date;
            var normalizedEnd = end?.Date.AddDays(1);

            return (normalizedStart, normalizedEnd);
        }

        private record ReportDefinition(string Key, string DisplayName, bool SupportsPropertyFilter, bool SupportsDateRange, string Description);
    }
}

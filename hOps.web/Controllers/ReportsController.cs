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

        private readonly IReadOnlyList<ReportDefinition> _reportDefinitions;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _reportDefinitions = new List<ReportDefinition>
            {
                new(PropertiesReportKey, "Property Directory", supportsPropertyFilter: true,
                    "Summarizes the properties you can access, including location and room counts."),
                new(RoomsReportKey, "Room Inventory", supportsPropertyFilter: true,
                    "Lists rooms for the selected properties with floor and type details."),
                new(RoomLayoutsReportKey, "Room Layout Coordinates", supportsPropertyFilter: true,
                    "Shows layout positions for rooms that have been placed on a floor plan."),
                new(UserAccessReportKey, "User Property Access", supportsPropertyFilter: true,
                    "Identifies which teammates can access each property."),
                new(DepartmentsReportKey, "Departments", supportsPropertyFilter: false,
                    "Reference list of departments configured for work orders and assignments."),
                new(WorkOrderTypesReportKey, "Work Order Types", supportsPropertyFilter: false,
                    "Lookup values for work order categorization and routing."),
                new(PhonebookTypesReportKey, "Phonebook Types", supportsPropertyFilter: false,
                    "Categories available when organizing the property phonebook."),
                new(CalendarCategoriesReportKey, "Calendar Categories", supportsPropertyFilter: false,
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
            var selectedPropertyIds = form.SelectedPropertyIds ?? new List<int>();
            var viewModel = await BuildViewModelAsync(form.SelectedReportType, selectedPropertyIds, form.IncludeAllProperties);

            ModelState.Clear();
            return View(viewModel);
        }

        private async Task<ReportRequestViewModel> BuildViewModelAsync(
            string? selectedReportType = null,
            IEnumerable<int>? selectedPropertyIds = null,
            bool includeAllProperties = true)
        {
            var user = await _userManager.GetUserAsync(User);

            var accessibleProperties = new List<Property>();
            if (user != null)
            {
                accessibleProperties = await _context.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == user.Id)
                    .Include(upa => upa.Property)
                    .Select(upa => upa.Property)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }

            var accessiblePropertyIds = accessibleProperties.Select(p => p.Id).ToList();

            var definition = _reportDefinitions.FirstOrDefault(r => r.Key == selectedReportType);

            var supportsPropertyFilter = definition?.SupportsPropertyFilter ?? true;

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
                        Value = p.Id.ToString(),
                        Text = $"{p.Name} ({p.Code})",
                        Selected = scopedPropertyIds.Contains(p.Id)
                    })
                    .ToList(),
                SelectedReportDescription = definition?.Description,
                SelectedReportSupportsPropertyFilter = supportsPropertyFilter
            };

            if (!string.IsNullOrWhiteSpace(selectedReportType) && definition != null)
            {
                viewModel.Result = await RunReportAsync(definition, scopedPropertyIds);
            }

            return viewModel;
        }

        private async Task<ReportResultViewModel> RunReportAsync(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
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
            var query = _context.Properties.AsQueryable();
            if (propertyIds.Any())
            {
                query = query.Where(p => propertyIds.Contains(p.Id));
            }

            var data = await query
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
                Headers = new List<string> { "Property", "Code", "Address", "Rooms" },
                Rows = data
                    .Select(p => (IReadOnlyList<string>)new List<string>
                    {
                        p.Name,
                        p.Code,
                        string.IsNullOrWhiteSpace(p.Address) ? "—" : p.Address!,
                        p.RoomCount.ToString()
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildRoomsReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var query = _context.Rooms
                .Include(r => r.Property)
                .AsQueryable();

            if (propertyIds.Any())
            {
                query = query.Where(r => propertyIds.Contains(r.PropertyId));
            }

            var data = await query
                .OrderBy(r => r.Property.Name)
                .ThenBy(r => r.RoomNumber)
                .Select(r => new
                {
                    Property = r.Property.Name,
                    r.Property.Code,
                    r.RoomNumber,
                    r.Floor,
                    r.RoomType,
                    r.Description
                })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = new List<string> { "Property", "Code", "Room", "Floor", "Type", "Description" },
                Rows = data
                    .Select(r => (IReadOnlyList<string>)new List<string>
                    {
                        r.Property,
                        r.Code,
                        r.RoomNumber,
                        r.Floor.ToString(),
                        r.RoomType,
                        string.IsNullOrWhiteSpace(r.Description) ? "—" : r.Description!
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildRoomLayoutsReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var query = _context.RoomLayouts.AsQueryable();
            if (propertyIds.Any())
            {
                query = query.Where(rl => propertyIds.Contains(rl.PropertyId));
            }

            var data = await query
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
                Headers = new List<string> { "Property", "Code", "Room", "Floor", "X", "Y", "Width", "Height", "Label" },
                Rows = data
                    .Select(x => (IReadOnlyList<string>)new List<string>
                    {
                        x.Property,
                        x.Code,
                        x.RoomNumber,
                        x.Floor.ToString(),
                        x.X.ToString(),
                        x.Y.ToString(),
                        x.Width.ToString(),
                        x.Height.ToString(),
                        string.IsNullOrWhiteSpace(x.Label) ? "—" : x.Label!
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildUserAccessReport(ReportDefinition definition, IReadOnlyCollection<int> propertyIds)
        {
            var query = _context.UserPropertyAccesses.AsQueryable();
            if (propertyIds.Any())
            {
                query = query.Where(upa => propertyIds.Contains(upa.PropertyId));
            }

            var data = await query
                .Include(upa => upa.Property)
                .Include(upa => upa.ApplicationUser)
                .OrderBy(upa => upa.Property.Name)
                .ThenBy(upa => upa.ApplicationUser.LastName)
                .ThenBy(upa => upa.ApplicationUser.FirstName)
                .Select(upa => new
                {
                    Property = upa.Property.Name,
                    upa.Property.Code,
                    upa.ApplicationUser.FirstName,
                    upa.ApplicationUser.LastName,
                    upa.ApplicationUser.Email
                })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = new List<string> { "Property", "Code", "First Name", "Last Name", "Email" },
                Rows = data
                    .Select(x => (IReadOnlyList<string>)new List<string>
                    {
                        x.Property,
                        x.Code,
                        string.IsNullOrWhiteSpace(x.FirstName) ? "—" : x.FirstName!,
                        string.IsNullOrWhiteSpace(x.LastName) ? "—" : x.LastName!,
                        string.IsNullOrWhiteSpace(x.Email) ? "—" : x.Email!
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildDepartmentsReport(ReportDefinition definition)
        {
            var data = await _context.Departments
                .OrderBy(d => d.Name)
                .Select(d => new
                {
                    d.Name,
                    d.Color
                })
                .ToListAsync();

            return new ReportResultViewModel
            {
                Title = definition.DisplayName,
                Headers = new List<string> { "Department", "Color" },
                Rows = data
                    .Select(d => (IReadOnlyList<string>)new List<string>
                    {
                        string.IsNullOrWhiteSpace(d.Name) ? "—" : d.Name!,
                        d.Color
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildWorkOrderTypesReport(ReportDefinition definition)
        {
            var data = await _context.WorkOrderTypes
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
                        w.Name,
                        w.Color
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildPhonebookTypesReport(ReportDefinition definition)
        {
            var data = await _context.PhonebookTypes
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
                        p.Name,
                        p.Color
                    })
                    .ToList()
            };
        }

        private async Task<ReportResultViewModel> BuildCalendarCategoriesReport(ReportDefinition definition)
        {
            var data = await _context.CalendarCategories
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
                        c.Name,
                        c.Color
                    })
                    .ToList()
            };
        }

        private record ReportDefinition(string Key, string DisplayName, bool SupportsPropertyFilter, string Description);
    }
}

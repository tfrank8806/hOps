using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class WorkOrdersController : BaseController
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WorkOrdersController> _logger;
        private readonly MentionService _mentionService;

        public WorkOrdersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<WorkOrdersController> logger,
            MentionService mentionService) : base(context, userManager)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _mentionService = mentionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] WorkOrderFilterInput filters)
        {
            var viewModel = await BuildViewModelAsync(filters, null);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] WorkOrderFilterInput filters)
        {
            var viewModel = await BuildViewModelAsync(filters, null);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Work Orders");

            var headers = new[]
            {
                "Status",
                "Location",
                "Department",
                "Type",
                "Issue",
                "Due Date",
                "Created Date",
                "Creator",
                "Properties",
                "Attachments"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            for (var index = 0; index < viewModel.WorkOrders.Count; index++)
            {
                var rowNumber = index + 2;
                var order = viewModel.WorkOrders[index];

                worksheet.Cell(rowNumber, 1).Value = order.Status;
                worksheet.Cell(rowNumber, 2).Value = order.Location;
                worksheet.Cell(rowNumber, 3).Value = order.Department ?? "Unassigned";
                worksheet.Cell(rowNumber, 4).Value = order.WorkOrderType ?? string.Empty;
                worksheet.Cell(rowNumber, 5).Value = order.Issue;
                worksheet.Cell(rowNumber, 6).Value = order.DueDate;
                worksheet.Cell(rowNumber, 7).Value = order.CreatedAt.ToLocalTime();
                worksheet.Cell(rowNumber, 8).Value = string.IsNullOrWhiteSpace(order.Creator) ? "Unknown" : order.Creator;
                worksheet.Cell(rowNumber, 9).Value = string.Join(Environment.NewLine, order.Properties);
                worksheet.Cell(rowNumber, 10).Value = string.Join(Environment.NewLine, order.Attachments.Select(a => a.FileName));

                worksheet.Cell(rowNumber, 6).Style.DateFormat.Format = "MMM dd, yyyy";
                worksheet.Cell(rowNumber, 7).Style.DateFormat.Format = "MMM dd, yyyy";
                worksheet.Row(rowNumber).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            }

            worksheet.Columns().AdjustToContents();
            worksheet.Rows().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"work-orders-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Form")] WorkOrderFormViewModel form)
        {
            var filters = new WorkOrderFilterInput();
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);

            if (!accessiblePropertyIds.Any())
            {
                ModelState.AddModelError(string.Empty, "You do not have permission to create work orders.");
            }

            var selectedPropertyIds = form.SelectedPropertyIds?.Where(id => accessiblePropertyIds.Contains(id)).Distinct().ToList() ?? new List<int>();

            if (!selectedPropertyIds.Any())
            {
                if (accessiblePropertyIds.Count == 1)
                {
                    selectedPropertyIds = accessiblePropertyIds;
                }
                else
                {
                    ModelState.AddModelError("Form.SelectedPropertyIds", "Please select at least one property.");
                }
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildViewModelAsync(filters, form);
                return View("Index", invalidModel);
            }

            var workOrder = new WorkOrder
            {
                Status = form.Status,
                Location = form.Location ?? string.Empty,
                WorkOrderTypeId = form.WorkOrderTypeId,
                Issue = form.Issue,
                Details = form.Details,
                DueDate = form.DueDate,
                DepartmentId = form.DepartmentId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = user?.Id
            };

            foreach (var propertyId in selectedPropertyIds)
            {
                workOrder.Properties.Add(new WorkOrderProperty
                {
                    PropertyId = propertyId
                });
            }

            if (form.Photos != null && form.Photos.Count > 0)
            {
                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "workorders");
                Directory.CreateDirectory(uploadPath);

                foreach (var file in form.Photos)
                {
                    if (file.Length <= 0) continue;

                    var extension = Path.GetExtension(file.FileName);
                    var fileName = $"{Guid.NewGuid()}{extension}";
                    var fullPath = Path.Combine(uploadPath, fileName);

                    using (var stream = System.IO.File.Create(fullPath))
                    {
                        await file.CopyToAsync(stream);
                    }

                    workOrder.Attachments.Add(new WorkOrderAttachment
                    {
                        FilePath = Path.Combine("/uploads/workorders", fileName).Replace("\\", "/"),
                        OriginalFileName = file.FileName
                    });
                }
            }

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            var workOrderLink = Url.Action(nameof(Index), "WorkOrders", new { highlight = workOrder.Id }, Request.Scheme)
                ?? Url.Action(nameof(Index), "WorkOrders") ?? "/WorkOrders";

            await _mentionService.CreateMentionNotificationsAsync(
                $"{workOrder.Issue}\n{workOrder.Details}",
                user!,
                $"Work Order #{workOrder.Id}",
                workOrderLink,
                workOrder.Issue);

            if (form.DepartmentId.HasValue)
            {
                _logger.LogInformation("Work order {WorkOrderId} assigned to department {DepartmentId}", workOrder.Id, form.DepartmentId);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<int>> GetAccessiblePropertyIdsAsync(ApplicationUser? user)
        {
            if (user == null)
            {
                return new List<int>();
            }

            return await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Select(upa => upa.PropertyId)
                .Distinct()
                .ToListAsync();
        }

        private async Task<WorkOrdersViewModel> BuildViewModelAsync(WorkOrderFilterInput filters, WorkOrderFormViewModel? form)
        {
            var user = await _userManager.GetUserAsync(User);
            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var statuses = WorkOrderStatusOptions.All;
            var statusColorMap = statuses.ToDictionary(s => s.Key, s => s.ColorHex, StringComparer.OrdinalIgnoreCase);
            var defaultStatus = _configuration.GetValue<string>("WorkOrders:DefaultStatus") ?? statuses.First().Key;

            if (string.IsNullOrWhiteSpace(filters.SortOrder))
            {
                filters.SortOrder = "newest";
            }

            if (filters.PropertyId.HasValue && !accessiblePropertyIds.Contains(filters.PropertyId.Value))
            {
                filters.PropertyId = null;
            }

            var selectedPropertyIds = form?.SelectedPropertyIds != null
                ? new HashSet<int>(form.SelectedPropertyIds.Where(id => accessiblePropertyIds.Contains(id)))
                : new HashSet<int>();

            var query = _context.WorkOrders
                .Include(w => w.WorkOrderType)
                .Include(w => w.Department)
                .Include(w => w.CreatedBy)
                .Include(w => w.Properties).ThenInclude(p => p.Property)
                .Include(w => w.Attachments)
                .AsQueryable();

            if (accessiblePropertyIds.Any())
            {
                query = query.Where(w => w.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)));
            }
            else
            {
                query = query.Where(_ => false);
            }

            if (!string.IsNullOrWhiteSpace(filters.RoomNumber))
            {
                var roomFilter = filters.RoomNumber.Trim();
                query = query.Where(w => w.Location.Contains(roomFilter));
            }

            if (filters.DepartmentId.HasValue)
            {
                query = query.Where(w => w.DepartmentId == filters.DepartmentId.Value);
            }

            if (filters.WorkOrderTypeId.HasValue)
            {
                query = query.Where(w => w.WorkOrderTypeId == filters.WorkOrderTypeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filters.Status))
            {
                query = query.Where(w => w.Status == filters.Status);
            }

            if (!string.IsNullOrWhiteSpace(filters.CreatorId))
            {
                query = query.Where(w => w.CreatedById == filters.CreatorId);
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var term = filters.Search.Trim();
                query = query.Where(w =>
                    EF.Functions.Like(w.Issue, $"%{term}%") ||
                    EF.Functions.Like(w.Details ?? string.Empty, $"%{term}%") ||
                    EF.Functions.Like(w.Location, $"%{term}%"));
            }

            if (filters.PropertyId.HasValue)
            {
                var propertyId = filters.PropertyId.Value;
                query = query.Where(w => w.Properties.Any(p => p.PropertyId == propertyId));
            }

            query = filters.SortOrder?.ToLowerInvariant() switch
            {
                "oldest" => query.OrderBy(w => w.CreatedAt),
                _ => query.OrderByDescending(w => w.CreatedAt)
            };

            var workOrders = await query.ToListAsync();

            var listItems = workOrders.Select(wo => new WorkOrderListItemViewModel
            {
                Id = wo.Id,
                Status = wo.Status,
                StatusColor = WorkOrderStatusOptions.GetColor(wo.Status),
                Location = wo.Location,
                WorkOrderType = wo.WorkOrderType?.Name,
                Issue = wo.Issue,
                Details = wo.Details,
                DueDate = wo.DueDate,
                CreatedAt = wo.CreatedAt,
                Department = wo.Department?.Name,
                DepartmentColor = wo.Department?.Color,
                Creator = wo.CreatedBy != null
                    ? string.Join(" ", new[] { wo.CreatedBy.FirstName, wo.CreatedBy.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : null,
                Properties = wo.Properties.Select(p => $"{p.Property.Name} ({p.Property.Code})").ToList(),
                Attachments = wo.Attachments.Select(a => new WorkOrderAttachmentViewModel
                {
                    FilePath = a.FilePath,
                    FileName = string.IsNullOrWhiteSpace(a.OriginalFileName) ? Path.GetFileName(a.FilePath) : a.OriginalFileName
                }).ToList()
            }).ToList();

            var departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            var workOrderTypes = await _context.WorkOrderTypes.OrderBy(t => t.Name).ToListAsync();

            var propertyOptions = await _context.Properties
                .Where(p => accessiblePropertyIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .Select(p => new PropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsSelected = false
                })
                .ToListAsync();

            if (selectedPropertyIds.Count > 0)
            {
                foreach (var option in propertyOptions)
                {
                    option.IsSelected = selectedPropertyIds.Contains(option.Id);
                }
            }

            if (selectedPropertyIds.Count == 0 && propertyOptions.Count == 1)
            {
                propertyOptions[0].IsSelected = true;
            }

            var creatorOptions = workOrders
                .Where(wo => !string.IsNullOrWhiteSpace(wo.CreatedById) && wo.CreatedBy != null)
                .Select(wo => new
                {
                    wo.CreatedById,
                    Name = string.Join(" ", new[] { wo.CreatedBy!.FirstName, wo.CreatedBy!.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
                })
                .GroupBy(x => x.CreatedById)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.CreatedById!,
                    Text = string.IsNullOrWhiteSpace(x.Name) ? "Unknown" : x.Name
                })
                .ToList();

            var locationSuggestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roomSuggestions = await _context.Rooms
                .Where(r => accessiblePropertyIds.Contains(r.PropertyId))
                .Select(r => r.RoomNumber)
                .Distinct()
                .ToListAsync();
            foreach (var room in roomSuggestions.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                locationSuggestions.Add(room);
            }

            foreach (var loc in workOrders.Select(wo => wo.Location).Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                locationSuggestions.Add(loc);
            }

            var currentProperty = ViewBag.CurrentProperty as Property;

            var effectiveForm = form ?? new WorkOrderFormViewModel
            {
                Status = defaultStatus,
                DueDate = DateTime.Today
            };

            if (string.IsNullOrWhiteSpace(effectiveForm.Status))
            {
                effectiveForm.Status = defaultStatus;
            }

            if (!effectiveForm.SelectedPropertyIds.Any())
            {
                if (propertyOptions.Any(po => po.IsSelected))
                {
                    effectiveForm.SelectedPropertyIds = propertyOptions.Where(po => po.IsSelected).Select(po => po.Id).ToList();
                }
                else if (currentProperty != null)
                {
                    effectiveForm.SelectedPropertyIds = new List<int> { currentProperty.Id };
                    var match = propertyOptions.FirstOrDefault(p => p.Id == currentProperty.Id);
                    if (match != null)
                    {
                        match.IsSelected = true;
                    }
                }
            }

            var viewModel = new WorkOrdersViewModel
            {
                WorkOrders = listItems,
                Filters = filters,
                Form = effectiveForm,
                StatusOptions = statuses.ToList(),
                Departments = departments,
                WorkOrderTypes = workOrderTypes,
                PropertyOptions = propertyOptions,
                CreatorOptions = creatorOptions,
                LocationSuggestions = locationSuggestions.OrderBy(x => x).ToList(),
                StatusColorMap = statusColorMap
            };

            return viewModel;
        }
    }
}


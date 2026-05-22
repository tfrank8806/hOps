using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.WorkOrders;
using hOps.web.Utilities;
using hOps.web.Services.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class WorkOrdersController : BaseController
    {
        private static readonly string[] StatusProgression = new[]
        {
            "New",
            "In Progress",
            "Escalated",
            "On Hold",
            "Completed",
            "Cancelled"
        };
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WorkOrdersController> _logger;
        private readonly MentionService _mentionService;
        private readonly IEmailSender _emailSender;
        private readonly IUserTimeZoneService _timeZoneService;
        private readonly ITranslationService _translationService;

        public WorkOrdersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<WorkOrdersController> logger,
            MentionService mentionService,
            IEmailSender emailSender,
            IUserTimeZoneService timeZoneService,
            ITranslationService translationService) : base(context, userManager)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _mentionService = mentionService;
            _emailSender = emailSender;
            _timeZoneService = timeZoneService;
            _translationService = translationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] WorkOrderFilterInput filters)
        {
            var viewModel = await BuildViewModelAsync(filters, null);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, [FromQuery] WorkOrderFilterInput filters)
        {
            var workOrder = await _context.WorkOrders
                .Include(w => w.Properties)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);

            if (!workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            var form = new WorkOrderFormViewModel
            {
                Id = workOrder.Id,
                Status = workOrder.Status,
                Location = workOrder.Location,
                WorkOrderTypeId = workOrder.WorkOrderTypeId,
                Issue = workOrder.Issue,
                Details = workOrder.Details,
                DueDate = workOrder.DueDate,
                DepartmentId = workOrder.DepartmentId,
                AssignedUserId = workOrder.AssignedToUserId,
                SelectedPropertyIds = workOrder.Properties.Select(p => p.PropertyId).ToList()
            };

            var viewModel = await BuildViewModelAsync(filters, form);
            viewModel.EditingWorkOrderId = workOrder.Id;

            return View("Index", viewModel);
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
                "Assigned To",
                "Type",
                "Issue",
                "Details",
                "Due Date",
                "Created Date",
                "Creator",
                "Properties",
                "Attachments"
            };

            var activeLanguage = HttpContext?.Items?["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

            for (var i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                if (!isDefaultLanguage)
                {
                    header = _translationService.Translate(header, activeLanguage, header);
                }
                worksheet.Cell(1, i + 1).Value = header;
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            for (var index = 0; index < viewModel.WorkOrders.Count; index++)
            {
                var rowNumber = index + 2;
                var order = viewModel.WorkOrders[index];

                var statusLabel = WorkOrderStatusOptions.GetLabel(order.Status ?? string.Empty);
                if (!isDefaultLanguage)
                {
                    statusLabel = _translationService.Translate(statusLabel, activeLanguage, statusLabel);
                }

                var locationValue = order.Location ?? string.Empty;
                var issueValue = order.Issue ?? string.Empty;
                var detailsValue = order.Details ?? string.Empty;
                if (!isDefaultLanguage)
                {
                    var entityId = order.Id.ToString(CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(locationValue))
                    {
                        locationValue = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Location",
                            locationValue,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(issueValue))
                    {
                        issueValue = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Issue",
                            issueValue,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(detailsValue))
                    {
                        detailsValue = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Details",
                            detailsValue,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }
                }

                worksheet.Cell(rowNumber, 1).Value = statusLabel;
                worksheet.Cell(rowNumber, 2).Value = locationValue;

                var departmentValue = order.Department ?? (isDefaultLanguage
                    ? "Unassigned"
                    : _translationService.Translate("Unassigned", activeLanguage, "Unassigned"));
                var assignedValue = string.IsNullOrWhiteSpace(order.AssignedToName)
                    ? (isDefaultLanguage ? "Unassigned" : _translationService.Translate("Unassigned", activeLanguage, "Unassigned"))
                    : order.AssignedToName;

                worksheet.Cell(rowNumber, 3).Value = departmentValue;
                worksheet.Cell(rowNumber, 4).Value = assignedValue;
                worksheet.Cell(rowNumber, 5).Value = order.WorkOrderType ?? string.Empty;
                worksheet.Cell(rowNumber, 6).Value = issueValue;
                worksheet.Cell(rowNumber, 7).Value = detailsValue;
                worksheet.Cell(rowNumber, 8).Value = order.DueDate;
                var createdLocal = _timeZoneService.ConvertToUserTime(order.CreatedAt);
                worksheet.Cell(rowNumber, 9).Value = createdLocal;
                var creatorValue = string.IsNullOrWhiteSpace(order.Creator)
                    ? (isDefaultLanguage ? "Unknown" : _translationService.Translate("Unknown", activeLanguage, "Unknown"))
                    : order.Creator;
                worksheet.Cell(rowNumber, 10).Value = creatorValue;
                worksheet.Cell(rowNumber, 11).Value = string.Join(Environment.NewLine, order.Properties);
                worksheet.Cell(rowNumber, 12).Value = string.Join(Environment.NewLine, order.Attachments.Select(a => a.FileName));

                worksheet.Cell(rowNumber, 8).Style.DateFormat.Format = "MMM dd, yyyy";
                worksheet.Cell(rowNumber, 9).Style.DateFormat.Format = "MMM dd, yyyy";
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
                    selectedPropertyIds = new List<int> { accessiblePropertyIds.First() };
                }
                else
                {
                    ModelState.AddModelError("Form.SelectedPropertyIds", "Please select at least one property.");
                }
            }

            form.SelectedPropertyIds = selectedPropertyIds;
            form.AdditionalLocations ??= new List<string>();

            var assignableUsers = await GetAssignableUsersAsync(selectedPropertyIds);
            var assignableUserIds = new HashSet<string>(assignableUsers.Select(u => u.UserId), StringComparer.OrdinalIgnoreCase);
            var trimmedAssignee = string.IsNullOrWhiteSpace(form.AssignedUserId)
                ? null
                : form.AssignedUserId!.Trim();
            form.AssignedUserId = trimmedAssignee;

            if (!form.DepartmentId.HasValue && string.IsNullOrWhiteSpace(trimmedAssignee))
            {
                var assignmentError = "Select a department or an individual to assign this work order.";
                ModelState.AddModelError("Form.DepartmentId", assignmentError);
                ModelState.AddModelError("Form.AssignedUserId", assignmentError);
            }

            if (!string.IsNullOrWhiteSpace(trimmedAssignee) && !assignableUserIds.Contains(trimmedAssignee))
            {
                ModelState.AddModelError("Form.AssignedUserId", "Selected user does not have access to the selected properties.");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildViewModelAsync(filters, form);
                return View("Index", invalidModel);
            }

            var submittedLocations = ExtractSubmittedLocations(form);
            foreach (var location in submittedLocations)
            {
                await CreateWorkOrderAsync(form, user!, selectedPropertyIds, location);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetRoomWorkOrders(int propertyId, string roomNumber)
        {
            if (propertyId <= 0 || string.IsNullOrWhiteSpace(roomNumber))
            {
                return BadRequest(new { message = "Property and room information are required." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            if (!accessiblePropertyIds.Contains(propertyId))
            {
                return Forbid();
            }

            var trimmedRoom = roomNumber.Trim();

            var openOrders = await _context.WorkOrders
                .Where(wo => (wo.Status == "New" || wo.Status == "In Progress") &&
                             wo.Properties.Any(p => p.PropertyId == propertyId))
                .OrderByDescending(wo => wo.CreatedAt)
                .Select(wo => new
                {
                    wo.Id,
                    wo.Status,
                    wo.Issue,
                    wo.Location,
                    wo.CreatedAt,
                    DepartmentId = wo.DepartmentId,
                    DepartmentName = wo.Department != null ? wo.Department.Name : null,
                    DepartmentColor = wo.Department != null ? wo.Department.Color : null
                })
                .AsNoTracking()
                .ToListAsync();

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext.RequestAborted;
            var unassignedLabel = "Unassigned";

            var matchingOrders = openOrders
                .Where(wo => MatchesRoom(trimmedRoom, wo.Location))
                .Select(wo =>
                {
                    var departmentColor = string.IsNullOrWhiteSpace(wo.DepartmentColor) ? "#dc3545" : wo.DepartmentColor!;
                    var detailUrl = Url.Action(nameof(Edit), new { id = wo.Id }) ?? Url.Action(nameof(Index)) ?? "#";
                    var createdDisplay = _timeZoneService.ConvertToUserTime(wo.CreatedAt)
                        .ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture);
                    var statusLabel = WorkOrderStatusOptions.GetLabel(wo.Status ?? string.Empty);

                    return new RoomWorkOrderSummary
                    {
                        Id = wo.Id,
                        Status = wo.Status ?? string.Empty,
                        StatusLabel = statusLabel,
                        Issue = wo.Issue ?? string.Empty,
                        Location = wo.Location ?? string.Empty,
                        DepartmentId = wo.DepartmentId,
                        DepartmentName = string.IsNullOrWhiteSpace(wo.DepartmentName) ? unassignedLabel : wo.DepartmentName!,
                        DepartmentColor = departmentColor,
                        CreatedAtDisplay = createdDisplay,
                        DetailUrl = detailUrl
                    };
                })
                .ToList();

            if (!isDefaultLanguage && matchingOrders.Count > 0)
            {
                foreach (var order in matchingOrders)
                {
                    var entityId = order.Id.ToString(CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(order.Issue))
                    {
                        var translatedIssue = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Issue",
                            order.Issue,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        order.Issue = string.IsNullOrWhiteSpace(translatedIssue) ? order.Issue : translatedIssue;
                    }

                    if (!string.IsNullOrWhiteSpace(order.Location))
                    {
                        var translatedLocation = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Location",
                            order.Location,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        order.Location = string.IsNullOrWhiteSpace(translatedLocation) ? order.Location : translatedLocation;
                    }

                    if (order.DepartmentId.HasValue && !string.IsNullOrWhiteSpace(order.DepartmentName) && !string.Equals(order.DepartmentName, unassignedLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        var translatedDepartment = await _translationService.TranslateDynamicAsync(
                            "Department",
                            order.DepartmentId.Value.ToString(CultureInfo.InvariantCulture),
                            "Name",
                            order.DepartmentName,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translatedDepartment))
                        {
                            order.DepartmentName = translatedDepartment;
                        }
                    }
                    else
                    {
                        order.DepartmentName = _translationService.Translate("WorkOrders.Unassigned", activeLanguage, order.DepartmentName);
                    }

                    if (!string.IsNullOrWhiteSpace(order.StatusLabel))
                    {
                        order.StatusLabel = _translationService.Translate(order.StatusLabel, activeLanguage, order.StatusLabel);
                    }
                }
            }
            else if (matchingOrders.Count > 0)
            {
                foreach (var order in matchingOrders)
                {
                    if (!order.DepartmentId.HasValue || string.IsNullOrWhiteSpace(order.DepartmentName) || string.Equals(order.DepartmentName, unassignedLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        order.DepartmentName = unassignedLabel;
                    }
                }
            }

            return Json(new
            {
                workOrders = matchingOrders.Select(order => new
                {
                    id = order.Id,
                    status = order.Status,
                    statusLabel = order.StatusLabel,
                    issue = order.Issue,
                    location = order.Location,
                    departmentName = order.DepartmentName,
                    departmentColor = order.DepartmentColor,
                    createdAt = order.CreatedAtDisplay,
                    detailUrl = order.DetailUrl
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromLayout([FromBody] LayoutWorkOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kvp => kvp.Value?.Errors?.Any() == true)
                    .Select(kvp => new
                    {
                        field = kvp.Key,
                        message = kvp.Value!.Errors.First().ErrorMessage
                    })
                    .ToList();

                return BadRequest(new { message = "Please correct the highlighted fields.", errors });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            if (!accessiblePropertyIds.Contains(request.PropertyId))
            {
                return Forbid();
            }

            var issue = request.Issue?.Trim();
            if (string.IsNullOrWhiteSpace(issue))
            {
                return BadRequest(new { message = "Issue description is required." });
            }

            var defaultStatus = _configuration.GetValue<string>("WorkOrders:DefaultStatus") ?? WorkOrderStatusOptions.All.First().Key;

            var form = new WorkOrderFormViewModel
            {
                Status = defaultStatus,
                Location = string.IsNullOrWhiteSpace(request.RoomLabel) ? request.RoomNumber.Trim() : request.RoomLabel!.Trim(),
                WorkOrderTypeId = request.WorkOrderTypeId,
                Issue = issue,
                Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
                DueDate = NormalizeUtcDate((request.DueDate ?? DateTime.UtcNow.Date).Date),
                DepartmentId = request.DepartmentId,
                SelectedPropertyIds = new List<int> { request.PropertyId }
            };

            var workOrder = await CreateWorkOrderAsync(form, user, form.SelectedPropertyIds);

            return Ok(new
            {
                workOrder.Id,
                workOrder.Status,
                workOrder.Issue
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] WorkOrderFormViewModel form)
        {
            if (!form.Id.HasValue || form.Id.Value != id)
            {
                return BadRequest();
            }

            var filters = new WorkOrderFilterInput();
            var user = await _userManager.GetUserAsync(User);
            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);

            var workOrder = await _context.WorkOrders
                .Include(w => w.Properties)
                .Include(w => w.Attachments)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            if (!workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            var selectedPropertyIds = form.SelectedPropertyIds?
                .Where(pid => accessiblePropertyIds.Contains(pid))
                .Distinct()
                .ToList() ?? new List<int>();

            if (!selectedPropertyIds.Any())
            {
                ModelState.AddModelError("Form.SelectedPropertyIds", "Please select at least one property.");
            }

            form.SelectedPropertyIds = selectedPropertyIds;

            var assignableUsers = await GetAssignableUsersAsync(selectedPropertyIds);
            var assignableUserIds = new HashSet<string>(assignableUsers.Select(u => u.UserId), StringComparer.OrdinalIgnoreCase);
            var trimmedAssignee = string.IsNullOrWhiteSpace(form.AssignedUserId)
                ? null
                : form.AssignedUserId!.Trim();
            form.AssignedUserId = trimmedAssignee;

            if (!form.DepartmentId.HasValue && string.IsNullOrWhiteSpace(trimmedAssignee))
            {
                var assignmentError = "Select a department or an individual to assign this work order.";
                ModelState.AddModelError("Form.DepartmentId", assignmentError);
                ModelState.AddModelError("Form.AssignedUserId", assignmentError);
            }

            if (!string.IsNullOrWhiteSpace(trimmedAssignee) && !assignableUserIds.Contains(trimmedAssignee))
            {
                ModelState.AddModelError("Form.AssignedUserId", "Selected user does not have access to the selected properties.");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildViewModelAsync(filters, form);
                invalidModel.EditingWorkOrderId = id;
                return View("Index", invalidModel);
            }

            workOrder.Status = form.Status;
            workOrder.Location = form.Location ?? string.Empty;
            workOrder.WorkOrderTypeId = form.WorkOrderTypeId;
            workOrder.Issue = form.Issue;
            workOrder.Details = form.Details;
            workOrder.DueDate = NormalizeUtcDate(form.DueDate);
            workOrder.DepartmentId = form.DepartmentId;
            workOrder.AssignedToUserId = string.IsNullOrWhiteSpace(form.AssignedUserId) ? null : form.AssignedUserId;

            var accessiblePropertySet = new HashSet<int>(accessiblePropertyIds);
            var toRemove = workOrder.Properties
                .Where(p => accessiblePropertySet.Contains(p.PropertyId) && !selectedPropertyIds.Contains(p.PropertyId))
                .ToList();
            foreach (var property in toRemove)
            {
                workOrder.Properties.Remove(property);
                _context.WorkOrderProperties.Remove(property);
            }

            var remainingPropertyIds = workOrder.Properties.Select(p => p.PropertyId).ToHashSet();
            foreach (var propertyId in selectedPropertyIds)
            {
                if (remainingPropertyIds.Contains(propertyId))
                {
                    continue;
                }

                workOrder.Properties.Add(new WorkOrderProperty
                {
                    PropertyId = propertyId
                });
                remainingPropertyIds.Add(propertyId);
            }

            if (form.Photos != null && form.Photos.Count > 0)
            {
                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "workorders");
                Directory.CreateDirectory(uploadPath);

                foreach (var file in form.Photos)
                {
                    if (file.Length <= 0)
                    {
                        continue;
                    }

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

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { highlight = workOrder.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdvanceStatus(int id, string? status, string? completionNotes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            if (!accessiblePropertyIds.Any())
            {
                return Forbid();
            }

            var workOrder = await _context.WorkOrders
                .Include(w => w.Properties)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            if (!workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            string nextStatus;
            if (!string.IsNullOrWhiteSpace(status))
            {
                nextStatus = StatusProgression.FirstOrDefault(s =>
                    s.Equals(status, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

                if (string.IsNullOrEmpty(nextStatus))
                {
                    return BadRequest("Invalid status selected.");
                }
            }
            else
            {
                var currentIndex = Array.FindIndex(StatusProgression, currentStatus =>
                    currentStatus.Equals(workOrder.Status, StringComparison.OrdinalIgnoreCase) ||
                    (currentStatus.Equals("New", StringComparison.OrdinalIgnoreCase) && workOrder.Status.Equals("Open", StringComparison.OrdinalIgnoreCase)));

                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }

                nextStatus = StatusProgression[(currentIndex + 1) % StatusProgression.Length];
            }

            var alreadyAtStatus = workOrder.Status.Equals(nextStatus, StringComparison.OrdinalIgnoreCase) ||
                (nextStatus.Equals("New", StringComparison.OrdinalIgnoreCase) && workOrder.Status.Equals("Open", StringComparison.OrdinalIgnoreCase));

            var shouldSave = false;

            if (!alreadyAtStatus)
            {
                workOrder.Status = nextStatus;
                shouldSave = true;
            }

            var trimmedNotes = string.IsNullOrWhiteSpace(completionNotes) ? null : completionNotes.Trim();
            var isCompleting = nextStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase);

            if (isCompleting)
            {
                if (!string.Equals(workOrder.CompletionNotes ?? string.Empty, trimmedNotes ?? string.Empty, StringComparison.Ordinal))
                {
                    workOrder.CompletionNotes = trimmedNotes;
                    shouldSave = true;
                }
            }
            else if (!string.IsNullOrWhiteSpace(workOrder.CompletionNotes))
            {
                workOrder.CompletionNotes = null;
                shouldSave = true;
            }

            if (shouldSave)
            {
                await _context.SaveChangesAsync();
            }

            return RedirectWithFilters(workOrder.Id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = user != null
                ? await _userManager.GetRolesAsync(user)
                : new List<string>();

            if (!HasManagementPrivileges(roles))
            {
                return Forbid();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);

            var workOrder = await _context.WorkOrders
                .Include(w => w.Properties)
                .Include(w => w.Attachments)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            if (!workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            var attachmentPaths = workOrder.Attachments
                .Select(a => a.FilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            _context.WorkOrders.Remove(workOrder);
            await _context.SaveChangesAsync();

            DeleteAttachmentFiles(attachmentPaths);

            return RedirectWithFilters();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteDepartmentWorkOrder(int id, string? returnUrl, string? completionNotes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var departmentIds = await _context.UserDepartmentSubscriptions
                .Where(s => s.UserId == user.Id)
                .Select(s => s.DepartmentId)
                .ToListAsync();

            if (!departmentIds.Any())
            {
                TempData["ToDoError"] = "You are not assigned to any departments.";
                return RedirectBack(returnUrl);
            }

            var workOrder = await _context.WorkOrders
                .Include(w => w.Properties)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            if (!workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            if (!workOrder.DepartmentId.HasValue || !departmentIds.Contains(workOrder.DepartmentId.Value))
            {
                return Forbid();
            }

            var trimmedNotes = string.IsNullOrWhiteSpace(completionNotes) ? null : completionNotes.Trim();
            var statusChanged = !string.Equals(workOrder.Status, "Completed", StringComparison.OrdinalIgnoreCase);
            var notesChanged = !string.Equals(workOrder.CompletionNotes ?? string.Empty, trimmedNotes ?? string.Empty, StringComparison.Ordinal);

            if (statusChanged)
            {
                workOrder.Status = "Completed";
            }

            if (notesChanged)
            {
                workOrder.CompletionNotes = trimmedNotes;
            }

            if (statusChanged || notesChanged)
            {
                await _context.SaveChangesAsync();
                TempData["ToDoMessage"] = $"Marked work order #{id} complete.";
            }
            else
            {
                TempData["ToDoMessage"] = $"Work order #{id} is already completed.";
            }

            return RedirectBack(returnUrl, new { highlight = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPersonalTodo(string title, string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["ToDoError"] = "Please enter a description for your to-do item.";
                return RedirectBack(returnUrl);
            }

            var trimmed = title.Trim();
            if (trimmed.Length > 256)
            {
                trimmed = trimmed[..256];
            }

            _context.UserToDoItems.Add(new UserToDoItem
            {
                Title = trimmed,
                UserId = user.Id
            });
            await _context.SaveChangesAsync();

            TempData["ToDoMessage"] = "Added a new to-do item.";
            return RedirectBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePersonalTodo(int id, string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var todo = await _context.UserToDoItems
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (todo == null)
            {
                return NotFound();
            }

            todo.IsCompleted = !todo.IsCompleted;
            todo.CompletedAtUtc = todo.IsCompleted ? DateTime.UtcNow : null;
            await _context.SaveChangesAsync();

            TempData["ToDoMessage"] = todo.IsCompleted
                ? "Marked to-do item complete."
                : "Reopened to-do item.";
            return RedirectBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePersonalTodo(int id, string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var todo = await _context.UserToDoItems
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (todo == null)
            {
                return NotFound();
            }

            _context.UserToDoItems.Remove(todo);
            await _context.SaveChangesAsync();

            TempData["ToDoMessage"] = "Removed the to-do item.";
            return RedirectBack(returnUrl);
        }

        private async Task<WorkOrder> CreateWorkOrderAsync(
            WorkOrderFormViewModel form,
            ApplicationUser user,
            IReadOnlyCollection<int> propertyIds,
            string? locationOverride = null)
        {
            var resolvedLocation = locationOverride ?? form.Location;
            var normalizedLocation = string.IsNullOrWhiteSpace(resolvedLocation)
                ? string.Empty
                : resolvedLocation.Trim();

            var workOrder = new WorkOrder
            {
                Status = form.Status,
                Location = normalizedLocation,
                WorkOrderTypeId = form.WorkOrderTypeId,
                Issue = form.Issue,
                Details = form.Details,
                DueDate = NormalizeUtcDate(form.DueDate),
                DepartmentId = form.DepartmentId,
                AssignedToUserId = string.IsNullOrWhiteSpace(form.AssignedUserId) ? null : form.AssignedUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = user.Id
            };

            foreach (var propertyId in propertyIds)
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
                    if (file.Length <= 0)
                    {
                        continue;
                    }

                    var extension = Path.GetExtension(file.FileName);
                    var fileName = $"{Guid.NewGuid()}{extension}";
                    var fullPath = Path.Combine(uploadPath, fileName);

                    using var stream = System.IO.File.Create(fullPath);
                    await file.CopyToAsync(stream);

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
                ?? Url.Action(nameof(Index), "WorkOrders")
                ?? "/WorkOrders";

            await _mentionService.CreateMentionNotificationsAsync(
                $"{workOrder.Issue}\n{workOrder.Details}",
                user,
                $"Work Order #{workOrder.Id}",
                workOrderLink,
                workOrder.Issue);

            await SendDepartmentAlertEmailsAsync(workOrder, workOrderLink, user);

            if (form.DepartmentId.HasValue)
            {
                _logger.LogInformation("Work order {WorkOrderId} assigned to department {DepartmentId}", workOrder.Id, form.DepartmentId);
            }
            if (!string.IsNullOrWhiteSpace(form.AssignedUserId))
            {
                _logger.LogInformation("Work order {WorkOrderId} assigned to user {UserId}", workOrder.Id, form.AssignedUserId);
            }

            return workOrder;
        }

        private static List<string> ExtractSubmittedLocations(WorkOrderFormViewModel form)
        {
            var locations = new List<string>();

            void TryAdd(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                var trimmed = value.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    locations.Add(trimmed);
                }
            }

            TryAdd(form.Location);

            if (form.AdditionalLocations != null)
            {
                foreach (var extra in form.AdditionalLocations)
                {
                    TryAdd(extra);
                }
            }

            if (!locations.Any())
            {
                locations.Add(string.Empty);
            }

            return locations;
        }

        private async Task SendDepartmentAlertEmailsAsync(WorkOrder workOrder, string workOrderLink, ApplicationUser? createdBy)
        {
            if (!workOrder.DepartmentId.HasValue)
            {
                return;
            }

            await _context.Entry(workOrder).Reference(w => w.Department).LoadAsync();
            await _context.Entry(workOrder)
                .Collection(w => w.Properties)
                .Query()
                .Include(p => p.Property)
                .LoadAsync();

            var departmentId = workOrder.DepartmentId.Value;
            var workOrderPropertyIds = workOrder.Properties
                .Select(p => p.PropertyId)
                .Where(pid => pid > 0)
                .Distinct()
                .ToList();

            var recipientCandidates = await _context.Users
                .Where(u => u.EmailOnWorkOrderDepartment
                            && u.DepartmentEmailSubscriptions.Any(s => s.DepartmentId == departmentId)
                            && !string.IsNullOrWhiteSpace(u.Email)
                            && (createdBy == null || u.Id != createdBy.Id))
                .Select(u => new
                {
                    User = u,
                    PropertyPreferences = u.EmailPropertySubscriptions.Select(s => new { s.PropertyId, s.IncludeInWorkOrderAlerts }),
                    AccessIds = u.UserPropertyAccesses!.Select(upa => upa.PropertyId)
                })
                .ToListAsync();

            var recipients = recipientCandidates
                .Where(r =>
                {
                    var preferenceIds = r.PropertyPreferences
                        .Where(p => p.IncludeInWorkOrderAlerts)
                        .Select(p => p.PropertyId)
                        .ToHashSet();

                    if (!preferenceIds.Any())
                    {
                        preferenceIds = r.AccessIds.ToHashSet();
                    }

                    if (!preferenceIds.Any())
                    {
                        return false;
                    }

                    if (!workOrderPropertyIds.Any())
                    {
                        return true;
                    }

                    return workOrderPropertyIds.Any(id => preferenceIds.Contains(id));
                })
                .Select(r => r.User)
                .ToList();

            var notificationUserIds = await _context.UserDepartmentSubscriptions
                .Where(s => s.DepartmentId == departmentId)
                .Select(s => s.UserId)
                .Where(id => createdBy == null || id != createdBy.Id)
                .Distinct()
                .ToListAsync();

            var departmentName = workOrder.Department?.Name ?? "department";

            if (notificationUserIds.Any())
            {
                await CreateDepartmentNotificationsAsync(notificationUserIds, workOrder, workOrderLink, departmentName);
            }

            if (!recipients.Any())
            {
                return;
            }

            var subject = $"New work order #{workOrder.Id} assigned to {departmentName}";
            var actorName = BuildUserDisplayName(createdBy);
            var safeActor = WebUtility.HtmlEncode(actorName);
            var safeDepartment = WebUtility.HtmlEncode(departmentName);
            var safeIssue = WebUtility.HtmlEncode(workOrder.Issue ?? string.Empty);
            var safeLocation = string.IsNullOrWhiteSpace(workOrder.Location) ? null : WebUtility.HtmlEncode(workOrder.Location);
            var dueDateValue = workOrder.DueDate;

            var detailPreview = string.IsNullOrWhiteSpace(workOrder.Details) ? null : workOrder.Details.Trim();
            if (!string.IsNullOrWhiteSpace(detailPreview) && detailPreview.Length > 500)
            {
                detailPreview = $"{detailPreview[..500]}A??'A+??TA???sA,A_A??'A??,???A???sA,A?A??'A??,???A???sA,A?";
            }
            var safeDetails = string.IsNullOrWhiteSpace(detailPreview)
                ? null
                : WebUtility.HtmlEncode(detailPreview).Replace("\r\n", "\n").Replace("\n", "<br/>");

            var propertyNames = workOrder.Properties
                .Select(p => p.Property?.Name ?? $"Property #{p.PropertyId}")
                .Distinct()
                .ToList();
            var safeProperties = propertyNames.Any()
                ? string.Join(", ", propertyNames.Select(WebUtility.HtmlEncode))
                : null;

            foreach (var recipient in recipients)
            {
                try
                {
                    var recipientTimeZone = ResolveUserTimeZone(recipient);
                    var dueDateText = FormatDueDate(dueDateValue, recipientTimeZone);
                    var safeDueDate = WebUtility.HtmlEncode(dueDateText);

                    var bodyBuilder = new StringBuilder();
                    bodyBuilder.AppendLine($"<p>{safeActor} created a new work order assigned to <strong>{safeDepartment}</strong>.</p>");
                    bodyBuilder.AppendLine($"<p><strong>Issue:</strong> {safeIssue}</p>");
                    if (safeLocation != null)
                    {
                        bodyBuilder.AppendLine($"<p><strong>Location:</strong> {safeLocation}</p>");
                    }
                    bodyBuilder.AppendLine($"<p><strong>Due Date:</strong> {safeDueDate}</p>");
                    if (safeProperties != null)
                    {
                        bodyBuilder.AppendLine($"<p><strong>Properties:</strong> {safeProperties}</p>");
                    }
                    if (safeDetails != null)
                    {
                        bodyBuilder.AppendLine($"<p><strong>Details:</strong><br/>{safeDetails}</p>");
                    }
                    bodyBuilder.AppendLine($"<p><a href=\"{workOrderLink}\">Open work order</a></p>");

                    await _emailSender.SendEmailAsync(recipient.Email!, subject, bodyBuilder.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send work order email notification to user {UserId}", recipient.Id);
                }
            }
        }

        private async Task CreateDepartmentNotificationsAsync(
            IEnumerable<string> userIds,
            WorkOrder workOrder,
            string workOrderLink,
            string departmentName)
        {
            var now = DateTime.UtcNow;
            var locationText = string.IsNullOrWhiteSpace(workOrder.Location)
                ? null
                : workOrder.Location.Trim();
            var locationPrefix = string.IsNullOrWhiteSpace(locationText)
                ? string.Empty
                : $"Room {locationText} • ";
            var notificationTitle = $"{locationPrefix}New {departmentName} work order".Trim();
            var notificationContent = string.IsNullOrWhiteSpace(workOrder.Issue)
                ? $"Work order #{workOrder.Id}"
                : workOrder.Issue;
            if (!string.IsNullOrWhiteSpace(locationText))
            {
                notificationContent = $"Room {locationText}: {notificationContent}";
            }

            foreach (var userId in userIds)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = userId,
                    Type = "workorder",
                    Title = notificationTitle,
                    Content = notificationContent,
                    LinkUrl = workOrderLink,
                    CreatedAt = now,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();
        }

        private static TimeZoneInfo ResolveUserTimeZone(ApplicationUser? user)
        {
            var normalized = DefaultTimeZoneProvider.NormalizeForStorage(user?.TimeZoneId);
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(normalized);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static string FormatDueDate(DateTime dueDate, TimeZoneInfo timeZone)
        {
            if (dueDate == default)
            {
                return "Not set";
            }

            var normalized = dueDate.Kind switch
            {
                DateTimeKind.Local => dueDate.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
                _ => dueDate
            };

            var local = TimeZoneInfo.ConvertTimeFromUtc(normalized, timeZone);
            var offset = new DateTimeOffset(local, timeZone.GetUtcOffset(local));
            return offset.ToString("MMM d, yyyy h:mm tt zzz");
        }

        private static string BuildUserDisplayName(ApplicationUser? user)
        {
            if (user == null)
            {
                return "A teammate";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName))
            {
                parts.Add(user.FirstName);
            }
            if (!string.IsNullOrWhiteSpace(user.LastName))
            {
                parts.Add(user.LastName);
            }

            if (parts.Count > 0)
            {
                return string.Join(" ", parts);
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email!;
            }

            return "A teammate";
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

        private async Task<List<AssignableUserOption>> GetAssignableUsersAsync(IEnumerable<int> propertyIds)
        {
            var targetIds = propertyIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!targetIds.Any())
            {
                return new List<AssignableUserOption>();
            }

            var candidates = await _context.UserPropertyAccesses
                .Where(upa => targetIds.Contains(upa.PropertyId))
                .Select(upa => new
                {
                    upa.ApplicationUserId,
                    upa.ApplicationUser.FirstName,
                    upa.ApplicationUser.LastName,
                    upa.ApplicationUser.Email
                })
                .Where(x => x.ApplicationUserId != null)
                .ToListAsync();

            return candidates
                .GroupBy(x => x.ApplicationUserId)
                .Select(group =>
                {
                    var user = group.First();
                    var displayName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = user.Email ?? "Team Member";
                    }
                    return new AssignableUserOption
                    {
                        UserId = user.ApplicationUserId!,
                        DisplayName = displayName
                    };
                })
                .OrderBy(option => option.DisplayName)
                .ToList();
        }
        private async Task<WorkOrdersViewModel> BuildViewModelAsync(WorkOrderFilterInput? filters, WorkOrderFormViewModel? form)
        {
            filters ??= new WorkOrderFilterInput();
            filters.Normalize();

            var user = await _userManager.GetUserAsync(User);
            var userRoles = user != null
                ? await _userManager.GetRolesAsync(user)
                : new List<string>();
            var canManage = HasManagementPrivileges(userRoles);
            var accessiblePropertyIds = await GetAccessiblePropertyIdsAsync(user);
            var canUpdateStatus = accessiblePropertyIds.Any();
            var statuses = WorkOrderStatusOptions.All;
            var statusColorMap = statuses.ToDictionary(s => s.Key, s => s.ColorHex, StringComparer.OrdinalIgnoreCase);
            var defaultStatus = _configuration.GetValue<string>("WorkOrders:DefaultStatus") ?? statuses.First().Key;

            var currentPropertyId = GetCurrentPropertyId();
            var normalizedPropertyFilters = filters.PropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!normalizedPropertyFilters.Any() && currentPropertyId.HasValue && accessiblePropertyIds.Contains(currentPropertyId.Value))
            {
                normalizedPropertyFilters.Add(currentPropertyId.Value);
            }

            filters.PropertyIds = normalizedPropertyFilters;

            var targetPropertyIds = normalizedPropertyFilters.Any()
                ? normalizedPropertyFilters
                : accessiblePropertyIds.ToList();
            var targetPropertySet = new HashSet<int>(targetPropertyIds);

            var query = _context.WorkOrders
                .Include(w => w.WorkOrderType)
                .Include(w => w.Department)
                .Include(w => w.CreatedBy)
                .Include(w => w.AssignedTo)
                .Include(w => w.Properties).ThenInclude(p => p.Property)
                .Include(w => w.Attachments)
                .AsQueryable();

            if (targetPropertySet.Any())
            {
                query = query.Where(w => w.Properties.Any(p => targetPropertySet.Contains(p.PropertyId)));
            }
            else
            {
                query = query.Where(_ => false);
            }

            if (!string.IsNullOrEmpty(filters.RoomNumber))
            {
                query = query.Where(w => w.Location.Contains(filters.RoomNumber!));
            }

            if (filters.DepartmentIds.Any())
            {
                var departmentSet = new HashSet<int>(filters.DepartmentIds);
                query = query.Where(w => w.DepartmentId.HasValue && departmentSet.Contains(w.DepartmentId.Value));
            }

            if (filters.WorkOrderTypeIds.Any())
            {
                var typeSet = new HashSet<int>(filters.WorkOrderTypeIds);
                query = query.Where(w => w.WorkOrderTypeId.HasValue && typeSet.Contains(w.WorkOrderTypeId.Value));
            }

            if (filters.Statuses.Any())
            {
                var statusSet = new HashSet<string>(filters.Statuses, StringComparer.OrdinalIgnoreCase);
                query = query.Where(w => statusSet.Contains(w.Status));
            }

            if (filters.CreatorIds.Any())
            {
                var creatorSet = new HashSet<string>(filters.CreatorIds, StringComparer.OrdinalIgnoreCase);
                query = query.Where(w => w.CreatedById != null && creatorSet.Contains(w.CreatedById));
            }

            if (!string.IsNullOrEmpty(filters.Search))
            {
                var term = filters.Search!;
                query = query.Where(w =>
                    EF.Functions.Like(w.Issue, $"%{term}%") ||
                    EF.Functions.Like(w.Details ?? string.Empty, $"%{term}%") ||
                    EF.Functions.Like(w.Location, $"%{term}%"));
            }

            query = filters.SortOrder switch
            {
                "status" => query.OrderBy(w => w.Status ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "status_desc" => query.OrderByDescending(w => w.Status ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "location" => query.OrderBy(w => w.Location ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "location_desc" => query.OrderByDescending(w => w.Location ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "department" => query.OrderBy(w => w.Department != null ? w.Department.Name : string.Empty).ThenByDescending(w => w.CreatedAt),
                "department_desc" => query.OrderByDescending(w => w.Department != null ? w.Department.Name : string.Empty).ThenByDescending(w => w.CreatedAt),
                "type" => query.OrderBy(w => w.WorkOrderType != null ? w.WorkOrderType.Name : string.Empty).ThenByDescending(w => w.CreatedAt),
                "type_desc" => query.OrderByDescending(w => w.WorkOrderType != null ? w.WorkOrderType.Name : string.Empty).ThenByDescending(w => w.CreatedAt),
                "issue" => query.OrderBy(w => w.Issue ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "issue_desc" => query.OrderByDescending(w => w.Issue ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "details" => query.OrderBy(w => w.Details ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "details_desc" => query.OrderByDescending(w => w.Details ?? string.Empty).ThenByDescending(w => w.CreatedAt),
                "due" => query.OrderBy(w => w.DueDate).ThenByDescending(w => w.CreatedAt),
                "due_desc" => query.OrderByDescending(w => w.DueDate).ThenByDescending(w => w.CreatedAt),
                "created" => query.OrderBy(w => w.CreatedAt),
                "created_desc" => query.OrderByDescending(w => w.CreatedAt),
                "creator" => query.OrderBy(w => w.CreatedBy != null ? w.CreatedBy.LastName : string.Empty)
                                  .ThenBy(w => w.CreatedBy != null ? w.CreatedBy.FirstName : string.Empty),
                "creator_desc" => query.OrderByDescending(w => w.CreatedBy != null ? w.CreatedBy.LastName : string.Empty)
                                       .ThenByDescending(w => w.CreatedBy != null ? w.CreatedBy.FirstName : string.Empty),
                "oldest" => query.OrderBy(w => w.CreatedAt),
                "newest" => query.OrderByDescending(w => w.CreatedAt),
                _ => query.OrderByDescending(w => w.CreatedAt)
            };

            if (filters.HideCompleted)
            {
                query = query.Where(w => w.Status == null || (w.Status != "Completed" && w.Status != "Cancelled"));
            }

            var workOrders = await query.ToListAsync();
            var now = DateTime.UtcNow;

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext.RequestAborted;
            var unassignedTranslated = isDefaultLanguage
                ? "Unassigned"
                : _translationService.Translate("Unassigned", activeLanguage, "Unassigned");

            var listItems = new List<WorkOrderListItemViewModel>();
            foreach (var wo in workOrders)
            {
                var sla = WorkOrderSlaHelper.Calculate(wo.DueDate, now);
                var propertyDetails = wo.Properties?
                    .Select(p => new WorkOrderPropertyDisplayViewModel
                    {
                        Id = p.PropertyId,
                        Name = p.Property?.Name ?? string.Empty,
                        TranslatedName = p.Property?.Name ?? string.Empty,
                        Code = p.Property?.Code ?? string.Empty
                    })
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<WorkOrderPropertyDisplayViewModel>();

                var creatorName = wo.CreatedBy != null
                    ? string.Join(" ", new[] { wo.CreatedBy.FirstName, wo.CreatedBy.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : null;

                var statusLabel = WorkOrderStatusOptions.GetLabel(wo.Status ?? string.Empty);
                var assignedDisplayName = BuildDisplayName(wo.AssignedTo);

                var item = new WorkOrderListItemViewModel
                {
                    Id = wo.Id,
                    Status = wo.Status,
                    StatusColor = WorkOrderStatusOptions.GetColor(wo.Status),
                    StatusLabel = statusLabel,
                    TranslatedStatusLabel = statusLabel,
                    Location = wo.Location ?? string.Empty,
                    TranslatedLocation = wo.Location ?? string.Empty,
                    WorkOrderType = wo.WorkOrderType?.Name,
                    TranslatedWorkOrderType = wo.WorkOrderType?.Name,
                    WorkOrderTypeId = wo.WorkOrderTypeId,
                    Issue = wo.Issue ?? string.Empty,
                    TranslatedIssue = wo.Issue ?? string.Empty,
                    Details = wo.Details,
                    TranslatedDetails = wo.Details,
                    CompletionNotes = wo.CompletionNotes,
                    TranslatedCompletionNotes = wo.CompletionNotes,
                    DueDate = wo.DueDate,
                    CreatedAt = wo.CreatedAt,
                    Department = wo.Department?.Name,
                    TranslatedDepartment = wo.Department?.Name ?? unassignedTranslated,
                    DepartmentId = wo.DepartmentId,
                    DepartmentColor = wo.Department?.Color,
                    AssignedToId = wo.AssignedToUserId,
                    AssignedToName = assignedDisplayName,
                    TranslatedAssignedTo = string.IsNullOrWhiteSpace(assignedDisplayName) ? unassignedTranslated : assignedDisplayName,
                    Creator = creatorName,
                    PriorityLabel = sla.PriorityLabel,
                    PriorityClass = sla.PriorityClass,
                    SlaStatus = sla.SlaStatus,
                    SlaStatusClass = sla.SlaStatusClass,
                    SlaSummary = WorkOrderSlaHelper.BuildSummaryText(sla),
                    IsOverdue = sla.IsOverdue,
                    Properties = propertyDetails,
                    TranslatedPropertyNames = propertyDetails
                        .Select(p =>
                        {
                            var name = string.IsNullOrWhiteSpace(p.TranslatedName) ? p.Name : p.TranslatedName;
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                return null;
                            }

                            return string.IsNullOrWhiteSpace(p.Code)
                                ? name
                                : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", name, p.Code);
                        })
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()!,
                    Attachments = wo.Attachments.Select(a => new WorkOrderAttachmentViewModel
                    {
                        FilePath = a.FilePath,
                        FileName = string.IsNullOrWhiteSpace(a.OriginalFileName) ? Path.GetFileName(a.FilePath) : a.OriginalFileName
                    }).ToList()
                };

                listItems.Add(item);
            }

            if (!isDefaultLanguage)
            {
                foreach (var item in listItems)
                {
                    var entityId = item.Id.ToString(CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(item.Location))
                    {
                        item.TranslatedLocation = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Location",
                            item.Location,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(item.Issue))
                    {
                        item.TranslatedIssue = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Issue",
                            item.Issue,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(item.Details))
                    {
                        item.TranslatedDetails = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Details",
                            item.Details,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(item.CompletionNotes))
                    {
                        item.TranslatedCompletionNotes = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "CompletionNotes",
                            item.CompletionNotes,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(item.WorkOrderType) && item.WorkOrderTypeId.HasValue)
                    {
                        item.TranslatedWorkOrderType = await _translationService.TranslateDynamicAsync(
                            "WorkOrderType",
                            item.WorkOrderTypeId.Value.ToString(CultureInfo.InvariantCulture),
                            "Name",
                            item.WorkOrderType,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(item.Department) && item.DepartmentId.HasValue)
                    {
                        item.TranslatedDepartment = await _translationService.TranslateDynamicAsync(
                            "Department",
                            item.DepartmentId.Value.ToString(CultureInfo.InvariantCulture),
                            "Name",
                            item.Department,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (item.Properties is { Count: > 0 })
                    {
                        foreach (var property in item.Properties)
                        {
                            if (string.IsNullOrWhiteSpace(property.Name))
                            {
                                property.TranslatedName = property.Name;
                                continue;
                            }

                            property.TranslatedName = await _translationService.TranslateDynamicAsync(
                                "Property",
                                property.Id.ToString(CultureInfo.InvariantCulture),
                                "Name",
                                property.Name,
                                _translationService.DefaultLanguage,
                                activeLanguage,
                                cancellationToken);
                        }

                        item.TranslatedPropertyNames = item.Properties
                            .Select(p =>
                            {
                                var translatedName = string.IsNullOrWhiteSpace(p.TranslatedName) ? p.Name : p.TranslatedName;
                                if (string.IsNullOrWhiteSpace(translatedName))
                                {
                                    return null;
                                }

                                return string.IsNullOrWhiteSpace(p.Code)
                                    ? translatedName
                                    : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", translatedName, p.Code);
                            })
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList()!;
                    }

                    if (!string.IsNullOrWhiteSpace(item.StatusLabel))
                    {
                        item.TranslatedStatusLabel = _translationService.Translate(item.StatusLabel, activeLanguage, item.StatusLabel);
                    }

                    item.TranslatedAssignedTo = string.IsNullOrWhiteSpace(item.AssignedToName)
                        ? unassignedTranslated
                        : item.AssignedToName;
                }
            }
            else
            {
                foreach (var item in listItems)
                {
                    item.TranslatedPropertyNames = item.Properties
                        .Select(p =>
                        {
                            var translatedName = string.IsNullOrWhiteSpace(p.TranslatedName) ? p.Name : p.TranslatedName;
                            if (string.IsNullOrWhiteSpace(translatedName))
                            {
                                return null;
                            }

                            return string.IsNullOrWhiteSpace(p.Code)
                                ? translatedName
                                : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", translatedName, p.Code);
                        })
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()!;

                    if (string.IsNullOrWhiteSpace(item.TranslatedAssignedTo))
                    {
                        item.TranslatedAssignedTo = string.IsNullOrWhiteSpace(item.AssignedToName)
                            ? unassignedTranslated
                            : item.AssignedToName;
                    }

                    if (string.IsNullOrWhiteSpace(item.TranslatedStatusLabel))
                    {
                        item.TranslatedStatusLabel = string.IsNullOrWhiteSpace(item.StatusLabel)
                            ? item.Status
                            : item.StatusLabel;
                    }
                }
            }


            var departmentSummaries = listItems
                .Where(wo =>
                    string.IsNullOrWhiteSpace(wo.Status) ||
                    (!string.Equals(wo.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(wo.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)))
                .GroupBy(wo => new
                {
                    Name = string.IsNullOrWhiteSpace(wo.Department) ? "Unassigned" : wo.Department,
                    Translated = string.IsNullOrWhiteSpace(wo.TranslatedDepartment) ? unassignedTranslated : wo.TranslatedDepartment,
                    Color = string.IsNullOrWhiteSpace(wo.DepartmentColor) ? null : wo.DepartmentColor
                })
                .Select(group => new DepartmentWorkOrderSummaryViewModel
                {
                    DepartmentName = group.Key.Name ?? "Unassigned",
                    TranslatedDepartmentName = group.Key.Translated ?? group.Key.Name ?? "Unassigned",
                    DepartmentColor = group.Key.Color,
                    OpenCount = group.Count()
                })
                .OrderByDescending(summary => summary.OpenCount)
                .ThenBy(summary => summary.DepartmentName)
                .ToList();

            var departmentQuery = _context.Departments.AsQueryable();
            var workOrderTypeQuery = _context.WorkOrderTypes.AsQueryable();

            if (targetPropertySet.Any())
            {
                departmentQuery = departmentQuery.Where(d => !d.PropertyId.HasValue || targetPropertySet.Contains(d.PropertyId.Value));
                workOrderTypeQuery = workOrderTypeQuery.Where(t => !t.PropertyId.HasValue || targetPropertySet.Contains(t.PropertyId.Value));
            }
            else
            {
                departmentQuery = departmentQuery.Where(_ => false);
                workOrderTypeQuery = workOrderTypeQuery.Where(_ => false);
            }

            var departments = await departmentQuery.OrderBy(d => d.Name).ToListAsync();
            var workOrderTypes = await workOrderTypeQuery.OrderBy(t => t.Name).ToListAsync();

            var creatorFilters = new HashSet<string>(filters.CreatorIds, StringComparer.OrdinalIgnoreCase);

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
                    Text = string.IsNullOrWhiteSpace(x.Name) ? "Unknown" : x.Name,
                    Selected = !string.IsNullOrWhiteSpace(x.CreatedById) && creatorFilters.Contains(x.CreatedById)
                })
                .ToList();

            var locationSuggestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetPropertySet.Any())
            {
                var roomSuggestions = await _context.Rooms
                    .Where(r => targetPropertySet.Contains(r.PropertyId))
                    .Select(r => r.RoomNumber)
                    .Distinct()
                    .ToListAsync();
                foreach (var room in roomSuggestions.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    locationSuggestions.Add(room);
                }
            }

            foreach (var loc in workOrders.Select(wo => wo.Location).Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                locationSuggestions.Add(loc);
            }

            var effectiveForm = form ?? new WorkOrderFormViewModel
            {
                Status = defaultStatus,
                DueDate = DateTime.UtcNow.Date
            };

            if (string.IsNullOrWhiteSpace(effectiveForm.Status))
            {
                effectiveForm.Status = defaultStatus;
            }

            effectiveForm.SelectedPropertyIds = effectiveForm.SelectedPropertyIds
                .Where(targetPropertySet.Contains)
                .Distinct()
                .ToList();

            effectiveForm.AdditionalLocations ??= new List<string>();

            if (!effectiveForm.SelectedPropertyIds.Any() && targetPropertySet.Any())
            {
                effectiveForm.SelectedPropertyIds = targetPropertySet.ToList();
            }

            var accessibleProperties = await _context.Properties
                .Where(p => accessiblePropertyIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name, p.Code })
                .ToListAsync();

            var propertyFilterSet = new HashSet<int>(filters.PropertyIds);

            var propertyFilterOptions = accessibleProperties
                .Select(p => new PropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsFilterSelected = propertyFilterSet.Contains(p.Id)
                })
                .ToList();

            var propertyFormOptions = accessibleProperties
                .Where(p => targetPropertySet.Contains(p.Id))
                .Select(p => new PropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsSelected = effectiveForm.SelectedPropertyIds.Contains(p.Id)
                })
                .ToList();

            var assignableUsersForProperties = await GetAssignableUsersAsync(targetPropertySet);
            var assigneeOptions = assignableUsersForProperties
                .Select(user => new SelectListItem
                {
                    Value = user.UserId,
                    Text = user.DisplayName,
                    Selected = !string.IsNullOrWhiteSpace(effectiveForm.AssignedUserId) &&
                               string.Equals(effectiveForm.AssignedUserId, user.UserId, StringComparison.Ordinal)
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(effectiveForm.AssignedUserId) &&
                assigneeOptions.All(option => !option.Selected))
            {
                var fallbackUser = await _context.Users
                    .Where(u => u.Id == effectiveForm.AssignedUserId)
                    .Select(u => new AssignableUserOption
                    {
                        UserId = u.Id,
                        DisplayName = BuildDisplayName(u)
                    })
                    .FirstOrDefaultAsync();

                if (fallbackUser != null)
                {
                    assigneeOptions.Add(new SelectListItem
                    {
                        Value = fallbackUser.UserId,
                        Text = fallbackUser.DisplayName,
                        Selected = true
                    });

                    assigneeOptions = assigneeOptions
                        .OrderBy(option => option.Text)
                        .ToList();
                }
                else
                {
                    effectiveForm.AssignedUserId = null;
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
                PropertyOptions = propertyFormOptions,
                PropertyFilterOptions = propertyFilterOptions,
                CreatorOptions = creatorOptions,
                AssigneeOptions = assigneeOptions,
                LocationSuggestions = locationSuggestions.OrderBy(x => x).ToList(),
                StatusColorMap = statusColorMap,
                EditingWorkOrderId = form?.Id,
                CanManageWorkOrders = canManage,
                CanUpdateWorkOrderStatus = canUpdateStatus,
                DepartmentSummaries = departmentSummaries
            };

            return viewModel;
        }

        private IActionResult RedirectBack(string? returnUrl, object? fallbackRouteValues = null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return fallbackRouteValues == null
                ? RedirectToAction(nameof(Index))
                : RedirectToAction(nameof(Index), fallbackRouteValues);
        }

        private int? GetCurrentPropertyId()
        {
            return (ViewBag.CurrentProperty as Property)?.Id;
        }

        private static bool HasManagementPrivileges(IList<string> roles)
        {
            return roles.Any(role =>
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        }

        private IActionResult RedirectWithFilters(int? highlight = null)
        {
            var baseUrl = Url.Action(nameof(Index)) ?? "/WorkOrders";
            var queryBuilder = new QueryBuilder();

            foreach (var kvp in HttpContext.Request.Query)
            {
                if (string.Equals(kvp.Key, "highlight", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var value in kvp.Value)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }
                    queryBuilder.Add(kvp.Key, value);
                }
            }

            if (highlight.HasValue)
            {
                queryBuilder.Add("highlight", highlight.Value.ToString(CultureInfo.InvariantCulture));
            }

            return Redirect(baseUrl + queryBuilder.ToQueryString());
        }

        private void DeleteAttachmentFiles(IEnumerable<string> relativePaths)
        {
            foreach (var relativePath in relativePaths)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                try
                {
                    var trimmed = relativePath.TrimStart('/', '\\');
                    var normalized = trimmed.Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);
                    var fullPath = Path.Combine(_environment.WebRootPath, normalized);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete work order attachment at {Path}", relativePath);
                }
            }
        }

        private static DateTime NormalizeUtcDate(DateTime value)
        {
            if (value == default)
            {
                return DateTime.SpecifyKind(default, DateTimeKind.Utc);
            }

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static bool MatchesRoom(string roomNumber, string? location)
        {
            if (string.IsNullOrWhiteSpace(roomNumber) || string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var trimmedLocation = location.Trim();
            var trimmedRoom = roomNumber.Trim();

            if (trimmedLocation.Equals(trimmedRoom, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return trimmedLocation.Contains(trimmedRoom, StringComparison.OrdinalIgnoreCase) ||
                trimmedRoom.Contains(trimmedLocation, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDisplayName(ApplicationUser? user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
            {
                return string.Join(" ", new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            }

            return string.IsNullOrWhiteSpace(user.Email) ? user.UserName ?? "Teammate" : user.Email!;
        }

        private sealed class RoomWorkOrderSummary
        {
            public int Id { get; set; }
            public string Status { get; set; } = string.Empty;
            public string StatusLabel { get; set; } = string.Empty;
            public string Issue { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public int? DepartmentId { get; set; }
            public string DepartmentName { get; set; } = string.Empty;
            public string DepartmentColor { get; set; } = "#dc3545";
            public string CreatedAtDisplay { get; set; } = string.Empty;
            public string DetailUrl { get; set; } = "#";
        }

        private sealed class AssignableUserOption
        {
            public string UserId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}



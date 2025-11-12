using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ClosedXML.Excel;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.WorkOrders;
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

        public WorkOrdersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<WorkOrdersController> logger,
            MentionService mentionService,
            IEmailSender emailSender,
            IUserTimeZoneService timeZoneService) : base(context, userManager)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _mentionService = mentionService;
            _emailSender = emailSender;
            _timeZoneService = timeZoneService;
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
                var createdLocal = _timeZoneService.ConvertToUserTime(order.CreatedAt);
                worksheet.Cell(rowNumber, 7).Value = createdLocal;
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
                    selectedPropertyIds = new List<int> { accessiblePropertyIds.First() };
                }
                else
                {
                    ModelState.AddModelError("Form.SelectedPropertyIds", "Please select at least one property.");
                }
            }

            form.SelectedPropertyIds = selectedPropertyIds;

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildViewModelAsync(filters, form);
                return View("Index", invalidModel);
            }

            await CreateWorkOrderAsync(form, user!, selectedPropertyIds);
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
                    wo.CreatedAt
                })
                .AsNoTracking()
                .ToListAsync();

            var matchingOrders = openOrders
                .Where(wo => MatchesRoom(trimmedRoom, wo.Location))
                .Select(wo => new
                {
                    wo.Id,
                    wo.Status,
                    wo.Issue,
                    CreatedAt = _timeZoneService.ConvertToUserTime(wo.CreatedAt)
                        .ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture),
                    DetailUrl = Url.Action(nameof(Edit), new { id = wo.Id }) ?? Url.Action(nameof(Index))
                })
                .ToList();

            return Json(new
            {
                workOrders = matchingOrders
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
                DueDate = (request.DueDate ?? DateTime.UtcNow.Date).Date,
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
            workOrder.DueDate = form.DueDate;
            workOrder.DepartmentId = form.DepartmentId;

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
        public async Task<IActionResult> AdvanceStatus(int id)
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
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            if (!workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            var currentIndex = Array.FindIndex(StatusProgression, status =>
                status.Equals(workOrder.Status, StringComparison.OrdinalIgnoreCase) ||
                (status.Equals("New", StringComparison.OrdinalIgnoreCase) && workOrder.Status.Equals("Open", StringComparison.OrdinalIgnoreCase)));

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextStatus = StatusProgression[(currentIndex + 1) % StatusProgression.Length];

            workOrder.Status = nextStatus;
            await _context.SaveChangesAsync();

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
        public async Task<IActionResult> CompleteDepartmentWorkOrder(int id, string? returnUrl)
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

            if (!string.Equals(workOrder.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                workOrder.Status = "Completed";
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
            IReadOnlyCollection<int> propertyIds)
        {
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

            return workOrder;
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
            var dueDateText = dueDateValue == default
                ? "Not set"
                : _timeZoneService.ConvertToUserTime(dueDateValue).ToString("MMM d, yyyy h:mm tt");
            dueDateText = WebUtility.HtmlEncode(dueDateText);

            var detailPreview = string.IsNullOrWhiteSpace(workOrder.Details) ? null : workOrder.Details.Trim();
            if (!string.IsNullOrWhiteSpace(detailPreview) && detailPreview.Length > 500)
            {
                detailPreview = $"{detailPreview[..500]}ÃƒÆ’Ã‚Â¯Ãƒâ€šÃ‚Â¿Ãƒâ€šÃ‚Â½";
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

            var bodyBuilder = new StringBuilder();
            bodyBuilder.AppendLine($@"<p>{safeActor} created a new work order assigned to <strong>{safeDepartment}</strong>.</p>");
            bodyBuilder.AppendLine($@"<p><strong>Issue:</strong> {safeIssue}</p>");
            if (safeLocation != null)
            {
                bodyBuilder.AppendLine($@"<p><strong>Location:</strong> {safeLocation}</p>");
            }
            bodyBuilder.AppendLine($@"<p><strong>Due Date:</strong> {dueDateText}</p>");
            if (safeProperties != null)
            {
                bodyBuilder.AppendLine($@"<p><strong>Properties:</strong> {safeProperties}</p>");
            }
            if (safeDetails != null)
            {
                bodyBuilder.AppendLine($@"<p><strong>Details:</strong><br/>{safeDetails}</p>");
            }
            bodyBuilder.AppendLine($@"<p><a href=""{workOrderLink}"">Open work order</a></p>");

            var htmlBody = bodyBuilder.ToString();

            foreach (var recipient in recipients)
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient.Email!, subject, htmlBody);
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
            var notificationTitle = $"New {departmentName} work order";
            var notificationContent = string.IsNullOrWhiteSpace(workOrder.Issue)
                ? $"Work order #{workOrder.Id}"
                : workOrder.Issue;

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
                query = query.Where(w => w.Status == null || w.Status != "Completed");
            }

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
                DueDate = DateTime.Today
            };

            if (string.IsNullOrWhiteSpace(effectiveForm.Status))
            {
                effectiveForm.Status = defaultStatus;
            }

            effectiveForm.SelectedPropertyIds = effectiveForm.SelectedPropertyIds
                .Where(targetPropertySet.Contains)
                .Distinct()
                .ToList();

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
                LocationSuggestions = locationSuggestions.OrderBy(x => x).ToList(),
                StatusColorMap = statusColorMap,
                EditingWorkOrderId = form?.Id,
                CanManageWorkOrders = canManage
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
    }
}


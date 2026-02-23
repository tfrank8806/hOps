using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Api;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class WorkOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPropertyAccessService _propertyAccessService;

        public WorkOrdersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPropertyAccessService propertyAccessService)
        {
            _context = context;
            _userManager = userManager;
            _propertyAccessService = propertyAccessService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkOrderListItemDto>>> GetWorkOrders([FromQuery] WorkOrderListQuery query)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var accessiblePropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!accessiblePropertyIds.Any())
            {
                return Forbid();
            }

            if (query.PropertyId.HasValue && !accessiblePropertyIds.Contains(query.PropertyId.Value))
            {
                return Forbid();
            }

            var take = Math.Clamp(query.Take, 5, 200);

            var baseQuery = _context.WorkOrders
                .AsNoTracking()
                .Include(w => w.CreatedBy)
                .Include(w => w.Properties).ThenInclude(p => p.Property)
                .Include(w => w.Attachments)
                .Where(w => w.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)));

            if (query.PropertyId.HasValue)
            {
                baseQuery = baseQuery.Where(w => w.Properties.Any(p => p.PropertyId == query.PropertyId.Value));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                baseQuery = baseQuery.Where(w => w.Status == query.Status);
            }

            var workOrders = await baseQuery
                .OrderByDescending(w => w.CreatedAt)
                .Take(take)
                .ToListAsync();

            var items = workOrders.Select(MapListItem).ToList();
            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<WorkOrderDetailDto>> GetWorkOrder(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var accessiblePropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!accessiblePropertyIds.Any())
            {
                return Forbid();
            }

            var workOrder = await _context.WorkOrders
                .AsNoTracking()
                .Include(w => w.CreatedBy)
                .Include(w => w.Properties).ThenInclude(p => p.Property)
                .Include(w => w.Attachments)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null || !workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return NotFound();
            }

            return Ok(MapDetail(workOrder));
        }

        [HttpPost]
        public async Task<ActionResult<WorkOrderDetailDto>> CreateWorkOrder([FromBody] CreateWorkOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var accessiblePropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!accessiblePropertyIds.Any())
            {
                return Forbid();
            }

            var propertySelection = request.PropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!propertySelection.Any())
            {
                return BadRequest(new { error = "At least one accessible property is required." });
            }

            var workOrder = new WorkOrder
            {
                Issue = request.Issue.Trim(),
                Details = request.Details?.Trim(),
                Location = request.Location?.Trim(),
                DepartmentId = request.DepartmentId,
                WorkOrderTypeId = request.WorkOrderTypeId,
                Status = string.IsNullOrWhiteSpace(request.Status)
                    ? WorkOrderStatusOptions.DefaultStatus
                    : request.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id,
                DueDate = (request.DueDateUtc ?? DateTime.UtcNow.Date).Date
            };

            foreach (var propertyId in propertySelection)
            {
                workOrder.Properties.Add(new WorkOrderProperty
                {
                    PropertyId = propertyId
                });
            }

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            await _context.Entry(workOrder).Reference(w => w.CreatedBy).LoadAsync();
            await _context.Entry(workOrder).Collection(w => w.Properties).Query().Include(p => p.Property).LoadAsync();
            await _context.Entry(workOrder).Collection(w => w.Attachments).LoadAsync();

            return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, MapDetail(workOrder));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<WorkOrderDetailDto>> UpdateWorkOrder(int id, [FromBody] UpdateWorkOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var accessiblePropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!accessiblePropertyIds.Any())
            {
                return Forbid();
            }

            var workOrder = await _context.WorkOrders
                .Include(w => w.Properties)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null || !workOrder.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return NotFound();
            }

            workOrder.Issue = request.Issue.Trim();
            workOrder.Details = request.Details?.Trim();
            workOrder.Location = request.Location?.Trim();
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                workOrder.Status = request.Status;
            }
            if (request.DueDateUtc.HasValue)
            {
                workOrder.DueDate = request.DueDateUtc.Value.Date;
            }
            workOrder.DepartmentId = request.DepartmentId;
            workOrder.WorkOrderTypeId = request.WorkOrderTypeId;

            var incomingPropertyIds = request.PropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!incomingPropertyIds.Any())
            {
                return BadRequest(new { error = "At least one accessible property is required." });
            }

            var toRemove = workOrder.Properties.Where(p => !incomingPropertyIds.Contains(p.PropertyId)).ToList();
            foreach (var property in toRemove)
            {
                workOrder.Properties.Remove(property);
            }

            foreach (var propertyId in incomingPropertyIds)
            {
                if (!workOrder.Properties.Any(p => p.PropertyId == propertyId))
                {
                    workOrder.Properties.Add(new WorkOrderProperty { PropertyId = propertyId });
                }
            }

            await _context.SaveChangesAsync();

            await _context.Entry(workOrder).Reference(w => w.CreatedBy).LoadAsync();
            await _context.Entry(workOrder).Collection(w => w.Properties).Query().Include(p => p.Property).LoadAsync();
            await _context.Entry(workOrder).Collection(w => w.Attachments).LoadAsync();

            return Ok(MapDetail(workOrder));
        }

        private static WorkOrderListItemDto MapListItem(WorkOrder workOrder)
        {
            return new WorkOrderListItemDto
            {
                Id = workOrder.Id,
                Status = workOrder.Status ?? WorkOrderStatusOptions.DefaultStatus,
                Issue = workOrder.Issue ?? $"Work Order #{workOrder.Id}",
                Department = workOrder.Department?.Name ?? "Unassigned",
                WorkOrderType = workOrder.WorkOrderType?.Name ?? string.Empty,
                Location = workOrder.Location ?? string.Empty,
                CreatedAtUtc = workOrder.CreatedAt,
                DueDateUtc = workOrder.DueDate,
                Creator = FormatUserName(workOrder.CreatedBy),
                Properties = workOrder.Properties
                    .Select(p => p.Property != null
                        ? (string.IsNullOrWhiteSpace(p.Property.Code) ? p.Property.Name : $"{p.Property.Name} ({p.Property.Code})")
                        : $"Property #{p.PropertyId}")
                    .Distinct()
                    .ToList()
            };
        }

        private static WorkOrderDetailDto MapDetail(WorkOrder workOrder)
        {
            var dto = MapListItem(workOrder);
            return new WorkOrderDetailDto
            {
                Id = dto.Id,
                Status = dto.Status,
                Issue = dto.Issue,
                Department = dto.Department,
                WorkOrderType = dto.WorkOrderType,
                Location = dto.Location,
                CreatedAtUtc = dto.CreatedAtUtc,
                DueDateUtc = dto.DueDateUtc,
                Creator = dto.Creator,
                Properties = dto.Properties,
                Details = workOrder.Details ?? string.Empty,
                DepartmentId = workOrder.DepartmentId,
                WorkOrderTypeId = workOrder.WorkOrderTypeId,
                Attachments = workOrder.Attachments
                    .Select(a => new WorkOrderAttachmentDto
                    {
                        Id = a.Id,
                        FileName = string.IsNullOrWhiteSpace(a.OriginalFileName) ? a.FilePath : a.OriginalFileName,
                        DownloadUrl = a.FilePath
                    })
                    .ToList()
            };
        }

        private static string FormatUserName(ApplicationUser? user)
        {
            if (user == null)
            {
                return "Unknown";
            }

            var name = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return user.Email ?? "Unknown";
        }
    }
}

using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public SettingsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // — Departments CRUD —
        public async Task<IActionResult> Departments()
        {
            var departments = await _db.Departments.ToListAsync();
            return View(departments);
        }

        public IActionResult CreateDepartment() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDepartment(Department model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Departments.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        public async Task<IActionResult> EditDepartment(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();
            return View(dept);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDepartment(Department model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Departments.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();
            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        // — WorkOrderTypes CRUD —
        public async Task<IActionResult> WorkOrderTypes()
        {
            var types = await _db.WorkOrderTypes.ToListAsync();
            return View(types);
        }

        public IActionResult CreateWorkOrderType()
        {
            ViewData["FormAction"] = nameof(CreateWorkOrderType);
            return View("WorkOrderTypeForm", new WorkOrderType());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWorkOrderType(WorkOrderType model)
        {
            if (!ModelState.IsValid) return View("WorkOrderTypeForm", model);
            _db.WorkOrderTypes.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(WorkOrderTypes));
        }

        public async Task<IActionResult> EditWorkOrderType(int id)
        {
            var item = await _db.WorkOrderTypes.FindAsync(id);
            if (item == null) return NotFound();
            ViewData["FormAction"] = nameof(EditWorkOrderType);
            return View("WorkOrderTypeForm", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWorkOrderType(WorkOrderType model)
        {
            if (!ModelState.IsValid) return View("WorkOrderTypeForm", model);
            _db.WorkOrderTypes.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(WorkOrderTypes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkOrderType(int id)
        {
            var item = await _db.WorkOrderTypes.FindAsync(id);
            if (item == null) return NotFound();
            _db.WorkOrderTypes.Remove(item);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(WorkOrderTypes));
        }

        // — PhonebookTypes CRUD —
        public async Task<IActionResult> PhonebookTypes()
        {
            var list = await _db.PhonebookTypes.ToListAsync();
            return View(list);
        }

        public IActionResult CreatePhonebookType()
        {
            ViewData["FormAction"] = nameof(CreatePhonebookType);
            return View("PhonebookTypeForm", new PhonebookType());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePhonebookType(PhonebookType model)
        {
            if (!ModelState.IsValid) return View("PhonebookTypeForm", model);
            _db.PhonebookTypes.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(PhonebookTypes));
        }

        public async Task<IActionResult> EditPhonebookType(int id)
        {
            var type = await _db.PhonebookTypes.FindAsync(id);
            if (type == null) return NotFound();
            ViewData["FormAction"] = nameof(EditPhonebookType);
            return View("PhonebookTypeForm", type);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPhonebookType(PhonebookType model)
        {
            if (!ModelState.IsValid) return View("PhonebookTypeForm", model);
            _db.PhonebookTypes.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(PhonebookTypes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhonebookType(int id)
        {
            var type = await _db.PhonebookTypes.FindAsync(id);
            if (type == null) return NotFound();
            _db.PhonebookTypes.Remove(type);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(PhonebookTypes));
        }

        // — CalendarCategories CRUD —
        public async Task<IActionResult> CalendarCategories()
        {
            var list = await _db.CalendarCategories.ToListAsync();
            return View(list);
        }

        public IActionResult CreateCalendarCategory()
        {
            ViewData["FormAction"] = nameof(CreateCalendarCategory);
            return View("CalendarCategoryForm", new CalendarCategory());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCalendarCategory(CalendarCategory model)
        {
            if (!ModelState.IsValid) return View("CalendarCategoryForm", model);
            _db.CalendarCategories.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(CalendarCategories));
        }

        public async Task<IActionResult> EditCalendarCategory(int id)
        {
            var item = await _db.CalendarCategories.FindAsync(id);
            if (item == null) return NotFound();
            ViewData["FormAction"] = nameof(EditCalendarCategory);
            return View("CalendarCategoryForm", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCalendarCategory(CalendarCategory model)
        {
            if (!ModelState.IsValid) return View("CalendarCategoryForm", model);
            _db.CalendarCategories.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(CalendarCategories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCalendarCategory(int id)
        {
            var item = await _db.CalendarCategories.FindAsync(id);
            if (item == null) return NotFound();
            _db.CalendarCategories.Remove(item);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(CalendarCategories));
        }

        // — Room Management (CRUD + CSV Import/Export) —
        public async Task<IActionResult> Rooms(int propertyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            List<Property> accessibleProps;
            if (roles.Contains("Admin"))
                accessibleProps = await _db.Properties.ToListAsync();
            else
                accessibleProps = await _db.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.Property)
                    .ToListAsync();

            if (!accessibleProps.Any()) return Forbid();
            if (!accessibleProps.Any(p => p.Id == propertyId))
                propertyId = accessibleProps.First().Id;

            var rooms = await _db.Rooms.Where(r => r.PropertyId == propertyId).ToListAsync();

            ViewBag.PropertyId = propertyId;
            ViewBag.AllProperties = accessibleProps;
            return View(rooms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRooms(int propertyId, List<Room> rooms)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);
            bool allowed = roles.Contains("Admin") ||
                await _db.UserPropertyAccesses.AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == propertyId);
            if (!allowed) return Forbid();

            foreach (var r in rooms)
            {
                r.PropertyId = propertyId;
                if (string.IsNullOrWhiteSpace(r.RoomNumber)) continue;
                if (r.Id == 0) _db.Rooms.Add(r);
                else _db.Rooms.Update(r);
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Rooms), new { propertyId });
        }

        public FileResult DownloadRoomsTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("RoomNumber,Floor,RoomType,Description");
            sb.AppendLine("101,1,Standard,Sample description");
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "rooms_template.csv");
        }

        [HttpPost]
        public async Task<IActionResult> ImportRooms(int propertyId, IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0) return BadRequest("CSV file is empty");

            using var reader = new System.IO.StreamReader(csvFile.OpenReadStream());
            await reader.ReadLineAsync();
            var newRooms = new List<Room>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 4) continue;
                newRooms.Add(new Room
                {
                    PropertyId = propertyId,
                    RoomNumber = parts[0].Trim(),
                    Floor = int.TryParse(parts[1].Trim(), out int f) ? f : 0,
                    RoomType = parts[2].Trim(),
                    Description = parts[3].Trim()
                });
            }
            _db.Rooms.RemoveRange(_db.Rooms.Where(r => r.PropertyId == propertyId));
            await _db.Rooms.AddRangeAsync(newRooms);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Rooms), new { propertyId });
        }

        // — Layout Editor —
        public async Task<IActionResult> LayoutEditor(int propertyId, int? floor)
        {
            var rooms = await _db.Rooms.Where(r => r.PropertyId == propertyId).ToListAsync();
            if (!rooms.Any()) return NotFound("No rooms found for this property.");

            var floors = rooms.Select(r => r.Floor).Distinct().OrderBy(f => f).ToList();
            int selectedFloor = floor ?? floors.First();

            var allLayouts = await _db.RoomLayouts
                .Where(l => l.PropertyId == propertyId)
                .ToListAsync();

            var layoutsByFloor = allLayouts
                .GroupBy(l => l.Floor)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (!layoutsByFloor.TryGetValue(selectedFloor, out var layouts))
            {
                layouts = new List<RoomLayout>();
            }

            var vm = new LayoutEditorViewModel
            {
                PropertyId = propertyId,
                SelectedFloor = selectedFloor,
                AllFloors = floors,
                Rooms = rooms,
                Layouts = layouts,
                LayoutsByFloor = layoutsByFloor
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveLayout([FromBody] LayoutSaveRequest request)
        {
            if (request == null || request.PropertyId <= 0)
            {
                return BadRequest();
            }

            var propertyId = request.PropertyId;
            var floor = request.Floor;
            var layoutDtos = request.Layouts ?? new List<RoomLayoutDto>();

            var layoutsOnFloor = await _db.RoomLayouts
                .Where(l => l.PropertyId == propertyId && l.Floor == floor)
                .ToListAsync();

            var keepSet = new HashSet<RoomLayout>();
            var orderedLayouts = new List<RoomLayout>();

            foreach (var dto in layoutDtos)
            {
                dto.PropertyId = propertyId;
                dto.Floor = floor;

                var trimmedLabel = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label!.Trim();
                var normalizedShapeType = string.IsNullOrWhiteSpace(dto.ShapeType) ? null : dto.ShapeType!.Trim();
                var normalizedShapeData = string.IsNullOrWhiteSpace(dto.ShapeData) ? null : dto.ShapeData!.Trim();

                RoomLayout? layoutEntity = null;
                if (dto.Id > 0)
                {
                    layoutEntity = layoutsOnFloor.FirstOrDefault(l => l.Id == dto.Id);
                }

                if (layoutEntity == null && dto.RoomId != 0)
                {
                    layoutEntity = layoutsOnFloor.FirstOrDefault(l => l.RoomId == dto.RoomId);
                }

                if (layoutEntity == null)
                {
                    layoutEntity = new RoomLayout
                    {
                        PropertyId = dto.PropertyId,
                        RoomId = dto.RoomId,
                        Floor = dto.Floor
                    };
                    _db.RoomLayouts.Add(layoutEntity);
                    layoutsOnFloor.Add(layoutEntity);
                }

                layoutEntity.X = dto.X;
                layoutEntity.Y = dto.Y;
                layoutEntity.Width = dto.Width;
                layoutEntity.Height = dto.Height;
                layoutEntity.Label = trimmedLabel;
                layoutEntity.ShapeType = normalizedShapeType;
                layoutEntity.ShapeData = normalizedShapeData;

                keepSet.Add(layoutEntity);
                orderedLayouts.Add(layoutEntity);
            }

            foreach (var existing in layoutsOnFloor.ToList())
            {
                if (!keepSet.Contains(existing))
                {
                    _db.RoomLayouts.Remove(existing);
                    layoutsOnFloor.Remove(existing);
                }
            }

            await _db.SaveChangesAsync();

            var responseLayouts = orderedLayouts
                .Select(l => new
                {
                    id = l.Id,
                    roomId = l.RoomId,
                    label = l.Label ?? string.Empty,
                    x = l.X,
                    y = l.Y,
                    width = l.Width,
                    height = l.Height,
                    shapeType = string.IsNullOrWhiteSpace(l.ShapeType) ? "rectangle" : l.ShapeType,
                    shapeData = l.ShapeData ?? string.Empty
                })
                .ToList();

            return Json(new { success = true, layouts = responseLayouts });
        }

        // — Add Floor (called from LayoutEditor via AJAX) —
        [HttpPost]
        public async Task<IActionResult> AddFloor([FromBody] AddFloorDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);
            bool allowed = roles.Contains("Admin") ||
                await _db.UserPropertyAccesses.AnyAsync(a => a.ApplicationUserId == user.Id && a.PropertyId == dto.PropertyId);
            if (!allowed) return Forbid();

            bool exists = await _db.Rooms.AnyAsync(r => r.PropertyId == dto.PropertyId && r.Floor == dto.Floor);
            if (!exists)
            {
                var dummy = new Room
                {
                    PropertyId = dto.PropertyId,
                    RoomNumber = $"Floor{dto.Floor}-placeholder",
                    Floor = dto.Floor,
                    RoomType = "Custom"
                };
                _db.Rooms.Add(dummy);
                await _db.SaveChangesAsync();
            }
            return Ok();
        }
    }

    public class AddFloorDto
    {
        public int PropertyId { get; set; }
        public int Floor { get; set; }
    }
}

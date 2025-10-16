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
            {
                accessibleProps = await _db.Properties.ToListAsync();
            }
            else
            {
                accessibleProps = await _db.UserPropertyAccesses
                    .Where(upa => upa.ApplicationUserId == currentUser.Id)
                    .Select(upa => upa.Property)
                    .ToListAsync();
            }

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

            bool allowed = roles.Contains("Admin")
                || await _db.UserPropertyAccesses
                    .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == propertyId);
            if (!allowed) return Forbid();

            foreach (var r in rooms)
            {
                r.PropertyId = propertyId;
                if (string.IsNullOrWhiteSpace(r.RoomNumber)) continue;
                if (r.Id == 0)
                    _db.Rooms.Add(r);
                else
                    _db.Rooms.Update(r);
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Rooms), new { propertyId });
        }

        public FileResult DownloadRoomsTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("RoomNumber,Floor,RoomType,Description");
            sb.AppendLine("101,1,Standard,Sample description");
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "rooms_template.csv");
        }

        [HttpPost]
        public async Task<IActionResult> ImportRooms(int propertyId, IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
                return BadRequest("CSV file is empty");

            using var reader = new System.IO.StreamReader(csvFile.OpenReadStream());
            await reader.ReadLineAsync(); // skip header

            var newRooms = new List<Room>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 4) continue;

                var room = new Room
                {
                    PropertyId = propertyId,
                    RoomNumber = parts[0].Trim(),
                    Floor = int.TryParse(parts[1].Trim(), out int f) ? f : 0,
                    RoomType = parts[2].Trim(),
                    Description = parts[3].Trim()
                };
                newRooms.Add(room);
            }

            _db.Rooms.RemoveRange(_db.Rooms.Where(r => r.PropertyId == propertyId));
            await _db.Rooms.AddRangeAsync(newRooms);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Rooms), new { propertyId });
        }

        // — Layout Editor & Save using Room.Floor —
        public async Task<IActionResult> LayoutEditor(int propertyId, int? floor)
        {
            var rooms = await _db.Rooms
                .Where(r => r.PropertyId == propertyId)
                .ToListAsync();

            if (!rooms.Any())
                return NotFound("No rooms found for this property.");

            var floorNumbers = rooms.Select(r => r.Floor).Distinct().OrderBy(f => f).ToList();
            int selectedFloor = floor ?? floorNumbers.First();

            var layouts = await _db.RoomLayouts
                .Where(l => l.PropertyId == propertyId && l.Floor == selectedFloor)
                .ToListAsync();

            var vm = new LayoutEditorViewModel
            {
                PropertyId = propertyId,
                SelectedFloor = selectedFloor,
                AllFloors = floorNumbers,
                Rooms = rooms,
                Layouts = layouts
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveLayout([FromBody] List<RoomLayoutDto> layoutDtos)
        {
            foreach (var dto in layoutDtos)
            {
                var existing = await _db.RoomLayouts
                    .FirstOrDefaultAsync(l =>
                        l.RoomId == dto.RoomId &&
                        l.PropertyId == dto.PropertyId &&
                        l.Floor == dto.Floor);

                if (existing != null)
                {
                    existing.X = dto.X;
                    existing.Y = dto.Y;
                    existing.Width = dto.Width;
                    existing.Height = dto.Height;
                    _db.RoomLayouts.Update(existing);
                }
                else
                {
                    _db.RoomLayouts.Add(new RoomLayout
                    {
                        PropertyId = dto.PropertyId,
                        RoomId = dto.RoomId,
                        Floor = dto.Floor,
                        X = dto.X,
                        Y = dto.Y,
                        Width = dto.Width,
                        Height = dto.Height
                    });
                }
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}

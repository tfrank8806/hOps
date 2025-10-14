using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SettingsController(ApplicationDbContext db)
        {
            _db = db;
        }

// Departments
        public async Task<IActionResult> Departments()
        {
            var departments = await _db.Departments.ToListAsync();
            return View(departments);
        }

        public IActionResult CreateDepartment()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateDepartment(Department model)
        {
            if (!ModelState.IsValid) return View(model);

            _db.Departments.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("Departments");
        }

        public async Task<IActionResult> EditDepartment(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();
            return View(dept);
        }

        [HttpPost]
        public async Task<IActionResult> EditDepartment(Department model)
        {
            if (!ModelState.IsValid) return View(model);

            _db.Departments.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("Departments");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return NotFound();

            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
            return RedirectToAction("Departments");
        }

// WorkOrderTypes
        public async Task<IActionResult> WorkOrderTypes()
        {
            var types = await _db.WorkOrderTypes.ToListAsync();
            return View(types);
        }

        public IActionResult CreateWorkOrderType()
        {
            ViewData["FormAction"] = "CreateWorkOrderType";
            return View("WorkOrderTypeForm", new WorkOrderType());
        }

        [HttpPost]
        public async Task<IActionResult> CreateWorkOrderType(WorkOrderType model)
        {
            if (!ModelState.IsValid) return View("WorkOrderTypeForm", model);

            _db.WorkOrderTypes.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("WorkOrderTypes");
        }

        public async Task<IActionResult> EditWorkOrderType(int id)
        {
            var item = await _db.WorkOrderTypes.FindAsync(id);
            if (item == null) return NotFound();
            ViewData["FormAction"] = "EditWorkOrderType";
            return View("WorkOrderTypeForm", item);
        }

        [HttpPost]
        public async Task<IActionResult> EditWorkOrderType(WorkOrderType model)
        {
            if (!ModelState.IsValid) return View("WorkOrderTypeForm", model);

            _db.WorkOrderTypes.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("WorkOrderTypes");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteWorkOrderType(int id)
        {
            var item = await _db.WorkOrderTypes.FindAsync(id);
            if (item == null) return NotFound();

            _db.WorkOrderTypes.Remove(item);
            await _db.SaveChangesAsync();
            return RedirectToAction("WorkOrderTypes");
        }

// RoomTypes
        public async Task<IActionResult> RoomTypes()
        {
            var types = await _db.RoomTypes.ToListAsync();
            return View(types);
        }

        public IActionResult CreateRoomType()
        {
            ViewData["FormAction"] = "CreateRoomType";
            return View("RoomTypeForm", new RoomType());
        }

        [HttpPost]
        public async Task<IActionResult> CreateRoomType(RoomType model)
        {
            if (!ModelState.IsValid) return View("RoomTypeForm", model);

            _db.RoomTypes.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("RoomTypes");
        }

        public async Task<IActionResult> EditRoomType(int id)
        {
            var type = await _db.RoomTypes.FindAsync(id);
            if (type == null) return NotFound();
            ViewData["FormAction"] = "EditRoomType";
            return View("RoomTypeForm", type);
        }

        [HttpPost]
        public async Task<IActionResult> EditRoomType(RoomType model)
        {
            if (!ModelState.IsValid) return View("RoomTypeForm", model);

            _db.RoomTypes.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("RoomTypes");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRoomType(int id)
        {
            var type = await _db.RoomTypes.FindAsync(id);
            if (type == null) return NotFound();

            _db.RoomTypes.Remove(type);
            await _db.SaveChangesAsync();
            return RedirectToAction("RoomTypes");
        }

// PhonebookTypes
        public async Task<IActionResult> PhonebookTypes()
        {
            var types = await _db.PhonebookTypes.ToListAsync();
            return View(types);
        }

        public IActionResult CreatePhonebookType()
        {
            ViewData["FormAction"] = "CreatePhonebookType";
            return View("PhonebookTypeForm", new PhonebookType());
        }

        [HttpPost]
        public async Task<IActionResult> CreatePhonebookType(PhonebookType model)
        {
            if (!ModelState.IsValid) return View("PhonebookTypeForm", model);

            _db.PhonebookTypes.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("PhonebookTypes");
        }

        public async Task<IActionResult> EditPhonebookType(int id)
        {
            var type = await _db.PhonebookTypes.FindAsync(id);
            if (type == null) return NotFound();
            ViewData["FormAction"] = "EditPhonebookType";
            return View("PhonebookTypeForm", type);
        }

        [HttpPost]
        public async Task<IActionResult> EditPhonebookType(PhonebookType model)
        {
            if (!ModelState.IsValid) return View("PhonebookTypeForm", model);

            _db.PhonebookTypes.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("PhonebookTypes");
        }

        [HttpPost]
        public async Task<IActionResult> DeletePhonebookType(int id)
        {
            var type = await _db.PhonebookTypes.FindAsync(id);
            if (type == null) return NotFound();

            _db.PhonebookTypes.Remove(type);
            await _db.SaveChangesAsync();
            return RedirectToAction("PhonebookTypes");
        }


// CalendaryCategories
        public async Task<IActionResult> CalendarCategories()
        {
            var list = await _db.CalendarCategories.ToListAsync();
            return View(list);
        }

        public IActionResult CreateCalendarCategory()
        {
            ViewData["FormAction"] = "CreateCalendarCategory";
            return View("CalendarCategoryForm", new CalendarCategory());
        }

        [HttpPost]
        public async Task<IActionResult> CreateCalendarCategory(CalendarCategory model)
        {
            if (!ModelState.IsValid) return View("CalendarCategoryForm", model);

            _db.CalendarCategories.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("CalendarCategories");
        }

        public async Task<IActionResult> EditCalendarCategory(int id)
        {
            var item = await _db.CalendarCategories.FindAsync(id);
            if (item == null) return NotFound();

            ViewData["FormAction"] = "EditCalendarCategory";
            return View("CalendarCategoryForm", item);
        }

        [HttpPost]
        public async Task<IActionResult> EditCalendarCategory(CalendarCategory model)
        {
            if (!ModelState.IsValid) return View("CalendarCategoryForm", model);

            _db.CalendarCategories.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction("CalendarCategories");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCalendarCategory(int id)
        {
            var item = await _db.CalendarCategories.FindAsync(id);
            if (item == null) return NotFound();

            _db.CalendarCategories.Remove(item);
            await _db.SaveChangesAsync();
            return RedirectToAction("CalendarCategories");
        }


        // Layout Editior
        public async Task<IActionResult> LayoutEditor(int propertyId)
        {
            // load rooms for that property
            var rooms = await _db.Rooms
                .Where(r => r.PropertyId == propertyId)
                .ToListAsync();
            var layouts = await _db.RoomLayouts
                .Where(l => l.PropertyId == propertyId)
                .ToListAsync();

            var vm = new LayoutEditorViewModel
            {
                PropertyId = propertyId,
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
                    .FirstOrDefaultAsync(l => l.RoomId == dto.RoomId && l.PropertyId == dto.PropertyId);

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
                    var newLayout = new RoomLayout
                    {
                        PropertyId = dto.PropertyId,
                        RoomId = dto.RoomId,
                        X = dto.X,
                        Y = dto.Y,
                        Width = dto.Width,
                        Height = dto.Height
                    };
                    _db.RoomLayouts.Add(newLayout);
                }
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

    }
}

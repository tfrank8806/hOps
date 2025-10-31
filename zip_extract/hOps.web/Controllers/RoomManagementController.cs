using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace hOps.web.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class RoomManagementController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoomManagementController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int propertyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                bool hasAccess = await _db.UserPropertyAccesses
                    .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == propertyId);
                if (!hasAccess)
                    return Forbid();
            }

            var rooms = await _db.Rooms
                .Where(r => r.PropertyId == propertyId)
                .ToListAsync();

            ViewBag.PropertyId = propertyId;
            return View(rooms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int propertyId, List<Room> rooms)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                bool hasAccess = await _db.UserPropertyAccesses
                    .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == propertyId);
                if (!hasAccess)
                    return Forbid();
            }

            foreach (var room in rooms)
            {
                room.PropertyId = propertyId;
                if (room.Id == 0)
                    _db.Rooms.Add(room);
                else
                    _db.Rooms.Update(room);
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { propertyId });
        }

        public FileResult DownloadTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("RoomNumber,RoomType,Floor,Description");
            sb.AppendLine("101,Standard,1,Sample room");
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "room_template.csv");
        }

        [HttpPost]
        public async Task<IActionResult> Import(int propertyId, IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
                return BadRequest("CSV file empty");

            using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream);
            string? header = await reader.ReadLineAsync();

            var rooms = new List<Room>();
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
                    RoomType = parts[1].Trim(),
                    Floor = int.TryParse(parts[2].Trim(), out var fl) ? fl : 0,
                    Description = parts[3].Trim()
                };
                rooms.Add(room);
            }

            var existing = _db.Rooms.Where(r => r.PropertyId == propertyId);
            _db.Rooms.RemoveRange(existing);

            await _db.Rooms.AddRangeAsync(rooms);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { propertyId });
        }
    }
}

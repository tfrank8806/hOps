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
    public class RoomManagementController : BaseController
    {
        public RoomManagementController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
            : base(db, userManager)
        {
        }

        public async Task<IActionResult> Index(int propertyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            IList<string> roles = new List<string>();
            roles = await _userManager.GetRolesAsync(currentUser);

            // If manager, enforce access restriction
            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                bool hasAccess = await _context.UserPropertyAccesses
                    .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == propertyId);
                if (!hasAccess)
                    return Forbid();
            }

            var rooms = await _context.Rooms
                .Where(r => r.PropertyId == propertyId)
                .ToListAsync();

            var property = await _context.Properties
                .Where(p => p.Id == propertyId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync();

            ViewBag.PropertyId = propertyId;
            ViewBag.PropertyName = property?.Name ?? $"Property {propertyId}";
            return View(rooms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int propertyId, List<Room> rooms)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            IList<string> roles = new List<string>();
            roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                bool hasAccess = await _context.UserPropertyAccesses
                    .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == propertyId);
                if (!hasAccess)
                    return Forbid();
            }

            foreach (var room in rooms)
            {
                room.PropertyId = propertyId;
                if (room.Id == 0)
                    _context.Rooms.Add(room);
                else
                    _context.Rooms.Update(room);
            }

            await _context.SaveChangesAsync();
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(int propertyId, IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["RoomImportError"] = "CSV file is empty.";
                return RedirectToAction(nameof(Index), new { propertyId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains("Manager") && !roles.Contains("Admin"))
            {
                var hasAccess = await _context.UserPropertyAccesses
                    .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == propertyId);
                if (!hasAccess)
                {
                    return Forbid();
                }
            }

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

            if (!rooms.Any())
            {
                TempData["RoomImportError"] = "No valid rooms were found in the uploaded file. Existing rooms were left unchanged.";
                return RedirectToAction(nameof(Index), new { propertyId });
            }

            var existing = _context.Rooms.Where(r => r.PropertyId == propertyId);
            _context.Rooms.RemoveRange(existing);

            await _context.Rooms.AddRangeAsync(rooms);
            await _context.SaveChangesAsync();

            TempData["RoomImportMessage"] = $"{rooms.Count} rooms imported successfully.";
            return RedirectToAction(nameof(Index), new { propertyId });
        }
    }
}

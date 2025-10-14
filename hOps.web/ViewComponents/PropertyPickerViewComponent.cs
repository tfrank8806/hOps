using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.ViewComponents
{
    public class PropertyPickerViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PropertyPickerViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null) return Content("");

            var props = await _db.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Include(upa => upa.Property)
                .Select(upa => upa.Property)
                .ToListAsync();

            return View(props);
        }
    }
}

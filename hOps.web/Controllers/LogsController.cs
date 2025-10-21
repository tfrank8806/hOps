using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace hOps.web.Controllers
{
    public class LogsController : BaseController
    {
        public LogsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Logs";
            var user = await _userManager.GetUserAsync(User);

            string displayName = "Unknown user";
            string currentUserId = string.Empty;

            if (user != null)
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    displayName = fullName;
                }
                else if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    displayName = user.Email;
                }
                else if (!string.IsNullOrWhiteSpace(user.UserName))
                {
                    displayName = user.UserName;
                }

                currentUserId = user.Id;
            }

            ViewBag.CurrentUserName = displayName;
            ViewBag.CurrentUserId = currentUserId;

            return View();
        }
    }
}

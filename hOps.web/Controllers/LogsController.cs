using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace hOps.web.Controllers
{
    public class LogsController : BaseController
    {
        public LogsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "Logs";
            return View();
        }
    }
}

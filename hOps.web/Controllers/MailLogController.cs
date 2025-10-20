using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace hOps.web.Controllers
{
    public class MailLogController : BaseController
    {
        public MailLogController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "Package & Mail Log";
            return View();
        }
    }
}

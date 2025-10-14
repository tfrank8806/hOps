using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace hOps.web.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult SwitchProperty(int propertyId)
        {
            HttpContext.Session.SetInt32("CurrentPropertyId", propertyId);
            return RedirectToAction(nameof(Index));
        }
    }
}

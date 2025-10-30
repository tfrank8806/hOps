using Microsoft.AspNetCore.Mvc;

namespace HotelOpsWeb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult SwitchProperty(int propertyId)
        {
            HttpContext.Session.SetInt32("CurrentPropertyId", propertyId);
            return RedirectToAction("Index", "Home");
        }

    }
}

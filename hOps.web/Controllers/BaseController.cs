using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

public class BaseController : Controller
{
    protected readonly ApplicationDbContext _context;
    protected readonly UserManager<ApplicationUser> _userManager;

    public BaseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var props = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == user.Id)
                .Include(upa => upa.Property)
                .Select(upa => upa.Property)
                .ToListAsync();

            ViewBag.UserProperties = props;

            int? currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            Property? currentProperty = currentPropertyId.HasValue
                ? props.FirstOrDefault(p => p.Id == currentPropertyId.Value)
                : null;

            if (currentProperty == null && user.DefaultPropertyId.HasValue)
            {
                currentProperty = props.FirstOrDefault(p => p.Id == user.DefaultPropertyId.Value);
                if (currentProperty != null)
                {
                    HttpContext.Session.SetInt32("CurrentPropertyId", currentProperty.Id);
                }
            }

            // Fallback to first property if session value is missing or invalid
            if (currentProperty == null && props.Any())
            {
                currentProperty = props.First();
                HttpContext.Session.SetInt32("CurrentPropertyId", currentProperty.Id);
            }
            else if (currentProperty == null)
            {
                HttpContext.Session.Remove("CurrentPropertyId");
            }

            ViewBag.CurrentProperty = currentProperty;

            var normalizedTimeZoneId = DefaultTimeZoneProvider.NormalizeForStorage(user.TimeZoneId);
            HttpContext.Items["UserTimeZoneId"] = normalizedTimeZoneId;
            HttpContext.Session.SetString("UserTimeZoneId", normalizedTimeZoneId);
        }

        await next(); // Continue with the request
    }
}

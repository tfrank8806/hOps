using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace hOps.web.Areas.Identity.Pages.Account
{
    public class RegisterConfirmationModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        public IActionResult OnGet(string? email = null)
        {
            Email = email ?? string.Empty;
            return Page();
        }
    }
}


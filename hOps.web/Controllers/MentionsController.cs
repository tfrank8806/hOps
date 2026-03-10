using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace hOps.web.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MentionsController : BaseController
    {
        private readonly MentionService _mentionService;

        public MentionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MentionService mentionService)
            : base(context, userManager)
        {
            _mentionService = mentionService;
        }

        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var suggestions = await _mentionService.SearchEntitiesAsync(currentUser, term);
            return Json(suggestions);
        }
    }
}


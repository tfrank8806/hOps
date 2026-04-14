using System.Security.Claims;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace hOps.web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtTokenService _tokenService;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IJwtTokenService tokenService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var lookup = request.UsernameOrEmail.Trim();
            ApplicationUser? user = await _userManager.FindByEmailAsync(lookup);
            user ??= await _userManager.FindByNameAsync(lookup);

            if (user == null)
            {
                await Task.Delay(400);
                return Unauthorized(new { error = "Invalid credentials." });
            }

            if (user.MustChangePassword)
            {
                return Unauthorized(new { error = "Password reset required before using the mobile app." });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { error = "Account has been deactivated. Contact an administrator." });
            }

            var passwordResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!passwordResult.Succeeded)
            {
                return Unauthorized(new { error = "Invalid credentials." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var additionalClaims = new[]
            {
                new Claim("defaultPropertyId", user.DefaultPropertyId?.ToString() ?? string.Empty)
            };
            var token = _tokenService.GenerateToken(user, additionalClaims, roles);

            var response = new LoginResponse
            {
                AccessToken = token.AccessToken,
                ExpiresAtUtc = token.ExpiresAtUtc,
                User = new UserSummaryViewModel
                {
                    Id = user.Id,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Email = user.Email ?? string.Empty,
                    ProfilePhotoUrl = user.ProfilePhotoPath,
                    Roles = roles.ToArray()
                }
            };

            return Ok(response);
        }
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using hOps.web.Models;
using hOps.web.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace hOps.web.Services
{
    public interface IJwtTokenService
    {
        JwtTokenResult GenerateToken(ApplicationUser user, IEnumerable<Claim>? additionalClaims = null, IEnumerable<string>? roles = null);
    }

    public sealed class JwtTokenResult
    {
        public string AccessToken { get; init; } = string.Empty;
        public DateTime ExpiresAtUtc { get; init; }
    }

    internal sealed class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _options;
        private readonly byte[] _signingKeyBytes;
        private readonly JwtSecurityTokenHandler _tokenHandler = new();

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value ?? new JwtOptions();
            _signingKeyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        }

        public JwtTokenResult GenerateToken(ApplicationUser user, IEnumerable<Claim>? additionalClaims = null, IEnumerable<string>? roles = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_options.AccessTokenMinutes <= 0 ? 60 : _options.AccessTokenMinutes);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
            };

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            if (additionalClaims != null)
            {
                claims.AddRange(additionalClaims);
            }

            if (roles != null)
            {
                foreach (var role in roles)
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }
            }

            var signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_signingKeyBytes),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: signingCredentials);

            return new JwtTokenResult
            {
                AccessToken = _tokenHandler.WriteToken(token),
                ExpiresAtUtc = expires
            };
        }
    }
}

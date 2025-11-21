using System.Net.Http.Json;

namespace hOps.Mobile.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default);
    }

    internal sealed class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ISecureTokenStore _tokenStore;

        public AuthService(HttpClient httpClient, ISecureTokenStore tokenStore)
        {
            _httpClient = httpClient;
            _tokenStore = tokenStore;
        }

        public async Task<LoginResponse?> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
        {
            var payload = new LoginRequest
            {
                UsernameOrEmail = usernameOrEmail,
                Password = password
            };

            var response = await _httpClient.PostAsJsonAsync("api/auth/login", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(login?.AccessToken))
            {
                await _tokenStore.SaveTokenAsync(login.AccessToken);
            }

            return login;
        }
    }

    public sealed class LoginRequest
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public UserSummaryDto User { get; set; } = new();
    }

    public sealed class UserSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePhotoUrl { get; set; }
    }
}

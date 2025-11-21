using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace hOps.Mobile.Services
{
    public interface IApiClient
    {
        Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default);
        Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken = default);
    }

    internal sealed class ApiClient : IApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ISecureTokenStore _tokenStore;

        public ApiClient(HttpClient httpClient, ISecureTokenStore tokenStore)
        {
            _httpClient = httpClient;
            _tokenStore = tokenStore;
        }

        public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        {
            using var request = await CreateRequestAsync(HttpMethod.Get, path);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken = default)
        {
            using var request = await CreateRequestAsync(HttpMethod.Post, path);
            request.Content = JsonContent.Create(payload);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);

            var token = await _tokenStore.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }
    }

    public interface ISecureTokenStore
    {
        Task SaveTokenAsync(string token);
        Task<string?> GetTokenAsync();
        Task ClearAsync();
    }

    internal sealed class SecureTokenStore : ISecureTokenStore
    {
        private const string TokenKey = "auth_token";
        private readonly IDictionary<string, string> _memoryStore = new Dictionary<string, string>();

        public Task SaveTokenAsync(string token)
        {
            _memoryStore[TokenKey] = token;
            return Task.CompletedTask;
        }

        public Task<string?> GetTokenAsync()
        {
            _memoryStore.TryGetValue(TokenKey, out var token);
            return Task.FromResult<string?>(token);
        }

        public Task ClearAsync()
        {
            _memoryStore.Remove(TokenKey);
            return Task.CompletedTask;
        }
    }
}

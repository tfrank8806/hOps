namespace hOps.Mobile.Services
{
    public interface ISecureTokenStore
    {
        Task SaveTokenAsync(string token);
        Task<string?> GetTokenAsync();
        Task ClearAsync();
        Task<bool> HasTokenAsync();
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

        public Task<bool> HasTokenAsync()
        {
            return Task.FromResult(_memoryStore.ContainsKey(TokenKey));
        }
    }
}

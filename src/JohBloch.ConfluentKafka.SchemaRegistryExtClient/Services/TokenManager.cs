using System;
using System.Threading.Tasks;
namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    public class TokenManager : ITokenManager, IDisposable
    {
        private readonly Func<Task<(string token, DateTime expiresAt)>> _refreshFunc;
        private readonly object _lock = new();
        private string _accessToken = string.Empty;
        private DateTime _expiresAt = DateTime.MinValue;
        private bool _disposed;

        public event EventHandler<string>? TokenRefreshed;
        public event EventHandler<Exception>? TokenRefreshFailed;

        /// <summary>
        /// Gets the current access token.
        /// </summary>
        public string AccessToken => _accessToken;

        /// <summary>
        /// Gets the expiration time of the current token.
        /// </summary>
        public DateTime ExpiresAt => _expiresAt;

        private readonly Interfaces.IMetricsCollector? _metrics;

        public TokenManager(Func<Task<(string token, DateTime expiresAt)>> refreshFunc, Interfaces.IMetricsCollector? metrics = null)
        {
            _refreshFunc = refreshFunc ?? throw new ArgumentNullException(nameof(refreshFunc));
            _metrics = metrics;
        }

        /// <summary>
        /// Gets the token, refreshing if expired.
        /// </summary>
        public async Task<string?> GetTokenAsync()
        {
            // Use the conservative refresh strategy to ensure tokens are refreshed when near expiry
            return await GetTokenAndRefreshIfNeededAsync();
        }

        /// <summary>
        /// Gets the token, refreshing if less than 1 minute left.
        /// </summary>
        public async Task<string> GetTokenAndRefreshIfNeededAsync()
        {
            var threshold = TimeSpan.FromMinutes(1);
            // Avoid subtracting from MinValue which would throw; treat MinValue as expired
            if (_expiresAt == DateTime.MinValue || DateTime.UtcNow >= _expiresAt - threshold)
            {
                await RefreshTokenAsync();
            }
            return _accessToken;
        }

        /// <summary>
        /// Forces a token refresh.
        /// </summary>
        public async Task ForceRefreshAsync()
        {
            await RefreshTokenAsync();
        }

        private async Task RefreshTokenAsync()
        {
            try
            {
                // Thread-safe refresh
                lock (_lock)
                {
                    // Check again inside lock
                    if (DateTime.UtcNow < _expiresAt)
                        return;
                }
                var (token, expiresAt) = await _refreshFunc();
                lock (_lock)
                {
                    _accessToken = token;
                    _expiresAt = expiresAt;
                }
                _metrics?.IncrementTokenRefresh();
                TokenRefreshed?.Invoke(this, token);
            }
            catch (Exception ex)
            {
                TokenRefreshFailed?.Invoke(this, ex);
                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // No unmanaged resources, but pattern for future
                _disposed = true;
            }
        }
    }
}

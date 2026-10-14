using System.Globalization;

namespace ReceptyOks.Services
{
    /// <summary>
    /// Persists the JWT returned by <c>/api/auth/validate</c> in SecureStorage and exposes it
    /// to <see cref="BearerTokenHandler"/>. Treats the token as expired slightly before the
    /// server does to avoid sending a token that dies in flight.
    /// </summary>
    public static class AccessTokenStore
    {
        public const string TokenKey = "AccessToken";
        public const string ExpiresKey = "AccessTokenExpiresAt";

        private static readonly TimeSpan ExpirySkew = TimeSpan.FromMinutes(1);
        private static readonly SemaphoreSlim _gate = new(1, 1);
        private static string? _token;
        private static DateTimeOffset _expiresAt;
        private static bool _loaded;

        public static async Task SaveAsync(string token, DateTimeOffset expiresAt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await SecureStorage.SetAsync(TokenKey, token).ConfigureAwait(false);
                await SecureStorage.SetAsync(ExpiresKey, expiresAt.ToString("O", CultureInfo.InvariantCulture)).ConfigureAwait(false);
                _token = token;
                _expiresAt = expiresAt;
                _loaded = true;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Returns the stored token, or <c>null</c> when none is stored or it has (nearly) expired.</summary>
        public static async Task<string?> GetValidTokenAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_loaded)
                {
                    _token = await SecureStorage.GetAsync(TokenKey).ConfigureAwait(false);
                    var raw = await SecureStorage.GetAsync(ExpiresKey).ConfigureAwait(false);
                    _expiresAt = DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                        ? parsed
                        : DateTimeOffset.MinValue;
                    _loaded = true;
                }

                if (string.IsNullOrEmpty(_token) || DateTimeOffset.UtcNow >= _expiresAt - ExpirySkew)
                    return null;

                return _token;
            }
            finally
            {
                _gate.Release();
            }
        }

        public static async Task ClearAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                SecureStorage.Remove(TokenKey);
                SecureStorage.Remove(ExpiresKey);
                _token = null;
                _expiresAt = DateTimeOffset.MinValue;
                _loaded = true;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}

using ReceptyOks.Configuration;
using ReceptyOks.Shared;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReceptyOks.Services
{
    public class TokenProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;

        public TokenProviderService(HttpClient httpClient, AppSettings appSettings)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            _httpClient = httpClient;
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        }

        public async Task<TokenResponse?> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            if (_appSettings.Http?.Github?.UserAgent == null)
                throw new InvalidOperationException("UserAgent is not configured");

            var request = new TokenRequest(await SecureSecretService.GetSecretBytesAsync(GlobalConstants.ApiKeyHeaderName), _appSettings.Http?.Github?.UserAgent);

            using var response = await _httpClient.PostAsJsonAsync("/api/tokenprovider/token", request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private sealed record TokenRequest(byte[] SecretHash, string UserName);

        public sealed class TokenResponse
        {
            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;

            [JsonPropertyName("expiresIn")]
            public int ExpiresIn { get; set; }
        }
    }
}

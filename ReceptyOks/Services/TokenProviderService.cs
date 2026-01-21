using ReceptyOks.Configuration;
using ReceptyOks.Shared;
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
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request),
                    System.Text.Encoding.UTF8,"application/json");

            using var response = await _httpClient.PostAsync("/api/tokenprovider/token", content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(responseString);

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

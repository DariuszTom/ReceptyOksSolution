using ReceptyOks.Shared;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReceptyOks.Services
{
    public class BackendAuthService
    {
        private readonly HttpClient _httpClient;

        public BackendAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> IsCorrectSecretStored(string apiKeyStorageKey = GlobalConstants.ApiKeyHeaderName)
        {
            var bytes = await SecureSecretService.GetSecretBytesAsync(apiKeyStorageKey).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0) return false;

            var result = await IsApiKeyValid(bytes).ConfigureAwait(false);
            return result.IsValid;
        }

        public async Task<AuthResponse> IsApiKeyValid(byte[] apiKey)
        {
            if (apiKey == null || apiKey.Length == 0)
                throw new ArgumentException("API key must not be empty.", nameof(apiKey));

            var authResponse = new AuthResponse { IsValid = false, Message = "Nieprawidłowy sekret" };
            try
            {
                using var response = await _httpClient.PostAsJsonAsync("/api/auth/validate", new { SecretHash = apiKey }).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AuthResponse>().ConfigureAwait(false)
                        ?? new AuthResponse { IsValid = false, Message = "Nieznany bład" };
                }
            }
            catch (Exception)
            {
                authResponse = new AuthResponse { IsValid = false, Message = "Bład serwisu" };
            }
            return authResponse;
        }

        [JsonSerializable(typeof(AuthResponse))]
        public sealed class AuthResponse
        {
            [JsonPropertyName("isValid")]
            public bool IsValid { get; set; }
            [JsonPropertyName("message")]
            public string? Message { get; set; }
        }
    }

}

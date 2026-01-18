using ReceptyOks.Shared;
using System.Text.Json.Serialization;

namespace ReceptyOks.Services
{
    public class BackendAuthService
    {
        private HttpClient _httpClient;
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

            var requestBody = new { SecretHash = Convert.ToBase64String(apiKey) };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");
            AuthResponse authResponse = new AuthResponse() { IsValid = false, Message = "Nieprawidłowy sekret" };
            try
            {
                using var response = await _httpClient.PostAsync("/api/auth/validate", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var correctResponse = System.Text.Json.JsonSerializer.Deserialize<AuthResponse>(responseString);
                    return correctResponse ?? new AuthResponse() { IsValid = false, Message = "Nieznany bład" };
                }
            }
            catch (Exception)
            {
                authResponse = new AuthResponse() { IsValid = false, Message = "Bład serwisu" };
            }
            return authResponse;
        }
        [JsonSerializable(typeof(AuthResponse))]
        public sealed class AuthResponse
        {
            [JsonPropertyName("isValid")]
            public bool IsValid { get; set; }
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }
    }

}

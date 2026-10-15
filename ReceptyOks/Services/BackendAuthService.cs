using ReceptyOks.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReceptyOks.Services
{
    /// <summary>
    /// Distinguishes a server-verified rejection from a network/cold-start failure
    /// so callers can decide whether to allow offline access.
    /// </summary>
    public enum AuthOutcome
    {
        Valid,
        Invalid,
        Unreachable
    }

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
                var clientId = await ClientIdentityService.GetOrCreateAsync().ConfigureAwait(false);
                using var response = await _httpClient.PostAsJsonAsync("/api/auth/validate", new ValidateRequest(apiKey, clientId), AuthJsonContext.Default.ValidateRequest).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var parsed = await response.Content.ReadFromJsonAsync(AuthJsonContext.Default.AuthResponse).ConfigureAwait(false)
                        ?? new AuthResponse { IsValid = false, Message = "Nieznany bład" };
                    await StoreTokenAsync(parsed).ConfigureAwait(false);
                    return parsed;
                }
            }
            catch (Exception)
            {
                authResponse = new AuthResponse { IsValid = false, Message = "Bład serwisu" };
            }
            return authResponse;
        }

        /// <summary>
        /// Validates the secret against the backend and reports whether the outcome was a
        /// definitive server response (Valid/Invalid) or a network failure/timeout (Unreachable).
        /// Use <see cref="AuthOutcome.Unreachable"/> to decide whether an offline fallback is safe.
        /// Pass a <paramref name="cancellationToken"/> to cap the wait time (e.g. for the fast
        /// auto-login path against a possibly cold-starting Container App).
        /// </summary>
        public async Task<(AuthOutcome Outcome, AuthResponse Response)> TryValidateAsync(byte[] apiKey, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(apiKey);
            if (apiKey.Length == 0)
                throw new ArgumentException("API key must not be empty.", nameof(apiKey));

            try
            {
                var clientId = await ClientIdentityService.GetOrCreateAsync().ConfigureAwait(false);
                using var response = await _httpClient.PostAsJsonAsync("/api/auth/validate", new ValidateRequest(apiKey, clientId), AuthJsonContext.Default.ValidateRequest, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = await response.Content.ReadFromJsonAsync(AuthJsonContext.Default.AuthResponse, cancellationToken).ConfigureAwait(false)
                        ?? new AuthResponse { IsValid = false, Message = "Nieznany bład" };
                    await StoreTokenAsync(parsed).ConfigureAwait(false);
                    return (parsed.IsValid ? AuthOutcome.Valid : AuthOutcome.Invalid, parsed);
                }

                // Server responded but refused the secret -> definitively invalid.
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    return (AuthOutcome.Invalid, new AuthResponse { IsValid = false, Message = "Nieprawidłowy sekret" });
                }

                // 5xx / 408 / etc. -> treat as transient / cold-start.
                return (AuthOutcome.Unreachable, new AuthResponse { IsValid = false, Message = "Serwer niedostępny" });
            }
            catch (OperationCanceledException)
            {
                // Caller-supplied timeout OR HttpClient internal timeout - both indicate unreachable.
                return (AuthOutcome.Unreachable, new AuthResponse { IsValid = false, Message = "Przekroczono limit czasu połączenia" });
            }
            catch (HttpRequestException)
            {
                return (AuthOutcome.Unreachable, new AuthResponse { IsValid = false, Message = "Brak połączenia z serwerem" });
            }
            catch (Exception)
            {
                return (AuthOutcome.Unreachable, new AuthResponse { IsValid = false, Message = "Bład serwisu" });
            }
        }

        /// <summary>
        /// Persists the JWT issued by the server so <see cref="BearerTokenHandler"/> can attach it
        /// to owner-scoped requests (e.g. HomeSeeker). Storage failures must not break login.
        /// </summary>
        private static async Task StoreTokenAsync(AuthResponse response)
        {
            if (!response.IsValid || string.IsNullOrEmpty(response.Token) || response.ExpiresAt is null)
                return;

            try
            {
                await AccessTokenStore.SaveAsync(response.Token, response.ExpiresAt.Value).ConfigureAwait(false);
            }
            catch
            {
                // SecureStorage unavailable - the user is still authenticated via the API key.
            }
        }

        public sealed class AuthResponse
        {
            [JsonPropertyName("isValid")]
            public bool IsValid { get; set; }
            [JsonPropertyName("message")]
            public string? Message { get; set; }
            [JsonPropertyName("token")]
            public string? Token { get; set; }
            [JsonPropertyName("expiresAt")]
            public DateTimeOffset? ExpiresAt { get; set; }
        }

        public sealed record ValidateRequest(byte[] SecretHash, string ClientId);
    }

    [JsonSerializable(typeof(BackendAuthService.AuthResponse))]
    [JsonSerializable(typeof(BackendAuthService.ValidateRequest))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    internal partial class AuthJsonContext : JsonSerializerContext
    {
    }
}

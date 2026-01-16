using ReceptyOks.Shared;
using System.Security.Cryptography;
using System.Text;

namespace ReceptyOks.Api.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    // Cached decoded bytes (provided by SecretStore)
    private readonly byte[]? _storedHashBytes;
    private readonly byte[]? _hmacKeyBytes;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ApiKeyAuthMiddleware> logger,
        SecretStore secretStore)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;

        // Use SecretStore to get cached secret bytes
        secretStore.Initialize();
        var (passwordHash, secretKey) = secretStore.GetSecrets();
        _storedHashBytes = passwordHash;
        _hmacKeyBytes = secretKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Pomijamy autoryzacji dla:
        // - endpointu auth (¿eby mo¿na by³o siê uwierzytelniæ)
        // - health checks
        // - OpenAPI/Scalar (tylko w development)
        if (ShouldSkipAuth(path, context.RequestServices.GetRequiredService<IWebHostEnvironment>()))
        {
            await _next(context);
            return;
        }

        // Sprawdzamy nag³ówek X-Api-Key
        if (!context.Request.Headers.TryGetValue(GlobalConstants.ApiKeyHeaderName, out var providedApiKey))
        {
            _logger.LogWarning("Request to {Path} rejected - missing {Header} header", path, GlobalConstants.ApiKeyHeaderName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
            return;
        }

        if (_storedHashBytes == null)
        {
            _logger.LogError("ApiAuth:PasswordHash is not configured or could not be decoded");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Server configuration error" });
            return;
        }

        var provided = providedApiKey.ToString().Trim();
        byte[] providedDerived;

        if (_hmacKeyBytes != null && _hmacKeyBytes.Length > 0)
        {
            // Compute HMAC-SHA256(provided) using configured secret key
            using var hmac = new HMACSHA256(_hmacKeyBytes);
            providedDerived = hmac.ComputeHash(Encoding.UTF8.GetBytes(provided));
        }
        else
        {
            // Fallback: compare raw UTF8 bytes (still constant-time)
            providedDerived = Encoding.UTF8.GetBytes(provided);
        }

        var isValid = CryptographicOperations.FixedTimeEquals(_storedHashBytes, providedDerived);

        if (!isValid)
        {
            _logger.LogWarning("Request to {Path} rejected - invalid API key", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        await _next(context);
    }

    private static byte[] DecodeStringToBytes(string value)
    {
        // Try Base64
        try
        {
            var b = Convert.FromBase64String(value);
            if (b.Length > 0) return b;
        }
        catch { }

        // Try hex (Convert.FromHexString available on modern runtimes)
        try
        {
            var hex = value;
            // allow optional 0x prefix
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex[2..];
            }

            // Convert.FromHexString may throw FormatException
            var bytes = Convert.FromHexString(hex);
            if (bytes.Length > 0) return bytes;
        }
        catch { }

        // Fallback to UTF8 bytes
        return Encoding.UTF8.GetBytes(value);
    }

    private static bool ShouldSkipAuth(string path, IWebHostEnvironment environment)
    {
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (environment.IsDevelopment())
        {
            if (path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Extension method do rejestracji middleware.
/// </summary>
public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}

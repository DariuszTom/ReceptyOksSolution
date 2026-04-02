using ReceptyOks.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace ReceptyOks.Api.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly PartitionedRateLimiter<HttpContext> _rateLimiter;

    // Cached decoded bytes (provided by SecretStore)
    private readonly byte[]? _storedHashBytes;
    private readonly byte[]? _hmacKeyBytes;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthMiddleware> logger,
        PartitionedRateLimiter<HttpContext> rateLimiter,
        SecretStore secretStore)
    {
        _next = next;
        _logger = logger;
        _rateLimiter = rateLimiter;

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
        // - endpointu auth (żeby można było się uwierzytelnić)
        // - health checks
        // - OpenAPI/Scalar (tylko w development)
        if (ShouldSkipAuth(path, context.RequestServices.GetRequiredService<IWebHostEnvironment>()))
        {
            await _next(context);
            return;
        }

        // Rate limiting (per-partition) - przed sprawdzeniem klucza
        using var lease = await _rateLimiter.AcquireAsync(context, permitCount: 1, context.RequestAborted).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            _logger.LogWarning("Rate limit exceeded for request to {Path}", path);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.Response.Headers["Retry-After"] = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            }

            await context.Response.WriteAsJsonAsync(new { error = "Too many requests. Please try again later." }).ConfigureAwait(false);
            return;
        }

        // Sprawdzamy nagłówek X-Api-Key
        if (!context.Request.Headers.TryGetValue(GlobalConstants.ApiKeyHeaderName, out var providedApiKey))
        {
            _logger.LogWarning("Request to {Path} rejected - missing {Header} header", path, GlobalConstants.ApiKeyHeaderName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" }).ConfigureAwait(false);
            return;
        }

        if (_storedHashBytes == null)
        {
            _logger.LogError("ApiAuth:PasswordHash is not configured or could not be decoded");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Server configuration error" }).ConfigureAwait(false);
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
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" }).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }


    private static bool ShouldSkipAuth(string path, IWebHostEnvironment environment)
    {
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/alive", StringComparison.OrdinalIgnoreCase) ||
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

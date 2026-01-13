using System.Security.Cryptography;
using System.Text;

namespace ReceptyOks.Api.Middleware;

/// <summary>
/// Middleware sprawdzaj?cy nag?ówek X-Api-Key z zahashowanym has?em.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Pomijamy autoryzacj? dla:
        // - endpointu auth (?eby mo?na by?o si? uwierzytelni?)
        // - health checks
        // - OpenAPI/Scalar (tylko w development)
        if (ShouldSkipAuth(path, context.RequestServices.GetRequiredService<IWebHostEnvironment>()))
        {
            await _next(context);
            return;
        }

        // Sprawdzamy nag?ówek X-Api-Key
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
        {
            _logger.LogWarning("Request to {Path} rejected - missing {Header} header", path, ApiKeyHeaderName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
            return;
        }

        var storedHash = _configuration["ApiAuth:PasswordHash"];

        if (string.IsNullOrEmpty(storedHash))
        {
            _logger.LogError("ApiAuth:PasswordHash is not configured");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Server configuration error" });
            return;
        }

        // Constant-time comparison dla bezpiecze?stwa
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash),
            Encoding.UTF8.GetBytes(providedApiKey.ToString()));

        if (!isValid)
        {
            _logger.LogWarning("Request to {Path} rejected - invalid API key", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkipAuth(string path, IWebHostEnvironment environment)
    {
        // Zawsze pomijamy auth i health checks
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // W development pomijamy te? OpenAPI i Scalar
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

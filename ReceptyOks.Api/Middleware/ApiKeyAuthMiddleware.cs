using ReceptyOks.Api.Extensions;
using ReceptyOks.Shared;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace ReceptyOks.Api.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    // StartsWithSegments matches whole segments only, so "/healthz" or "/api/authx" don't bypass auth.
    private static readonly PathString[] AnonymousPaths =
    [
        ApiRoutes.Auth.Base,
        ApiRoutes.Health,
        ApiRoutes.Alive,
        ApiRoutes.Prefix + ApiRoutes.Health,
        ApiRoutes.Prefix + ApiRoutes.Alive,
        ApiRoutes.Robots
    ];

    private static readonly PathString[] DevelopmentAnonymousPaths = ["/openapi", "/scalar"];

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly PartitionedRateLimiter<HttpContext> _rateLimiter;
    private readonly bool _isDevelopment;
    private readonly byte[]? _storedHashBytes;
    private readonly byte[]? _hmacKeyBytes;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthMiddleware> logger,
        PartitionedRateLimiter<HttpContext> rateLimiter,
        SecretStore secretStore,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _rateLimiter = rateLimiter;
        _isDevelopment = environment.IsDevelopment();

        secretStore.Initialize();
        var (passwordHash, secretKey) = secretStore.GetSecrets();
        if (passwordHash is { Length: > 0 } && secretKey is { Length: > 0 })
        {
            _storedHashBytes = passwordHash;
            _hmacKeyBytes = secretKey;
        }
        else
        {
            _logger.LogCritical("PasswordHash or SecretKey is not configured or could not be decoded - all protected requests will fail");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        if (ShouldSkipAuth(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var clientIp = context.GetClientIp();

        using var lease = await _rateLimiter.AcquireAsync(context, permitCount: 1, context.RequestAborted).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            _logger.LogWarning("Rate limit exceeded for request from {ClientIp} to {Path}", clientIp, path);

            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            await WriteErrorAsync(context, StatusCodes.Status429TooManyRequests, "Too many requests. Please try again later.").ConfigureAwait(false);
            return;
        }

        var providedApiKey = context.Request.Headers[GlobalConstants.ApiKeyHeaderName].ToString().Trim();
        if (providedApiKey.Length == 0)
        {
            _logger.LogWarning("Request from {ClientIp} to {Path} rejected - missing {Header} header", clientIp, path, GlobalConstants.ApiKeyHeaderName);
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "API key is required").ConfigureAwait(false);
            return;
        }

        if (_storedHashBytes is null || _hmacKeyBytes is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "Server configuration error").ConfigureAwait(false);
            return;
        }

        var providedDerived = HMACSHA256.HashData(_hmacKeyBytes, Encoding.UTF8.GetBytes(providedApiKey));
        if (!CryptographicOperations.FixedTimeEquals(_storedHashBytes, providedDerived))
        {
            _logger.LogWarning("Request from {ClientIp} to {Path} rejected - invalid API key", clientIp, path);
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "Invalid API key").ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool ShouldSkipAuth(PathString path) =>
        MatchesAny(path, AnonymousPaths) ||
        (_isDevelopment && MatchesAny(path, DevelopmentAnonymousPaths));

    private static bool MatchesAny(PathString path, PathString[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new { error });
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}

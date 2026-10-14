using ReceptyOks.Api.Services;
using ReceptyOks.Shared.Misc;
using System.Security.Cryptography;

namespace ReceptyOks.Api.Endpoints;

/// <summary>
/// Simple endpoints for authentication using a hashed password.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Upper bound for the client-supplied identifier stored in <c>SearchProfiles.OwnerId</c>.</summary>
    private const int MaxClientIdLength = 200;

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("strict")
            .DisableHttpMetrics();

        group.MapPost("/validate", (AuthRequest request, IConfiguration configuration, JwtTokenService tokens) =>
        {
            var storedHash = configuration["PasswordHash"];
            var secretKey = configuration["SecretKey"];

            if (string.IsNullOrEmpty(storedHash))
            {
                return Results.Problem(
                    detail: "Server configuration error",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (string.IsNullOrEmpty(secretKey))
            {
                return Results.Problem(
                    detail: "Server configuration error",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            // Decode the configured secret key (it's stored as Base64); fall back to UTF8 if not Base64
            var hmacKeyBytes = secretKey.DecodeBase64OrHexToBytes();
            using var hmac = new HMACSHA256(hmacKeyBytes);

            // Use provided bytes or empty array - always perform the comparison
            // to prevent timing-based information disclosure
            var providedBytes = request.SecretHash ?? [];
            var providedDerived = hmac.ComputeHash(providedBytes);

            // Stored hash is likely Base64-encoded HMAC; decode it before comparing
            var storedBytes = storedHash.DecodeBase64OrHexToBytes();

            // Always perform constant-time comparison; empty/null input will naturally fail
            var isValid = providedBytes.Length > 0 &&
                          storedBytes.Length == providedDerived.Length &&
                          CryptographicOperations.FixedTimeEquals(storedBytes, providedDerived);

            if (!isValid)
                return Results.Unauthorized();

            // Shared secret is correct. If the client identified itself, mint a JWT whose
            // `sub` is that id so owner-scoped endpoints (HomeSeeker) can attribute data to it.
            // Clients that don't send a ClientId keep the legacy API-key-only behaviour.
            var clientId = request.ClientId?.Trim();
            if (string.IsNullOrEmpty(clientId))
                return Results.Ok(new AuthResponse(true, "Authenticated"));

            if (clientId.Length > MaxClientIdLength)
                return Results.BadRequest(new { Message = $"ClientId must be at most {MaxClientIdLength} characters" });

            var (token, expiresAt) = tokens.Issue(clientId);
            return Results.Ok(new AuthResponse(true, "Authenticated", token, expiresAt));
        })
        .WithName("ValidatePassword")
        .WithDescription("Validates the password hash against the stored value. When ClientId is supplied, also returns a JWT (sub = ClientId) for owner-scoped endpoints.");
    }

}

/// <summary>
/// Request for password validation.
/// </summary>
/// <param name="SecretHash">Raw shared-secret bytes (server derives the HMAC).</param>
/// <param name="ClientId">Optional stable identifier of the calling device/user; becomes the JWT <c>sub</c> and the owner of HomeSeeker data.</param>
public sealed record AuthRequest(byte[] SecretHash, string? ClientId = null);

/// <summary>
/// Validation response. <see cref="Token"/> and <see cref="ExpiresAt"/> are populated only when a <c>ClientId</c> was sent.
/// </summary>
public sealed record AuthResponse(bool IsValid, string Message, string? Token = null, DateTime? ExpiresAt = null);

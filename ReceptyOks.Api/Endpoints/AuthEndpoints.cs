using ReceptyOks.Shared.Misc;
using System.Security.Cryptography;

namespace ReceptyOks.Api.Endpoints;

/// <summary>
/// Proste endpointy do uwierzytelniania za pomoc¹ zahashowanego has³a.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("strict")
            .DisableHttpMetrics();

        group.MapPost("/validate", (AuthRequest request, IConfiguration configuration) =>
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

            return isValid
                ? Results.Ok(new AuthResponse(true, "Authenticated"))
                : Results.Unauthorized();
        })
        .WithName("ValidatePassword")
        .WithDescription("Validates the password hash against the stored value");
    }

}

/// <summary>
/// Request do walidacji hasła.
/// </summary>
public sealed record AuthRequest(byte[] SecretHash);

/// <summary>
/// Odpowiedź z walidacji.
/// </summary>
public sealed record AuthResponse(bool IsValid, string Message);

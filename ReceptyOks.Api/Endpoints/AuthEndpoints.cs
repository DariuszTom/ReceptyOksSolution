using ReceptyOks.Shared.Misc;
using System.ClientModel.Primitives;
using System.Security.Cryptography;
using System.Text;

namespace ReceptyOks.Api.Endpoints;

/// <summary>
/// Proste endpointy do uwierzytelniania za pomocπ zahashowanego has≥a.
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

            if (request.SecretHash == null || request.SecretHash.Length == 0)
            {
                return Results.BadRequest(new AuthResponse(false, "Secret hash is required"));
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

            // Decode the provided secret if it's Base64-encoded (clients often send Base64); otherwise use raw UTF8 bytes
            var providedBytes = request.SecretHash;
            var providedDerived = hmac.ComputeHash(providedBytes);

            // Stored hash is likely Base64-encoded HMAC; decode it before comparing
            var storedBytes = storedHash.DecodeBase64OrHexToBytes();
            // Ensure same length before constant-time compare
            var isValid = storedBytes.Length == providedDerived.Length &&
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
/// Request do walidacji has≥a.
/// </summary>
public sealed record AuthRequest(byte[] SecretHash);

/// <summary>
/// Odpowiedü z walidacji.
/// </summary>
public sealed record AuthResponse(bool IsValid, string Message);

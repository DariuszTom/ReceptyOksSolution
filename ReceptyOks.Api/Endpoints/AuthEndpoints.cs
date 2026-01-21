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

            if (string.IsNullOrEmpty(request.SecretHash))
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
            var hmacKeyBytes = DecodeToCorrectFormat(secretKey);
            using var hmac = new HMACSHA256(hmacKeyBytes);

            // Decode the provided secret if it's Base64-encoded (clients often send Base64); otherwise use raw UTF8 bytes
            var providedBytes = DecodeToCorrectFormat(request.SecretHash);
            var providedDerived = hmac.ComputeHash(providedBytes);

            // Stored hash is likely Base64-encoded HMAC; decode it before comparing
            var storedBytes =DecodeToCorrectFormat(storedHash);

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
    public static byte[] DecodeToCorrectFormat(string? input)
    {
        if (input == null) return [];
        try
        {
            return Convert.FromBase64String(input);
        }
        catch
        {
            // Fallback to hex
            var hex = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input[2..] : input;
            return Convert.FromHexString(hex);
        }
    }
}

/// <summary>
/// Request do walidacji has≥a.
/// </summary>
public sealed record AuthRequest(string SecretHash);

/// <summary>
/// Odpowiedü z walidacji.
/// </summary>
public sealed record AuthResponse(bool IsValid, string Message);

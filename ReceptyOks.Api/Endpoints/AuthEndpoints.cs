using System.Security.Cryptography;
using System.Text;

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
            .AllowAnonymous();

        group.MapPost("/validate", (AuthRequest request, IConfiguration configuration) =>
        {
            var storedHash = configuration["PasswordHash"];

            if (string.IsNullOrEmpty(storedHash))
            {
                return Results.Problem(
                    detail: "Server configuration error",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (string.IsNullOrEmpty(request.PasswordHash))
            {
                return Results.BadRequest(new AuthResponse(false, "Password hash is required"));
            }

            // Porównanie zahashowanego has³a (constant-time comparison dla bezpieczeñstwa)
            var storedBytes = Encoding.UTF8.GetBytes(storedHash);
            var providedBytes = Encoding.UTF8.GetBytes(request.PasswordHash);
            var isValid = CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes);

            return isValid
                ? Results.Ok(new AuthResponse(true, "Authenticated"))
                : Results.Unauthorized();
        })
        .WithName("ValidatePassword")
        .WithDescription("Validates the password hash against the stored value");
    }
}

/// <summary>
/// Request do walidacji has³a.
/// </summary>
public sealed record AuthRequest(string PasswordHash);

/// <summary>
/// OdpowiedŸ z walidacji.
/// </summary>
public sealed record AuthResponse(bool IsValid, string Message);

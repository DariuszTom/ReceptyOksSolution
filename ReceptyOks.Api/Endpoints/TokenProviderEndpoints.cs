using ReceptyOks.Shared.Misc;

namespace ReceptyOks.Api.Endpoints
{
    public static class TokenProviderEndpoints
    {
        public static void MapTokenProviderEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/tokenprovider")
                .WithTags("Token Provider")
                .RequireRateLimiting("strict")
                .DisableHttpMetrics();     
            
            group.MapPost("/token", (AuthRequest request, IConfiguration configuration) =>
            {
                if (request is null || request.SecretHash?.Length == 0)
                    return Results.BadRequest(new { Message = "SecretHash is required" });

                var storedHash = configuration["PasswordHash"];
                var secretKey = configuration["SecretKey"];

                if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(secretKey))
                {
                    return Results.Problem(detail: "Server configuration error", statusCode: StatusCodes.Status500InternalServerError);
                }
                if(request.UserName != configuration["UserAgent"])
                {
                    return Results.Forbid();
                }
                // Decode configured secret key (Base64 preferred, fallback to UTF8/hex)
                byte[] hmacKeyBytes =secretKey.DecodeBase64OrHexToBytes();

                byte[] providedDerived;
                using (var hmac = new System.Security.Cryptography.HMACSHA256(hmacKeyBytes))
                {
                    providedDerived = hmac.ComputeHash(request.SecretHash);
                }

                byte[] storedBytes=storedHash.DecodeBase64OrHexToBytes();

                storedHash = null;
                var isValid = storedBytes.Length == providedDerived.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(storedBytes, providedDerived);

                if (!isValid)
                    return Results.Unauthorized();

                // Authenticated — return the token (from config/KeyVault).
                var token = configuration["Token"];
                if (string.IsNullOrWhiteSpace(token))
                    return Results.NotFound();

                // Return full token with a suggested short TTL (in minutes).
                return Results.Ok(new { Token = token, ExpiresIn = 10 });
            })
            .WithName("GetAnthropicTokenSecret")
            .WithDescription("Returns the full Anthropic token after validating the caller (less secure; use with caution)");
        }
        public record AuthRequest(byte[] SecretHash, string UserName);
    }
}

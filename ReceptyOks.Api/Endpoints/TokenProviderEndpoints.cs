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
                if(request.UserName != configuration["UserName"])
                {
                    return Results.Forbid();
                }
                // Decode configured secret key (Base64 preferred, fallback to UTF8/hex)
                byte[] hmacKeyBytes;
                try
                {
                    hmacKeyBytes = Convert.FromBase64String(secretKey);
                }
                catch
                {
                    var hex = secretKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? secretKey[2..] : secretKey;
                    try { hmacKeyBytes = Convert.FromHexString(hex); }
                    catch { hmacKeyBytes = System.Text.Encoding.UTF8.GetBytes(secretKey); }
                }

                byte[] providedDerived;
                using (var hmac = new System.Security.Cryptography.HMACSHA256(hmacKeyBytes))
                {
                    providedDerived = hmac.ComputeHash(request.SecretHash);
                }

                byte[] storedBytes;
                try
                {
                    storedBytes = Convert.FromBase64String(storedHash);
                }
                catch
                {
                    var hex = storedHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? storedHash[2..] : storedHash;
                    try { storedBytes = Convert.FromHexString(hex); }
                    catch { storedBytes = System.Text.Encoding.UTF8.GetBytes(storedHash); }
                }
                storedHash = null;
                var isValid = storedBytes.Length == providedDerived.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(storedBytes, providedDerived);

                if (!isValid)
                    return Results.Unauthorized();

                // Authenticated — return the token (from config/KeyVault). Consider making TTL short on client side.
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

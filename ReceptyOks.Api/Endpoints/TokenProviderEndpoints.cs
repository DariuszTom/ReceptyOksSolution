namespace ReceptyOks.Api.Endpoints
{
    public static class TokenProviderEndpoints
    {
        public static void MapTokenProviderEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/tokenprovider")
                .WithTags("Token Provider")
                .DisableHttpMetrics();     
            
            // Return the key. In non-development environments the token is masked for safety.
            group.MapGet("/anthropic", (IConfiguration configuration, IWebHostEnvironment env) =>
            {
                var token = configuration["Token"];
                if (string.IsNullOrWhiteSpace(token))
                    return Results.NotFound();

                    // Mask token except for development to avoid accidental disclosure
                    var masked = token.Length <= 8 ? new string('*', token.Length) : $"{token[..4]}...{token[^4..]}";
                    return Results.Ok(new { Token = masked, IsMasked = true });

            })
            .WithName("GetAnthropicToken")
            .WithDescription("Returns the  token when configured");
        }
    }
}

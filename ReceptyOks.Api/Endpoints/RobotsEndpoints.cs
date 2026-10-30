namespace ReceptyOks.Api.Endpoints;

public static class RobotsEndpoints
{
    // Private API - instruct all crawlers not to index anything.
    private const string RobotsTxtContent = """
        User-agent: *
        Disallow: /

        """;

    public static void MapRobotsEndpoints(this WebApplication app)
    {
        app.MapGet("/robots.txt", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "public, max-age=86400";
            return TypedResults.Text(RobotsTxtContent, "text/plain; charset=utf-8");
        })
        .WithName("GetRobotsTxt")
        .RequireRateLimiting("fixed")
        .ExcludeFromDescription();
    }
}

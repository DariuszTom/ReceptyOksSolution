using ReceptyOks.Shared.AI;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace ReceptyOks.Api.Extensions;

/// <summary>
/// Reverse proxy for the Anthropic Messages API. Clients call <c>/api/ai/v1/messages</c> with the
/// application API key (validated by <see cref="ApiKeyAuthMiddleware"/>); the proxy replaces it with
/// the real Anthropic key from configuration, so the Anthropic key never leaves the server.
/// </summary>
public static class AnthropicProxyExtensions
{
    public const string PathPrefix = "/api/ai";
    public const string RateLimiterPolicy = "ai";

    private const string RouteId = "anthropic-messages";
    private const string ClusterId = "anthropic";
    private const string TokenConfigKey = "Token";
    private const string AnthropicApiKeyHeader = "x-api-key";

    public static IServiceCollection AddAnthropicProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var destination = new AnthropicSettings().BaseUrl ?? throw new InvalidOperationException($"Missing {nameof(AnthropicSettings)}.{nameof(AnthropicSettings.BaseUrl)} configuration");

        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = RouteId,
                ClusterId = ClusterId,
                RateLimiterPolicy = RateLimiterPolicy,
                Match = new RouteMatch
                {
                    Path = $"{PathPrefix}/v1/messages",
                    Methods = ["POST"]
                }
            }
        };

        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = ClusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["anthropic"] = new() { Address = destination }
                },
                HttpRequest = new ForwarderRequestConfig
                {
                    // Long, non-streamed generations can take minutes.
                    ActivityTimeout = TimeSpan.FromMinutes(10)
                }
            }
        };

        services.AddReverseProxy()
            .LoadFromMemory(routes, clusters)
            .AddTransforms(context =>
            {
                if (!string.Equals(context.Route.RouteId, RouteId, StringComparison.Ordinal))
                {
                    return;
                }

                // Do not leak client IPs / proxy details to the upstream provider.
                context.UseDefaultForwarders = false;
                context.AddPathRemovePrefix(PathPrefix);
                context.AddRequestHeaderRemove("Authorization");
                context.AddRequestHeaderRemove("Cookie");
                context.AddRequestTransform(transform =>
                {
                    var token = transform.HttpContext.RequestServices
                        .GetRequiredService<IConfiguration>()[TokenConfigKey];

                    // Drop the client's application API key and inject the real Anthropic key.
                    transform.ProxyRequest.Headers.Remove(AnthropicApiKeyHeader);
                    transform.ProxyRequest.Headers.TryAddWithoutValidation(AnthropicApiKeyHeader, token);
                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }

    public static void AddAnthropicProxyRateLimiter(this Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options)
    {
        options.AddPolicy(RateLimiterPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(context.GetClientIp(), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    }

    public static void MapAnthropicProxy(this WebApplication app)
    {
        app.MapReverseProxy(pipeline =>
        {
            pipeline.Use(async (context, next) =>
            {
                var token = context.RequestServices.GetRequiredService<IConfiguration>()[TokenConfigKey];
                if (string.IsNullOrWhiteSpace(token))
                {
                    context.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger(typeof(AnthropicProxyExtensions))
                        .LogError("Anthropic proxy request rejected - '{Key}' is not configured", TokenConfigKey);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }

                await next().ConfigureAwait(false);
            });
            pipeline.UseLoadBalancing();
        });
    }
}

namespace ReceptyOks.Configuration;

using ReceptyOks.Interfaces;
using ReceptyOks.Services;

/// <summary>
/// Extension methods for registering HTTP clients with the dependency injection container.
/// </summary>
internal static class HttpClientServiceExtensions
{
    /// <summary>Named <see cref="HttpClient"/> that sends both the API key and the owner JWT.</summary>
    public const string HomeSeekerHttpClientName = "HomeSeekerApi";

    /// <summary>
    /// Registers all HTTP clients used by the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="appSettings">Application settings containing HTTP configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHttpClients(this IServiceCollection services, AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);

        var baseUrl = appSettings.Http.GetEffectiveApiUrl();
        var defaultTimeout = TimeSpan.FromSeconds(appSettings.Http.DefaultTimeoutSeconds);

        services.AddTransient<ApiKeyHandler>();
        services.AddTransient<BearerTokenHandler>();

        // Advertise Accept-Encoding (br/gzip/deflate) so the API's response compression actually kicks in.
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler((handler, _) => EnableAutomaticDecompression(handler)));

        // SyncService with extended timeout for Azure SQL sync + large JSON deserialization
        services.AddHttpClient<SyncService>(client =>
      {
          client.BaseAddress = new Uri(baseUrl);
          client.Timeout = TimeSpan.FromMinutes(5); // 300s - enough for full sync + deserialization
      })
      .AddHttpMessageHandler<ApiKeyHandler>()
      .AddServiceDiscovery()
      .SetHandlerLifetime(TimeSpan.FromMinutes(10)); // Prevent handler recycling killing active requests

        // Register ISyncService interface mapping
        services.AddTransient<ISyncService>(sp => sp.GetRequiredService<SyncService>());

        // BackendAuthService
        services.AddHttpClient<BackendAuthService>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = defaultTimeout;
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();

        // Anthropic reverse proxy on the backend - the Anthropic key never reaches the device.
        // The SDK enforces its own per-request timeout, so the HttpClient one is disabled.
        services.AddHttpClient(AiChatClientFactory.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();
        services.AddSingleton<AiChatClientFactory>();

        // ShoppingListService
        services.AddHttpClient<IShoppingListService, ShoppingListService>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = defaultTimeout;
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();

        // Owner-scoped API (HomeSeeker): API key + JWT issued at login (refreshed on demand).
        services.AddTransient<IAccessTokenRefresher, AccessTokenRefresher>();
        services.AddHttpClient<IHomeSeekerService, HomeSeekerService>(HomeSeekerHttpClientName, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = defaultTimeout;
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddHttpMessageHandler<BearerTokenHandler>()
        .AddServiceDiscovery();

        // HealthStatusService - no API key needed for health endpoints
        services.AddHttpClient<IHealthStatusService, HealthStatusService>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10); // Short timeout for health checks
        })
        .AddServiceDiscovery();

        return services;
    }

    private static void EnableAutomaticDecompression(HttpMessageHandler handler)
    {
        switch (handler)
        {
            case HttpClientHandler clientHandler when clientHandler.SupportsAutomaticDecompression:
                clientHandler.AutomaticDecompression = System.Net.DecompressionMethods.All;
                break;
            case SocketsHttpHandler socketsHandler:
                socketsHandler.AutomaticDecompression = System.Net.DecompressionMethods.All;
                break;
        }
    }
}

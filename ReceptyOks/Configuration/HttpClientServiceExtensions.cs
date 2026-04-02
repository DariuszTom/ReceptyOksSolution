namespace ReceptyOks.Configuration;

using ReceptyOks.Interfaces;
using ReceptyOks.Services;

/// <summary>
/// Extension methods for registering HTTP clients with the dependency injection container.
/// </summary>
internal static class HttpClientServiceExtensions
{
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

        // TokenProviderService
        services.AddHttpClient<TokenProviderService>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = defaultTimeout;
            })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();

        // ShoppingListService
        services.AddHttpClient<IShoppingListService, ShoppingListService>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = defaultTimeout;
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();

        return services;
    }
}

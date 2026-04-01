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

        // SyncService with extended timeout
        services.AddHttpClient<SyncService>(client =>
      {
          client.BaseAddress = new Uri(baseUrl);
          client.Timeout = TimeSpan.FromSeconds(appSettings.Http.DefaultTimeoutSeconds * 3);
      })
      .AddHttpMessageHandler<ApiKeyHandler>()
      .AddServiceDiscovery();

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

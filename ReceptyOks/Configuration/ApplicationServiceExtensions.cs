namespace ReceptyOks.Configuration;

using CommunityToolkit.Maui.ApplicationModel;
using CommunityToolkit.Maui.Media;
using Microsoft.Extensions.Logging;
using Plugin.Maui.OCR;
using ReceptyOks.BlazorComponents.Services;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.Configuration;
using ReceptyOks.Shared.OCR;

/// <summary>
/// Extension methods for registering application services with the dependency injection container.
/// </summary>
internal static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers core application services (database, OCR, speech, etc.).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Database
        services.AddSingleton<LocalDatabase>();

        // Blazor component services
        services.AddSingleton<InstructionsEditorState>();
        services.AddSingleton<HtmlViewerState>();

        // OCR
        services.AddSingleton(OcrPlugin.Default);
        services.AddSingleton<IOCRService, MobileOcerService>();

        // Update checking
        services.AddSingleton<UpdateCheckerService>();

        // Platform services
        services.AddSingleton<IBadge>(Badge.Default);
        services.AddSingleton<ISpeechToText>(SpeechToText.Default);

        // Background jobs
        services.AddSingleton(new CleanupOptions
        {
            Interval = TimeSpan.FromHours(12),
            StartupDelay = TimeSpan.FromSeconds(30),
            MaxAge = TimeSpan.FromDays(7)
        });
        services.AddHostedService<LogCleanupService>();

        return services;
    }

    /// <summary>
    /// Registers Blazor WebView services and developer tools in debug mode.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static MauiAppBuilder AddBlazorServices(this MauiAppBuilder builder)
    {
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
   builder.Services.AddBlazorWebViewDeveloperTools();
      builder.Logging.AddFilter("Microsoft.AspNetCore.Components.WebView", LogLevel.Trace);
#endif

        return builder;
    }
}

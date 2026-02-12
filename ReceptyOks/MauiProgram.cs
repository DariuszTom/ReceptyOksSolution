using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using ReceptyOks.Configuration;
using System.Reflection;
using UraniumUI;

namespace ReceptyOks;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Load Configuration from embedded appsettings.json
        var appSettings = LoadConfiguration(builder);

        // Configure logging
        builder.ConfigureSerilog(appSettings);

        // Configure MAUI app and UI frameworks
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit(options =>
            {
                options.SetShouldEnableSnackbarOnWindows(true);
            })
            .UseUraniumUI()
            .UseUraniumUIMaterial()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialSymbolsOutlined.ttf", "MaterialSymbolsOutlined");
                fonts.AddFont("MaterialSymbolsOutlinedFilled.ttf", "MaterialSymbolsOutlinedFilled");
                fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbolsRounded");
                fonts.AddFont("MaterialSymbolsRoundedFilled.ttf", "MaterialSymbolsRoundedFilled");
                fonts.AddFont("MaterialSymbolsSharp.ttf", "MaterialSymbolsSharp");
                fonts.AddFont("MaterialSymbolsSharpFilled.ttf", "MaterialSymbolsSharpFilled");
                fonts.AddMaterialSymbolsFonts();
            });

        // Add Blazor WebView services
        builder.AddBlazorServices();

        // Add Aspire Service Discovery
        builder.Services.AddServiceDiscovery();

        // Register application services
        builder.Services
                 .AddApplicationServices()
                 .AddHttpClients(appSettings)
                 .AddViewModels()
                 .AddViews();

        return builder.Build();
    }

    private static AppSettings LoadConfiguration(MauiAppBuilder builder)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ReceptyOks.appsettings.json") ?? throw new InvalidOperationException(
                "appsettings.json not found. Make sure it's marked as EmbeddedResource in .csproj");
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        builder.Configuration.AddConfiguration(config);

        // Bind to strongly-typed settings
        var appSettings = new AppSettings();
        config.Bind(appSettings);
        builder.Services.AddSingleton(appSettings);

        return appSettings;
    }
}

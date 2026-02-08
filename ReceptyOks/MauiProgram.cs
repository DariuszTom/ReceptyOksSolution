using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Plugin.Maui.OCR;
using ReceptyOks.Configuration;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.OCR;
using ReceptyOks.ViewModels;
using ReceptyOks.Views;
using Serilog;
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
		
		// Configure Serilog
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.Enrich.FromLogContext()
			.WriteTo.Sink(new SQLiteSink(appSettings.Database.LocalDatabasePath))
#if DEBUG
			.WriteTo.Debug()
#endif
			.CreateLogger();

		builder.Logging.AddSerilog(dispose: true);

		// Register Serilog logger instance for DI consumers that request Serilog.ILogger
		builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);

		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
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
		builder.Services.AddMauiBlazorWebView();
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		// Add Aspire Service Discovery
		builder.Services.AddServiceDiscovery();

		// Database
		builder.Services.AddSingleton<LocalDatabase>();
		builder.Services.AddTransient<ApiKeyHandler>();
		// Configure HttpClient with Aspire service discovery
		builder.Services.AddHttpClient<SyncService>(client =>
		{
			client.BaseAddress = new Uri($"{appSettings.Http.GetEffectiveApiUrl()}");
			client.Timeout = TimeSpan.FromSeconds(appSettings.Http.DefaultTimeoutSeconds);
		})
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();

		// Services
		builder.Services.AddSingleton(OcrPlugin.Default);
		builder.Services.AddSingleton<IOCRService, MobileOcerService>();
		builder.Services.AddSingleton<UpdateCheckerService>();
        // Configure HttpClient for BackendAuthService so PostAsync can use relative URIs
        builder.Services.AddHttpClient<BackendAuthService>(client =>
        {
            client.BaseAddress = new Uri($"{appSettings.Http.GetEffectiveApiUrl()}");
            client.Timeout = TimeSpan.FromSeconds(appSettings.Http.DefaultTimeoutSeconds);
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();
        // Configure HttpClient for TokenProviderService so it can request tokens from backend
        builder.Services.AddHttpClient<TokenProviderService>(client =>
        {
            client.BaseAddress = new Uri($"{appSettings.Http.GetEffectiveApiUrl()}");
            client.Timeout = TimeSpan.FromSeconds(appSettings.Http.DefaultTimeoutSeconds);
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddServiceDiscovery();
        // ViewModels
        builder.Services.AddTransient<RecipesViewModel>();
		builder.Services.AddTransient<RecipeDetailViewModel>();
		builder.Services.AddTransient<RecipeEditViewModel>();
		builder.Services.AddTransient<CategoriesViewModel>();
		builder.Services.AddTransient<CategoryEditViewModel>();
		builder.Services.AddTransient<LogsViewModel>();
		builder.Services.AddTransient<RandomRecipeViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<ChatBotViewModel>();



        // Views
        builder.Services.AddTransient<RecipesPage>();
		builder.Services.AddTransient<RecipeDetailPage>();
		builder.Services.AddTransient<RecipeEditPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<CategoryEditPage>();
		builder.Services.AddTransient<LogsPage>();
		builder.Services.AddTransient<RandomRecipePage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<ChatBotPage>();

        return builder.Build();
	}

	private static AppSettings LoadConfiguration(MauiAppBuilder builder)
	{
		var assembly = Assembly.GetExecutingAssembly();
		using var stream = assembly.GetManifestResourceStream("ReceptyOks.appsettings.json");
		
		if (stream == null)
		{
			throw new InvalidOperationException(
				"appsettings.json not found. Make sure it's marked as EmbeddedResource in .csproj");
		}
		
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

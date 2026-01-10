using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
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

		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
            .UseUraniumUI()
            .UseUraniumUIMaterial()
            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddMaterialSymbolsFonts();
			})
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				handlers.AddHandler<Shell, ReceptyOks.Platforms.Android.Handlers.CustomShellRenderer>();
#endif
			});

		// Add Aspire Service Discovery
		builder.Services.AddServiceDiscovery();

		// Database
		builder.Services.AddSingleton<LocalDatabase>();
		
		// Configure HttpClient with Aspire service discovery
		builder.Services.AddHttpClient<SyncService>(client =>
		{
			client.BaseAddress = new Uri($"http://{appSettings.Http.ApiServiceName}");
			client.Timeout = TimeSpan.FromSeconds(appSettings.Http.DefaultTimeoutSeconds);
		})
		.AddServiceDiscovery();

		// Services
		builder.Services.AddSingleton(OcrPlugin.Default);
		builder.Services.AddSingleton<IOCRService, MobileOcerService>();
		builder.Services.AddSingleton<UpdateCheckerService>();
        // ViewModels
        builder.Services.AddTransient<RecipesViewModel>();
		builder.Services.AddTransient<RecipeDetailViewModel>();
		builder.Services.AddTransient<RecipeEditViewModel>();
		builder.Services.AddTransient<CategoriesViewModel>();
		builder.Services.AddTransient<CategoryEditViewModel>();
		builder.Services.AddTransient<LogsViewModel>();
		builder.Services.AddTransient<RandomRecipeViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views
        builder.Services.AddTransient<RecipesPage>();
		builder.Services.AddTransient<RecipeDetailPage>();
		builder.Services.AddTransient<RecipeEditPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<CategoryEditPage>();
		builder.Services.AddTransient<LogsPage>();
		builder.Services.AddTransient<RandomRecipePage>();
		builder.Services.AddSingleton<AppSettings>(appSettings);

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

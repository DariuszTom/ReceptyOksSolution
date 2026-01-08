using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using Plugin.Maui.OCR;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.OCR;
using ReceptyOks.ViewModels;
using ReceptyOks.Views;
using UraniumUI;
using Serilog;

namespace ReceptyOks;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		
		// Configure Serilog
		var dbPath = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "recipes_local.db");
		
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.Enrich.FromLogContext()
			.WriteTo.Sink(new SQLiteSink(dbPath))
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
			});

		// Add Aspire Service Discovery
		builder.Services.AddServiceDiscovery();

		// Database
		builder.Services.AddSingleton<LocalDatabase>();
		
		// Configure HttpClient with Aspire service discovery
		builder.Services.AddHttpClient<SyncService>(client =>
		{
			// Nazwa usługi z AppHost - Aspire automatycznie rozwiąże URL
			client.BaseAddress = new Uri("http://receptyoks-api");
			client.Timeout = TimeSpan.FromSeconds(30);
		})
		.AddServiceDiscovery(); // Włącz service discovery dla tego HttpClient

		// Services
		builder.Services.AddSingleton(OcrPlugin.Default);
		builder.Services.AddSingleton<IOCRService, MobileOcerService>();


        // ViewModels
        builder.Services.AddTransient<RecipesViewModel>();
		builder.Services.AddTransient<RecipeDetailViewModel>();
		builder.Services.AddTransient<RecipeEditViewModel>();
		builder.Services.AddTransient<CategoriesViewModel>();
		builder.Services.AddTransient<CategoryEditViewModel>();
		builder.Services.AddTransient<LogsViewModel>();
		builder.Services.AddTransient<RandomRecipeViewModel>();
		
		// Views
		builder.Services.AddTransient<RecipesPage>();
		builder.Services.AddTransient<RecipeDetailPage>();
		builder.Services.AddTransient<RecipeEditPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<CategoryEditPage>();
		builder.Services.AddTransient<LogsPage>();
		builder.Services.AddTransient<RandomRecipePage>();

		return builder.Build();
	}
}

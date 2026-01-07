using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
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

		// Database
		builder.Services.AddSingleton<LocalDatabase>();
		
		// HTTP Client
		builder.Services.AddSingleton<HttpClient>();
		
		// Services
		builder.Services.AddSingleton<SyncService>();
		builder.Services.AddSingleton(OcrPlugin.Default);
		builder.Services.AddSingleton<IOCRService, MobileOcerService>();


        // ViewModels
        builder.Services.AddTransient<RecipesViewModel>();
		builder.Services.AddTransient<RecipeDetailViewModel>();
		builder.Services.AddTransient<RecipeEditViewModel>();
		builder.Services.AddTransient<CategoriesViewModel>();
		builder.Services.AddTransient<CategoryEditViewModel>();
		builder.Services.AddTransient<LogsViewModel>();
		
		// Views
		builder.Services.AddTransient<RecipesPage>();
		builder.Services.AddTransient<RecipeDetailPage>();
		builder.Services.AddTransient<RecipeEditPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<CategoryEditPage>();
		builder.Services.AddTransient<LogsPage>();

		return builder.Build();
	}
}

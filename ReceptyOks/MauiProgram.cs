using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Plugin.Maui.OCR;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.OCR;
using ReceptyOks.ViewModels;
using ReceptyOks.Views;
using UraniumUI;

namespace ReceptyOks;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
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
		
		// Views
		builder.Services.AddTransient<RecipesPage>();
		builder.Services.AddTransient<RecipeDetailPage>();
		builder.Services.AddTransient<RecipeEditPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<CategoryEditPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

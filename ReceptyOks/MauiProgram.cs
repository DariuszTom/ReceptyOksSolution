using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.ViewModels;
using ReceptyOks.Views;
using UraniumUI;
using CommunityToolkit.Maui;

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

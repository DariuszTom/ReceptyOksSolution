namespace ReceptyOks.Configuration;

using ReceptyOks.ViewModels;

/// <summary>
/// Extension methods for registering ViewModels with the dependency injection container.
/// </summary>
internal static class ViewModelServiceExtensions
{
    /// <summary>
    /// Registers all ViewModels as transient services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<RecipesViewModel>();
        services.AddTransient<RecipeDetailViewModel>();
        services.AddTransient<RecipeEditViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<CategoryEditViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<RandomRecipeViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChatBotViewModel>();
        services.AddTransient<MealPlanViewModel>();
        services.AddTransient<ShopingListViewModel>();
        services.AddTransient<UserDetailsViewModel>();
        services.AddTransient<AppStatusViewModel>();
        services.AddTransient<IngredientsCalculationViewModel>();

        return services;
    }
}

namespace ReceptyOks.Configuration;

using ReceptyOks.Views;

/// <summary>
/// Extension methods for registering Views/Pages with the dependency injection container.
/// </summary>
internal static class ViewServiceExtensions
{
    /// <summary>
    /// Registers all Views/Pages as transient services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddViews(this IServiceCollection services)
    {
        services.AddTransient<RecipesPage>();
        services.AddTransient<RecipeDetailPage>();
        services.AddTransient<RecipeEditPage>();
        services.AddTransient<CategoriesPage>();
        services.AddTransient<CategoryEditPage>();
        services.AddTransient<LogsPage>();
        services.AddTransient<RandomRecipePage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<ChatBotPage>();
        services.AddTransient<MealPlanPage>();
        services.AddTransient<ShopingListPage>();
        services.AddTransient<UserDetailsPage>();
        services.AddTransient<AppStatusView>();

        return services;
    }
}

using System.Runtime.CompilerServices;

namespace ReceptyOks.Data
{
    public interface ILocalDatabase
    {
        Task ApplyServerCategoryAsync(CategoryLocal category);
        Task ApplyServerIngredientAsync(IngredientLocal ingredient);
        Task ApplyServerMealPlanAsync(MealPlanLocal mealPlan);
        Task ApplyServerRecipeAsync(RecipeLocal recipe);
        Task<int> ClearAllLogsAsync();
        Task ClearDirtyFlagsAsync();
        Task<int> ClearOldLogsAsync(int keepLastDays = 7);
        Task<int> DeleteCategoryAsync(Guid id);
        Task DeleteConversationAsync(string id);
        Task<int> DeleteMealPlanAsync(Guid id);
        Task<int> DeleteRecipeAsync(Guid id);
        Task<List<MealPlanLocal>> GetAllMealPlansAsync();
        Task<List<CategoryLocal>> GetCategoriesAsync();
        Task<CategoryLocal?> GetCategoryAsync(Guid id);
        Task<ConversationLocal?> GetConversationAsync(string id);
        Task<List<ConversationLocal>> GetConversationsAsync();
        Task<List<CategoryLocal>> GetDirtyCategoriesAsync();
        Task<List<IngredientLocal>> GetDirtyIngredientsAsync();
        Task<List<MealPlanLocal>> GetDirtyMealPlansAsync();
        Task<List<RecipeLocal>> GetDirtyRecipesAsync();
        Task<List<IngredientLocal>> GetIngredientsAsync();
        Task<DateTime?> GetLastSyncTimeAsync();
        Task<List<LogEntry>> GetLogsAsync(int limit = 100);
        Task<List<LogEntry>> GetLogsByLevelAsync(string level, int limit = 100);
        Task<MealPlanLocal?> GetMealPlanAsync(Guid id);
        Task<List<MealPlanLocal>> GetMealPlansForDateAsync(DateTime date);
        Task<List<MealPlanLocal>> GetMealPlansForDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<(MealPlanLocal MealPlan, RecipeLocal? Recipe)>> GetMealPlansWithRecipesAsync(DateTime startDate, DateTime endDate);
        Task<RecipeLocal?> GetRecipeAsync(Guid id);
        Task<List<RecipeIngredientLocal>> GetRecipeIngredientsAsync(Guid recipeId);
        Task<List<RecipeLocal>> GetRecipesAsync();
        Task<List<RecipeSummary>> GetRecipeSummariesAsync(string? searchQuery = null);
        Task<List<RecipeLocal>> GetRecipesByCategoryAndIngriendentsAsync(Guid categoryId, IEnumerable<Guid>? ingredientsId);
        Task<List<RecipeLocal>> GetRecipesByCategoryAsync(Guid categoryId);
        Task<int> PurgeDeletedConversationsAsync();
        Task<int> SaveCategoryAsync(CategoryLocal category);
        Task SaveConversationAsync(ConversationLocal conversation);
        Task<int> SaveIngredientAsync(IngredientLocal ingredient);
        Task<int> SaveMealPlanAsync(MealPlanLocal mealPlan);
        Task<int> SaveRecipeAsync(RecipeLocal recipe);
        Task SaveRecipeIngredientsAsync(Guid recipeId, List<RecipeIngredientLocal> ingredients);
        Task<List<RecipeLocal>> SearchRecipesAsync(string query);
        Task SetLastSyncTimeAsync(DateTime syncTime);
    }
}
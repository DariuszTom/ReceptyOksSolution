using ReceptyOks.Shared.DTOs;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Api.Repositories;

/// <summary>
/// Repository interface for batch data access operations used in synchronization.
/// </summary>
public interface ISyncRepository
{
    /// <summary>
    /// Batch loads categories by their IDs.
    /// </summary>
    Task<Dictionary<Guid, Category>> GetCategoriesByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>
    /// Batch loads ingredients by their IDs.
    /// </summary>
    Task<Dictionary<Guid, Ingredient>> GetIngredientsByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>
    /// Batch loads recipes with their ingredients by recipe IDs.
    /// </summary>
    Task<Dictionary<Guid, Recipe>> GetRecipesWithIngredientsByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>
    /// Batch loads meal plans by their IDs.
    /// </summary>
    Task<Dictionary<Guid, MealPlan>> GetMealPlansByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>
    /// Gets valid category IDs from a list of referenced IDs.
    /// </summary>
    Task<HashSet<Guid>> GetValidCategoryIdsAsync(IEnumerable<Guid> referencedIds);

    /// <summary>
    /// Gets valid ingredient IDs from a list of referenced IDs.
    /// </summary>
    Task<HashSet<Guid>> GetValidIngredientIdsAsync(IEnumerable<Guid> referencedIds);

    /// <summary>
    /// Gets valid recipe IDs from a list of referenced IDs.
    /// </summary>
    Task<HashSet<Guid>> GetValidRecipeIdsAsync(IEnumerable<Guid> referencedIds);

    /// <summary>
    /// Gets categories modified after a specific timestamp.
    /// </summary>
    Task<List<CategorySyncDto>> GetCategoriesModifiedSinceAsync(DateTime since);

    /// <summary>
    /// Gets ingredients modified after a specific timestamp.
    /// </summary>
    Task<List<IngredientSyncDto>> GetIngredientsModifiedSinceAsync(DateTime since);

    /// <summary>
    /// Gets recipes modified after a specific timestamp.
    /// </summary>
    Task<List<RecipeSyncDto>> GetRecipesModifiedSinceAsync(DateTime since);

    /// <summary>
    /// Gets meal plans modified after a specific timestamp.
    /// </summary>
    Task<List<MealPlanSyncDto>> GetMealPlansModifiedSinceAsync(DateTime since);

    /// <summary>
    /// Gets all categories.
    /// </summary>
    Task<List<CategorySyncDto>> GetAllCategoriesAsync();

    /// <summary>
    /// Gets all ingredients.
    /// </summary>
    Task<List<IngredientSyncDto>> GetAllIngredientsAsync();

    /// <summary>
    /// Gets all recipes.
    /// </summary>
    Task<List<RecipeSyncDto>> GetAllRecipesAsync();

    /// <summary>
    /// Gets all meal plans.
    /// </summary>
    Task<List<MealPlanSyncDto>> GetAllMealPlansAsync();
}

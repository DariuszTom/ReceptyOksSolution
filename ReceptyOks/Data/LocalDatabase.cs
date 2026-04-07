using ReceptyOks.Configuration;
using SQLite;
using System.Runtime.CompilerServices;

namespace ReceptyOks.Data;

public class LocalDatabase : ILocalDatabase
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public LocalDatabase(AppSettings settings)
    {
        _dbPath = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, settings.Database.LocalDatabaseName);
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_database is not null)
            return _database;

        _database = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _database.CreateTableAsync<RecipeLocal>().ConfigureAwait(false);
        await _database.CreateTableAsync<CategoryLocal>().ConfigureAwait(false);
        await _database.CreateTableAsync<IngredientLocal>().ConfigureAwait(false);
        await _database.CreateTableAsync<RecipeIngredientLocal>().ConfigureAwait(false);
        await _database.CreateTableAsync<SyncInfo>().ConfigureAwait(false);
        await _database.CreateTableAsync<LogEntry>().ConfigureAwait(false);
        await _database.CreateTableAsync<ConversationLocal>().ConfigureAwait(false);
        await _database.CreateTableAsync<MealPlanLocal>().ConfigureAwait(false);

        return _database;
    }

    #region Recipes

    public async Task<List<RecipeLocal>> GetRecipesAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<RecipeLocal>()
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<RecipeLocal?> GetRecipeAsync(Guid id)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<RecipeLocal>()
            .FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
    }

    public async Task<List<RecipeLocal>> GetRecipesByCategoryAsync(Guid categoryId)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<RecipeLocal>()
            .Where(r => r.CategoryId == categoryId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync().ConfigureAwait(false);
    }
    public async Task<List<RecipeLocal>> GetRecipesByCategoryAndIngriendentsAsync(Guid categoryId, IEnumerable<Guid>? ingredientsId)
    {
        List<RecipeLocal> recipes;
        if (categoryId == Guid.Empty)
            recipes = await GetRecipesAsync().ConfigureAwait(false);
        else
            recipes = await GetRecipesByCategoryAsync(categoryId).ConfigureAwait(false);

        if (recipes is null || recipes.Count == 0 || ingredientsId is null)
            return recipes ?? new List<RecipeLocal>();

        var ingredientsList = ingredientsId?.ToList() ?? new List<Guid>();
        if (ingredientsList.Count == 0)
            return recipes;

        var db = await GetConnectionAsync().ConfigureAwait(false);
        var filteredRecipes = new List<RecipeLocal>();

        foreach (var recipe in recipes)
        {
            var recipeIngredients = await db.Table<RecipeIngredientLocal>()
                .Where(ri => ri.RecipeId == recipe.Id)
                .ToListAsync().ConfigureAwait(false);

            var recipeIngredientIds = recipeIngredients.Select(ri => ri.IngredientId).ToHashSet();

            // SprawdŸ czy przepis zawiera wszystkie wymagane sk³adniki
            if (ingredientsList.All(ingId => recipeIngredientIds.Contains(ingId)))
            {
                filteredRecipes.Add(recipe);
            }
        }

        return filteredRecipes;
    }
    public async Task<int> SaveRecipeAsync(RecipeLocal recipe)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        recipe.UpdatedAt = DateTime.UtcNow;
        recipe.IsDirty = true;

        var existing = await db.Table<RecipeLocal>()
            .FirstOrDefaultAsync(r => r.Id == recipe.Id).ConfigureAwait(false);

        if (existing is null)
        {
            recipe.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(recipe).ConfigureAwait(false);
        }
        else
        {
            return await db.UpdateAsync(recipe).ConfigureAwait(false);
        }
    }

    public async Task<int> DeleteRecipeAsync(Guid id)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var recipe = await GetRecipeAsync(id).ConfigureAwait(false);
        if (recipe is null) return 0;

        recipe.IsDeleted = true;
        recipe.UpdatedAt = DateTime.UtcNow;
        recipe.IsDirty = true;
        return await db.UpdateAsync(recipe).ConfigureAwait(false);
    }

    public async Task<List<RecipeLocal>> SearchRecipesAsync(string query)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var lowerQuery = query.ToLower();
        return await db.Table<RecipeLocal>()
            .Where(r => !r.IsDeleted &&
                (r.Title.ToLower().Contains(lowerQuery) || r.Description.ToLower().Contains(lowerQuery)))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async IAsyncEnumerable<RecipeLocal> GetRecipesAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var recipes = await db.Table<RecipeLocal>()
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync().ConfigureAwait(false);

        foreach (var recipe in recipes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return recipe;
        }
    }

    #endregion

    #region Categories

    public async Task<List<CategoryLocal>> GetCategoriesAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<CategoryLocal>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<CategoryLocal?> GetCategoryAsync(Guid id)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<CategoryLocal>()
            .FirstOrDefaultAsync(c => c.Id == id).ConfigureAwait(false);
    }

    public async Task<int> SaveCategoryAsync(CategoryLocal category)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        category.UpdatedAt = DateTime.UtcNow;
        category.IsDirty = true;

        var existing = await db.Table<CategoryLocal>()
            .FirstOrDefaultAsync(c => c.Id == category.Id).ConfigureAwait(false);

        if (existing is null)
        {
            category.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(category).ConfigureAwait(false);
        }
        else
        {
            return await db.UpdateAsync(category).ConfigureAwait(false);
        }
    }

    public async Task<int> DeleteCategoryAsync(Guid id)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var category = await GetCategoryAsync(id).ConfigureAwait(false);
        if (category is null) return 0;

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        category.IsDirty = true;
        return await db.UpdateAsync(category).ConfigureAwait(false);
    }

    #endregion

    #region Ingredients

    public async Task<List<IngredientLocal>> GetIngredientsAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<IngredientLocal>()
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.Name)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> SaveIngredientAsync(IngredientLocal ingredient)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        ingredient.UpdatedAt = DateTime.UtcNow;
        ingredient.IsDirty = true;

        var existing = await db.Table<IngredientLocal>()
            .FirstOrDefaultAsync(i => i.Id == ingredient.Id).ConfigureAwait(false);

        if (existing is null)
        {
            ingredient.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(ingredient).ConfigureAwait(false);
        }
        else
        {
            return await db.UpdateAsync(ingredient).ConfigureAwait(false);
        }
    }

    #endregion

    #region Recipe Ingredients

    public async Task<List<RecipeIngredientLocal>> GetRecipeIngredientsAsync(Guid recipeId)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<RecipeIngredientLocal>()
            .Where(ri => ri.RecipeId == recipeId)
            .OrderBy(ri => ri.Order)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task SaveRecipeIngredientsAsync(Guid recipeId, List<RecipeIngredientLocal> ingredients)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);

        // Usuñ stare
        var existing = await db.Table<RecipeIngredientLocal>()
            .Where(ri => ri.RecipeId == recipeId)
            .ToListAsync().ConfigureAwait(false);
        foreach (var item in existing)
        {
            await db.DeleteAsync(item).ConfigureAwait(false);
        }

        // Dodaj nowe
        foreach (var ingredient in ingredients)
        {
            ingredient.RecipeId = recipeId;
            await db.InsertAsync(ingredient).ConfigureAwait(false);
        }
    }

    #endregion

    #region Sync

    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        var db = await GetConnectionAsync();
        var info = await db.Table<SyncInfo>().FirstOrDefaultAsync();
        return info?.LastSyncedAt;
    }

    public async Task SetLastSyncTimeAsync(DateTime syncTime)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var info = await db.Table<SyncInfo>().FirstOrDefaultAsync().ConfigureAwait(false);

        if (info is null)
        {
            await db.InsertAsync(new SyncInfo { Id = 1, LastSyncedAt = syncTime }).ConfigureAwait(false);
        }
        else
        {
            info.LastSyncedAt = syncTime;
            await db.UpdateAsync(info).ConfigureAwait(false);
        }
    }

    public async Task<List<RecipeLocal>> GetDirtyRecipesAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<RecipeLocal>()
            .Where(r => r.IsDirty)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<CategoryLocal>> GetDirtyCategoriesAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<CategoryLocal>()
            .Where(c => c.IsDirty)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<IngredientLocal>> GetDirtyIngredientsAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<IngredientLocal>()
            .Where(i => i.IsDirty)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task ClearDirtyFlagsAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        await db.ExecuteAsync("UPDATE Recipes SET IsDirty = 0").ConfigureAwait(false);
        await db.ExecuteAsync("UPDATE Categories SET IsDirty = 0").ConfigureAwait(false);
        await db.ExecuteAsync("UPDATE Ingredients SET IsDirty = 0").ConfigureAwait(false);
        await db.ExecuteAsync("UPDATE MealPlans SET IsDirty = 0").ConfigureAwait(false);
    }

    public async Task ApplyServerRecipeAsync(RecipeLocal recipe)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var existing = await db.Table<RecipeLocal>()
            .FirstOrDefaultAsync(r => r.Id == recipe.Id).ConfigureAwait(false);

        recipe.IsDirty = false;

        if (existing is null)
        {
            await db.InsertAsync(recipe).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(recipe).ConfigureAwait(false);
        }
    }

    public async Task ApplyServerCategoryAsync(CategoryLocal category)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var existing = await db.Table<CategoryLocal>()
            .FirstOrDefaultAsync(c => c.Id == category.Id).ConfigureAwait(false);

        category.IsDirty = false;

        if (existing is null)
        {
            await db.InsertAsync(category).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(category).ConfigureAwait(false);
        }
    }

    public async Task ApplyServerIngredientAsync(IngredientLocal ingredient)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var existing = await db.Table<IngredientLocal>()
            .FirstOrDefaultAsync(i => i.Id == ingredient.Id).ConfigureAwait(false);

        ingredient.IsDirty = false;

        if (existing is null)
        {
            await db.InsertAsync(ingredient).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(ingredient).ConfigureAwait(false);
        }
    }

    #endregion

    #region Logs

    public async Task<List<LogEntry>> GetLogsAsync(int limit = 100)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<LogEntry>()
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<LogEntry>> GetLogsByLevelAsync(string level, int limit = 100)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<LogEntry>()
            .Where(l => l.Level == level)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> ClearOldLogsAsync(int keepLastDays = 7)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var cutoffDate = DateTime.UtcNow.AddDays(-keepLastDays);
        return await db.ExecuteAsync("DELETE FROM Logs WHERE Timestamp < ?", cutoffDate).ConfigureAwait(false);
    }

    public async Task<int> ClearAllLogsAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.ExecuteAsync("DELETE FROM Logs").ConfigureAwait(false);
    }

    #endregion

    #region Conversations

    /// <summary>
    /// Saves or updates a conversation in the local database.
    /// </summary>
    public async Task SaveConversationAsync(ConversationLocal conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var db = await GetConnectionAsync().ConfigureAwait(false);
        var existing = await db.Table<ConversationLocal>()
            .FirstOrDefaultAsync(c => c.Id == conversation.Id).ConfigureAwait(false);

        conversation.UpdatedAt = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            conversation.CreatedAt = conversation.UpdatedAt;
            await db.InsertAsync(conversation).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(conversation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retrieves a conversation by its ID.
    /// </summary>
    public async Task<ConversationLocal?> GetConversationAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ConversationLocal>()
              .Where(c => c.Id == id && !c.IsDeleted)
              .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves all conversations ordered by most recent first.
    /// </summary>
    public async Task<List<ConversationLocal>> GetConversationsAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ConversationLocal>()
        .Where(c => !c.IsDeleted)
        .OrderByDescending(c => c.UpdatedAt)
        .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Marks a conversation as deleted (soft delete).
    /// </summary>
    public async Task DeleteConversationAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var db = await GetConnectionAsync().ConfigureAwait(false);
        var conversation = await GetConversationAsync(id).ConfigureAwait(false);

        if (conversation is not null)
        {
            conversation.IsDeleted = true;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await db.UpdateAsync(conversation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Permanently deletes all conversations marked as deleted.
    /// </summary>
    public async Task<int> PurgeDeletedConversationsAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.ExecuteAsync("DELETE FROM Conversations WHERE IsDeleted = 1").ConfigureAwait(false);
    }

    #endregion

    #region MealPlans

    /// <summary>
    /// Pobiera plany posi³ków dla zakresu dat.
    /// </summary>
    public async Task<List<MealPlanLocal>> GetMealPlansForDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1);

        return await db.Table<MealPlanLocal>()
                 .Where(mp => !mp.IsDeleted && mp.Date >= start && mp.Date < end)
              .OrderBy(mp => mp.Date)
         .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pobiera plany posi³ków dla konkretnego dnia.
    /// </summary>
    public async Task<List<MealPlanLocal>> GetMealPlansForDateAsync(DateTime date)
    {
        return await GetMealPlansForDateRangeAsync(date, date).ConfigureAwait(false);
    }

    /// <summary>
    /// Pobiera pojedynczy plan posi³ku po ID.
    /// </summary>
    public async Task<MealPlanLocal?> GetMealPlanAsync(Guid id)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<MealPlanLocal>().FirstOrDefaultAsync(mp => mp.Id == id && !mp.IsDeleted).ConfigureAwait(false);
    }

    /// <summary>
    /// Zapisuje lub aktualizuje plan posi³ku.
    /// </summary>
    public async Task<int> SaveMealPlanAsync(MealPlanLocal mealPlan)
    {
        ArgumentNullException.ThrowIfNull(mealPlan);

        var db = await GetConnectionAsync().ConfigureAwait(false);
        mealPlan.UpdatedAt = DateTime.UtcNow;
        mealPlan.IsDirty = true;

        var existing = await db.Table<MealPlanLocal>()
            .FirstOrDefaultAsync(mp => mp.Id == mealPlan.Id).ConfigureAwait(false);

        if (existing is null)
        {
            mealPlan.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(mealPlan).ConfigureAwait(false);
        }
        else
        {
            return await db.UpdateAsync(mealPlan).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Usuwa plan posi³ku (soft delete).
    /// </summary>
    public async Task<int> DeleteMealPlanAsync(Guid id)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var mealPlan = await GetMealPlanAsync(id).ConfigureAwait(false);
        if (mealPlan is null) return 0;

        mealPlan.IsDeleted = true;
        mealPlan.UpdatedAt = DateTime.UtcNow;
        mealPlan.IsDirty = true;
        return await db.UpdateAsync(mealPlan).ConfigureAwait(false);
    }

    /// <summary>
    /// Pobiera plany posi³ków z pe³nymi danymi przepisów dla zakresu dat.
    /// Batch-loads recipes to avoid N+1 queries.
    /// </summary>
    public async Task<List<(MealPlanLocal MealPlan, RecipeLocal? Recipe)>> GetMealPlansWithRecipesAsync(DateTime startDate, DateTime endDate)
    {
        var mealPlans = await GetMealPlansForDateRangeAsync(startDate, endDate).ConfigureAwait(false);
        if (mealPlans.Count == 0)
            return [];

        var db = await GetConnectionAsync().ConfigureAwait(false);
        var recipes = await db.Table<RecipeLocal>()
            .Where(r => !r.IsDeleted)
            .ToListAsync().ConfigureAwait(false);
        var recipeLookup = recipes.ToDictionary(r => r.Id);

        var result = new List<(MealPlanLocal, RecipeLocal?)>(mealPlans.Count);
        foreach (var mp in mealPlans)
        {
            recipeLookup.TryGetValue(mp.RecipeId, out var recipe);
            result.Add((mp, recipe));
        }

        return result;
    }

    /// <summary>
    /// Pobiera plany posi³ków oznaczone jako wymagaj¹ce synchronizacji.
    /// </summary>
    public async Task<List<MealPlanLocal>> GetDirtyMealPlansAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<MealPlanLocal>()
            .Where(mp => mp.IsDirty)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pobiera wszystkie plany posi³ków (do pe³nego uploadu).
    /// </summary>
    public async Task<List<MealPlanLocal>> GetAllMealPlansAsync()
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<MealPlanLocal>()
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stosuje plan posi³ku z serwera (upsert bez ustawiania IsDirty).
    /// </summary>
    public async Task ApplyServerMealPlanAsync(MealPlanLocal mealPlan)
    {
        var db = await GetConnectionAsync().ConfigureAwait(false);
        var existing = await db.Table<MealPlanLocal>()
            .FirstOrDefaultAsync(mp => mp.Id == mealPlan.Id).ConfigureAwait(false);

        mealPlan.IsDirty = false;

        if (existing is null)
        {
            await db.InsertAsync(mealPlan).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(mealPlan).ConfigureAwait(false);
        }
    }

    #endregion
}

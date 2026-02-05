using ReceptyOks.Configuration;
using SQLite;

namespace ReceptyOks.Data;

public class LocalDatabase
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

        await _database.CreateTableAsync<RecipeLocal>();
        await _database.CreateTableAsync<CategoryLocal>();
        await _database.CreateTableAsync<IngredientLocal>();
        await _database.CreateTableAsync<RecipeIngredientLocal>();
        await _database.CreateTableAsync<SyncInfo>();
        await _database.CreateTableAsync<LogEntry>();
        await _database.CreateTableAsync<ConversationLocal>();
        await _database.CreateTableAsync<MealPlanLocal>();

        return _database;
    }

    #region Recipes

    public async Task<List<RecipeLocal>> GetRecipesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<RecipeLocal>()
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<RecipeLocal?> GetRecipeAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<RecipeLocal>()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<RecipeLocal>> GetRecipesByCategoryAsync(Guid categoryId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<RecipeLocal>()
            .Where(r => r.CategoryId == categoryId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
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

        var db = await GetConnectionAsync();
        var filteredRecipes = new List<RecipeLocal>();

        foreach (var recipe in recipes)
        {
            var recipeIngredients = await db.Table<RecipeIngredientLocal>()
                .Where(ri => ri.RecipeId == recipe.Id)
                .ToListAsync();

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
        var db = await GetConnectionAsync();
        recipe.UpdatedAt = DateTime.UtcNow;
        recipe.IsDirty = true;

        var existing = await db.Table<RecipeLocal>()
            .FirstOrDefaultAsync(r => r.Id == recipe.Id);

        if (existing is null)
        {
            recipe.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(recipe);
        }
        else
        {
            return await db.UpdateAsync(recipe);
        }
    }

    public async Task<int> DeleteRecipeAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var recipe = await GetRecipeAsync(id);
        if (recipe is null) return 0;

        recipe.IsDeleted = true;
        recipe.UpdatedAt = DateTime.UtcNow;
        recipe.IsDirty = true;
        return await db.UpdateAsync(recipe);
    }

    public async Task<List<RecipeLocal>> SearchRecipesAsync(string query)
    {
        var db = await GetConnectionAsync();
        var lowerQuery = query.ToLower();
        return await db.Table<RecipeLocal>()
            .Where(r => !r.IsDeleted &&
                (r.Title.ToLower().Contains(lowerQuery) || r.Description.ToLower().Contains(lowerQuery)))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    #endregion

    #region Categories

    public async Task<List<CategoryLocal>> GetCategoriesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<CategoryLocal>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<CategoryLocal?> GetCategoryAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<CategoryLocal>()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<int> SaveCategoryAsync(CategoryLocal category)
    {
        var db = await GetConnectionAsync();
        category.UpdatedAt = DateTime.UtcNow;
        category.IsDirty = true;

        var existing = await db.Table<CategoryLocal>()
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        if (existing is null)
        {
            category.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(category);
        }
        else
        {
            return await db.UpdateAsync(category);
        }
    }

    public async Task<int> DeleteCategoryAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var category = await GetCategoryAsync(id);
        if (category is null) return 0;

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        category.IsDirty = true;
        return await db.UpdateAsync(category);
    }

    #endregion

    #region Ingredients

    public async Task<List<IngredientLocal>> GetIngredientsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<IngredientLocal>()
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<int> SaveIngredientAsync(IngredientLocal ingredient)
    {
        var db = await GetConnectionAsync();
        ingredient.UpdatedAt = DateTime.UtcNow;
        ingredient.IsDirty = true;

        var existing = await db.Table<IngredientLocal>()
            .FirstOrDefaultAsync(i => i.Id == ingredient.Id);

        if (existing is null)
        {
            ingredient.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(ingredient);
        }
        else
        {
            return await db.UpdateAsync(ingredient);
        }
    }

    #endregion

    #region Recipe Ingredients

    public async Task<List<RecipeIngredientLocal>> GetRecipeIngredientsAsync(Guid recipeId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<RecipeIngredientLocal>()
            .Where(ri => ri.RecipeId == recipeId)
            .OrderBy(ri => ri.Order)
            .ToListAsync();
    }

    public async Task SaveRecipeIngredientsAsync(Guid recipeId, List<RecipeIngredientLocal> ingredients)
    {
        var db = await GetConnectionAsync();

        // Usuñ stare
        var existing = await db.Table<RecipeIngredientLocal>()
            .Where(ri => ri.RecipeId == recipeId)
            .ToListAsync();
        foreach (var item in existing)
        {
            await db.DeleteAsync(item);
        }

        // Dodaj nowe
        foreach (var ingredient in ingredients)
        {
            ingredient.RecipeId = recipeId;
            await db.InsertAsync(ingredient);
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
        var db = await GetConnectionAsync();
        var info = await db.Table<SyncInfo>().FirstOrDefaultAsync();

        if (info is null)
        {
            await db.InsertAsync(new SyncInfo { Id = 1, LastSyncedAt = syncTime });
        }
        else
        {
            info.LastSyncedAt = syncTime;
            await db.UpdateAsync(info);
        }
    }

    public async Task<List<RecipeLocal>> GetDirtyRecipesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<RecipeLocal>()
            .Where(r => r.IsDirty)
            .ToListAsync();
    }

    public async Task<List<CategoryLocal>> GetDirtyCategoriesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<CategoryLocal>()
            .Where(c => c.IsDirty)
            .ToListAsync();
    }

    public async Task<List<IngredientLocal>> GetDirtyIngredientsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<IngredientLocal>()
            .Where(i => i.IsDirty)
            .ToListAsync();
    }

    public async Task ClearDirtyFlagsAsync()
    {
        var db = await GetConnectionAsync();
        await db.ExecuteAsync("UPDATE Recipes SET IsDirty = 0");
        await db.ExecuteAsync("UPDATE Categories SET IsDirty = 0");
        await db.ExecuteAsync("UPDATE Ingredients SET IsDirty = 0");
    }

    public async Task ApplyServerRecipeAsync(RecipeLocal recipe)
    {
        var db = await GetConnectionAsync();
        var existing = await db.Table<RecipeLocal>()
            .FirstOrDefaultAsync(r => r.Id == recipe.Id);

        recipe.IsDirty = false;

        if (existing is null)
        {
            await db.InsertAsync(recipe);
        }
        else
        {
            await db.UpdateAsync(recipe);
        }
    }

    public async Task ApplyServerCategoryAsync(CategoryLocal category)
    {
        var db = await GetConnectionAsync();
        var existing = await db.Table<CategoryLocal>()
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        category.IsDirty = false;

        if (existing is null)
        {
            await db.InsertAsync(category);
        }
        else
        {
            await db.UpdateAsync(category);
        }
    }

    public async Task ApplyServerIngredientAsync(IngredientLocal ingredient)
    {
        var db = await GetConnectionAsync();
        var existing = await db.Table<IngredientLocal>()
            .FirstOrDefaultAsync(i => i.Id == ingredient.Id);

        ingredient.IsDirty = false;

        if (existing is null)
        {
            await db.InsertAsync(ingredient);
        }
        else
        {
            await db.UpdateAsync(ingredient);
        }
    }

    #endregion

    #region Logs

    public async Task<List<LogEntry>> GetLogsAsync(int limit = 100)
    {
        var db = await GetConnectionAsync();
        return await db.Table<LogEntry>()
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<LogEntry>> GetLogsByLevelAsync(string level, int limit = 100)
    {
        var db = await GetConnectionAsync();
        return await db.Table<LogEntry>()
            .Where(l => l.Level == level)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> ClearOldLogsAsync(int keepLastDays = 7)
    {
        var db = await GetConnectionAsync();
        var cutoffDate = DateTime.UtcNow.AddDays(-keepLastDays);
        return await db.ExecuteAsync("DELETE FROM Logs WHERE Timestamp < ?", cutoffDate);
    }

    public async Task<int> ClearAllLogsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM Logs");
    }

    #endregion

    #region Conversations

    /// <summary>
    /// Saves or updates a conversation in the local database.
    /// </summary>
    public async Task SaveConversationAsync(ConversationLocal conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var db = await GetConnectionAsync();
        var existing = await db.Table<ConversationLocal>()
            .FirstOrDefaultAsync(c => c.Id == conversation.Id);

        conversation.UpdatedAt = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            conversation.CreatedAt = conversation.UpdatedAt;
            await db.InsertAsync(conversation);
        }
        else
        {
            await db.UpdateAsync(conversation);
        }
    }

    /// <summary>
    /// Retrieves a conversation by its ID.
    /// </summary>
    public async Task<ConversationLocal?> GetConversationAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var db = await GetConnectionAsync();
        return await db.Table<ConversationLocal>()
              .Where(c => c.Id == id && !c.IsDeleted)
              .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Retrieves all conversations ordered by most recent first.
    /// </summary>
    public async Task<List<ConversationLocal>> GetConversationsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<ConversationLocal>()
        .Where(c => !c.IsDeleted)
        .OrderByDescending(c => c.UpdatedAt)
        .ToListAsync();
    }

    /// <summary>
    /// Marks a conversation as deleted (soft delete).
    /// </summary>
    public async Task DeleteConversationAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var db = await GetConnectionAsync();
        var conversation = await GetConversationAsync(id);

        if (conversation is not null)
        {
            conversation.IsDeleted = true;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await db.UpdateAsync(conversation);
        }
    }

    /// <summary>
    /// Permanently deletes all conversations marked as deleted.
    /// </summary>
    public async Task<int> PurgeDeletedConversationsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM Conversations WHERE IsDeleted = 1");
    }

    #endregion

    #region MealPlans

    /// <summary>
    /// Pobiera plany posi³ków dla zakresu dat.
    /// </summary>
    public async Task<List<MealPlanLocal>> GetMealPlansForDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetConnectionAsync();
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1);

        return await db.Table<MealPlanLocal>()
                 .Where(mp => !mp.IsDeleted && mp.Date >= start && mp.Date < end)
              .OrderBy(mp => mp.Date)
         .ToListAsync();
    }

    /// <summary>
    /// Pobiera plany posi³ków dla konkretnego dnia.
    /// </summary>
    public async Task<List<MealPlanLocal>> GetMealPlansForDateAsync(DateTime date)
    {
        return await GetMealPlansForDateRangeAsync(date, date);
    }

    /// <summary>
    /// Pobiera pojedynczy plan posi³ku po ID.
    /// </summary>
    public async Task<MealPlanLocal?> GetMealPlanAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<MealPlanLocal>().FirstOrDefaultAsync(mp => mp.Id == id && !mp.IsDeleted);
    }

    /// <summary>
    /// Zapisuje lub aktualizuje plan posi³ku.
    /// </summary>
    public async Task<int> SaveMealPlanAsync(MealPlanLocal mealPlan)
    {
        ArgumentNullException.ThrowIfNull(mealPlan);

        var db = await GetConnectionAsync();
        mealPlan.UpdatedAt = DateTime.UtcNow;
        mealPlan.IsDirty = true;

        var existing = await db.Table<MealPlanLocal>()
            .FirstOrDefaultAsync(mp => mp.Id == mealPlan.Id);

        if (existing is null)
        {
            mealPlan.CreatedAt = DateTime.UtcNow;
            return await db.InsertAsync(mealPlan);
        }
        else
        {
            return await db.UpdateAsync(mealPlan);
        }
    }

    /// <summary>
    /// Usuwa plan posi³ku (soft delete).
    /// </summary>
    public async Task<int> DeleteMealPlanAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var mealPlan = await GetMealPlanAsync(id);
        if (mealPlan is null) return 0;

        mealPlan.IsDeleted = true;
        mealPlan.UpdatedAt = DateTime.UtcNow;
        mealPlan.IsDirty = true;
        return await db.UpdateAsync(mealPlan);
    }

    /// <summary>
    /// Pobiera plany posi³ków z pe³nymi danymi przepisów dla zakresu dat.
    /// </summary>
    public async Task<List<(MealPlanLocal MealPlan, RecipeLocal? Recipe)>> GetMealPlansWithRecipesAsync(DateTime startDate, DateTime endDate)
    {
        var mealPlans = await GetMealPlansForDateRangeAsync(startDate, endDate);
        var result = new List<(MealPlanLocal, RecipeLocal?)>();

        foreach (var mp in mealPlans)
        {
            var recipe = await GetRecipeAsync(mp.RecipeId);
            result.Add((mp, recipe));
        }

        return result;
    }

    #endregion
}

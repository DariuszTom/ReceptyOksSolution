using SQLite;

namespace ReceptyOks.Data;

public class LocalDatabase
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public LocalDatabase()
    {
        _dbPath = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "recipes_local.db");
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
}

using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared.DTOs;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Api.Repositories;

/// <summary>
/// Repository implementation for batch data access operations used in synchronization.
/// Minimizes Azure SQL round trips by using batch queries.
/// </summary>
public class SyncRepository : ISyncRepository
{
    private readonly RecipeDbContext _db;

    public SyncRepository(RecipeDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<Guid, Category>> GetCategoriesByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, Category>();

        return await _db.Categories
            .Where(c => idList.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, Ingredient>> GetIngredientsByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, Ingredient>();

        return await _db.Ingredients
            .Where(i => idList.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, Recipe>> GetRecipesWithIngredientsByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, Recipe>();

        return await _db.Recipes
            .Include(r => r.Ingredients)
            .Where(r => idList.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, MealPlan>> GetMealPlansByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, MealPlan>();

        return await _db.MealPlans
            .Where(mp => idList.Contains(mp.Id))
            .ToDictionaryAsync(mp => mp.Id)
            .ConfigureAwait(false);
    }

    public async Task<HashSet<Guid>> GetValidCategoryIdsAsync(IEnumerable<Guid> referencedIds)
    {
        var idList = referencedIds.ToList();
        if (idList.Count == 0)
            return new HashSet<Guid>();

        return await _db.Categories
            .Where(c => idList.Contains(c.Id))
            .Select(c => c.Id)
            .ToHashSetAsync()
            .ConfigureAwait(false);
    }

    public async Task<HashSet<Guid>> GetValidIngredientIdsAsync(IEnumerable<Guid> referencedIds)
    {
        var idList = referencedIds.ToList();
        if (idList.Count == 0)
            return new HashSet<Guid>();

        return await _db.Ingredients
            .Where(i => idList.Contains(i.Id))
            .Select(i => i.Id)
            .ToHashSetAsync()
            .ConfigureAwait(false);
    }

    public async Task<HashSet<Guid>> GetValidRecipeIdsAsync(IEnumerable<Guid> referencedIds)
    {
        var idList = referencedIds.ToList();
        if (idList.Count == 0)
            return new HashSet<Guid>();

        return await _db.Recipes
            .Where(r => idList.Contains(r.Id))
            .Select(r => r.Id)
            .ToHashSetAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<CategorySyncDto>> GetCategoriesModifiedSinceAsync(DateTime since)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.UpdatedAt > since)
            .Select(c => new CategorySyncDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IconName = c.IconName,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsDeleted = c.IsDeleted
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<IngredientSyncDto>> GetIngredientsModifiedSinceAsync(DateTime since)
    {
        return await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.UpdatedAt > since)
            .Select(i => new IngredientSyncDto
            {
                Id = i.Id,
                Name = i.Name,
                Unit = i.Unit,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                IsDeleted = i.IsDeleted
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<RecipeSyncDto>> GetRecipesModifiedSinceAsync(DateTime since)
    {
        return await _db.Recipes
            .AsNoTracking()
            .Where(r => r.UpdatedAt > since)
            .Select(r => new RecipeSyncDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Instructions = r.Instructions,
                PreparationTimeMinutes = r.PreparationTimeMinutes,
                CookingTimeMinutes = r.CookingTimeMinutes,
                Servings = r.Servings,
                Image = r.Image,
                ImageContentType = r.ImageContentType,
                CategoryId = r.CategoryId,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                IsDeleted = r.IsDeleted,
                Ingredients = r.Ingredients.Select(ri => new RecipeIngredientSyncDto
                {
                    Id = ri.Id,
                    IngredientId = ri.IngredientId,
                    Quantity = ri.Quantity,
                    Unit = ri.Unit,
                    Notes = ri.Notes,
                    Order = ri.Order
                }).ToList()
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<MealPlanSyncDto>> GetMealPlansModifiedSinceAsync(DateTime since)
    {
        return await _db.MealPlans
            .AsNoTracking()
            .Where(mp => mp.UpdatedAt > since)
            .Select(mp => new MealPlanSyncDto
            {
                Id = mp.Id,
                Date = mp.Date,
                StartHour = mp.StartHour,
                DurationMinutes = mp.DurationMinutes,
                RecipeId = mp.RecipeId,
                Notes = mp.Notes,
                CreatedAt = mp.CreatedAt,
                UpdatedAt = mp.UpdatedAt,
                IsDeleted = mp.IsDeleted
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<CategorySyncDto>> GetAllCategoriesAsync()
    {
        return await _db.Categories
            .AsNoTracking()
            .Select(c => new CategorySyncDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IconName = c.IconName,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsDeleted = c.IsDeleted
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<IngredientSyncDto>> GetAllIngredientsAsync()
    {
        return await _db.Ingredients
            .AsNoTracking()
            .Select(i => new IngredientSyncDto
            {
                Id = i.Id,
                Name = i.Name,
                Unit = i.Unit,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                IsDeleted = i.IsDeleted
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<RecipeSyncDto>> GetAllRecipesAsync()
    {
        return await _db.Recipes
            .AsNoTracking()
            .Select(r => new RecipeSyncDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Instructions = r.Instructions,
                PreparationTimeMinutes = r.PreparationTimeMinutes,
                CookingTimeMinutes = r.CookingTimeMinutes,
                Servings = r.Servings,
                Image = r.Image,
                ImageContentType = r.ImageContentType,
                CategoryId = r.CategoryId,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                IsDeleted = r.IsDeleted,
                Ingredients = r.Ingredients.Select(ri => new RecipeIngredientSyncDto
                {
                    Id = ri.Id,
                    IngredientId = ri.IngredientId,
                    Quantity = ri.Quantity,
                    Unit = ri.Unit,
                    Notes = ri.Notes,
                    Order = ri.Order
                }).ToList()
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<MealPlanSyncDto>> GetAllMealPlansAsync()
    {
        return await _db.MealPlans
            .AsNoTracking()
            .Select(mp => new MealPlanSyncDto
            {
                Id = mp.Id,
                Date = mp.Date,
                StartHour = mp.StartHour,
                DurationMinutes = mp.DurationMinutes,
                RecipeId = mp.RecipeId,
                Notes = mp.Notes,
                CreatedAt = mp.CreatedAt,
                UpdatedAt = mp.UpdatedAt,
                IsDeleted = mp.IsDeleted
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }
}

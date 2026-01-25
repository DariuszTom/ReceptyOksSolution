using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared.DTOs;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sync")
      .WithTags("Synchronization")
        .RequireRateLimiting("fixed");

        // POST - synchronizacja dwukierunkowa
        group.MapPost("/", async (SyncRequest request, RecipeDbContext db, ILogger<RecipeDbContext> logger) =>
  {
      var lastSync = request.LastSyncedAt ?? DateTime.MinValue;

      logger.LogInformation("Sync started. LastSyncedAt: {LastSync}, ChangedCategories: {CatCount}, ChangedIngredients: {IngCount}, ChangedRecipes: {RecCount}",
            lastSync, request.ChangedCategories.Count, request.ChangedIngredients.Count, request.ChangedRecipes.Count);

      // 1. Zastosuj zmiany z klienta
      await ApplyClientChanges(request, db, logger);

      // Capture syncTime after applying client changes so SyncedAt >= any UpdatedAt set above
      var syncTime = DateTime.UtcNow;

      // 2. Pobierz zmiany serwera od ostatniej synchronizacji
      var response = new SyncResponse
      {
          SyncedAt = syncTime,
          Categories = await GetServerCategories(lastSync, db),
          Ingredients = await GetServerIngredients(lastSync, db),
          Recipes = await GetServerRecipes(lastSync, db)
      };

      logger.LogInformation(
                   "Sync completed. SyncedAt: {SyncTime}, ReturnedCategories: {CatCount}, ReturnedIngredients: {IngCount}, ReturnedRecipes: {RecCount}",
              syncTime,
        response.Categories.Count,
     response.Ingredients.Count,
           response.Recipes.Count);

      return Results.Ok(response);
  })
        .WithName("Sync");

        // GET - pobierz wszystkie dane (pocz¹tkowa synchronizacja)
        group.MapGet("/full", async (RecipeDbContext db, ILogger<RecipeDbContext> logger) =>
        {
            logger.LogInformation("Full sync requested");

            var response = new SyncResponse
            {
                SyncedAt = DateTime.UtcNow,
                Categories = await db.Categories
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
                    .ToListAsync(),
                Ingredients = await db.Ingredients
                    .Select(i => new IngredientSyncDto
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Unit = i.Unit,
                        CreatedAt = i.CreatedAt,
                        UpdatedAt = i.UpdatedAt,
                        IsDeleted = i.IsDeleted
                    })
                    .ToListAsync(),
                Recipes = await db.Recipes
                    .Include(r => r.Ingredients)
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
            };

            logger.LogInformation(
        "Full sync completed. Categories: {CatCount}, Ingredients: {IngCount}, Recipes: {RecCount}",
           response.Categories.Count,
          response.Ingredients.Count,
                response.Recipes.Count);

            return Results.Ok(response);
        })
        .WithName("FullSync");
    }

    private static async Task ApplyClientChanges(SyncRequest request, RecipeDbContext db, ILogger logger)
    {
        var addedCategories = 0;
        var updatedCategories = 0;
        var skippedCategories = 0;

        // Kategorie
        foreach (var categoryDto in request.ChangedCategories)
        {
            var existing = await db.Categories.FindAsync(categoryDto.Id);
            if (existing is null)
            {
                logger.LogDebug("Adding new category: {CategoryId} - {CategoryName}", categoryDto.Id, categoryDto.Name);
                db.Categories.Add(new Category
                {
                    Id = categoryDto.Id,
                    Name = categoryDto.Name,
                    Description = categoryDto.Description,
                    IconName = categoryDto.IconName,
                    CreatedAt = categoryDto.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = categoryDto.IsDeleted
                });
                addedCategories++;
            }
            else if (categoryDto.UpdatedAt > existing.UpdatedAt)
            {
                logger.LogDebug(
               "Updating category: {CategoryId} - {CategoryName} (client: {ClientUpdated}, server: {ServerUpdated})",
              categoryDto.Id, categoryDto.Name, categoryDto.UpdatedAt, existing.UpdatedAt);
                existing.Name = categoryDto.Name;
                existing.Description = categoryDto.Description;
                existing.IconName = categoryDto.IconName;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = categoryDto.IsDeleted;
                updatedCategories++;
            }
            else
            {
                logger.LogDebug("Skipping category (server newer): {CategoryId} - {CategoryName} (client: {ClientUpdated}, server: {ServerUpdated})",
                       categoryDto.Id, categoryDto.Name, categoryDto.UpdatedAt, existing.UpdatedAt);
                skippedCategories++;
            }
        }

        logger.LogInformation("Categories processed - Added: {Added}, Updated: {Updated}, Skipped: {Skipped}",
                                addedCategories, updatedCategories, skippedCategories);

        // Save categories first so they exist for FK references
        await db.SaveChangesAsync();

        var addedIngredients = 0;
        var updatedIngredients = 0;
        var skippedIngredients = 0;

        // Sk³adniki
        foreach (var ingredientDto in request.ChangedIngredients)
        {
            var existing = await db.Ingredients.FindAsync(ingredientDto.Id);
            if (existing is null)
            {
                logger.LogDebug("Adding new ingredient: {IngredientId} - {IngredientName}", ingredientDto.Id, ingredientDto.Name);
                db.Ingredients.Add(new Ingredient
                {
                    Id = ingredientDto.Id,
                    Name = ingredientDto.Name,
                    Unit = ingredientDto.Unit,
                    CreatedAt = ingredientDto.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = ingredientDto.IsDeleted
                });
                addedIngredients++;
            }
            else if (ingredientDto.UpdatedAt > existing.UpdatedAt)
            {
                logger.LogDebug(
               "Updating ingredient: {IngredientId} - {IngredientName} (client: {ClientUpdated}, server: {ServerUpdated})",
           ingredientDto.Id, ingredientDto.Name, ingredientDto.UpdatedAt, existing.UpdatedAt);
                existing.Name = ingredientDto.Name;
                existing.Unit = ingredientDto.Unit;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = ingredientDto.IsDeleted;
                updatedIngredients++;
            }
            else
            {
                logger.LogDebug(
                      "Skipping ingredient (server newer): {IngredientId} - {IngredientName} (client: {ClientUpdated}, server: {ServerUpdated})",
                      ingredientDto.Id, ingredientDto.Name, ingredientDto.UpdatedAt, existing.UpdatedAt);
                skippedIngredients++;
            }
        }

        logger.LogInformation("Ingredients processed - Added: {Added}, Updated: {Updated}, Skipped: {Skipped}",
            addedIngredients, updatedIngredients, skippedIngredients);

        // Save ingredients so they exist for RecipeIngredient FK references
        await db.SaveChangesAsync();

        // Load valid FK IDs to validate recipe references
        var validCategoryIds = await db.Categories.Select(c => c.Id).ToHashSetAsync();
        var validIngredientIds = await db.Ingredients.Select(i => i.Id).ToHashSetAsync();

        var addedRecipes = 0;
        var updatedRecipes = 0;
        var skippedRecipes = 0;
        var invalidRecipes = 0;

        // Przepisy
        foreach (var recipeDto in request.ChangedRecipes)
        {
            // Validate CategoryId FK reference exists
            if (recipeDto.CategoryId.HasValue && !validCategoryIds.Contains(recipeDto.CategoryId.Value))
            {
                logger.LogWarning(
                          "Skipping recipe with invalid category reference: {RecipeId} - {RecipeTitle}, CategoryId: {CategoryId}",
                    recipeDto.Id, recipeDto.Title, recipeDto.CategoryId);
                invalidRecipes++;
                continue;
            }

            // Validate all IngredientId FK references exist
            var invalidIngredientRefs = recipeDto.Ingredients
       .Where(ri => !validIngredientIds.Contains(ri.IngredientId))
         .ToList();
            if (invalidIngredientRefs.Count > 0)
            {
                logger.LogWarning(
                    "Skipping recipe with invalid ingredient references: {RecipeId} - {RecipeTitle}, InvalidIngredientIds: {InvalidIds}",
                         recipeDto.Id, recipeDto.Title, string.Join(", ", invalidIngredientRefs.Select(r => r.IngredientId)));
                invalidRecipes++;
                continue;
            }

            var existing = await db.Recipes
    .Include(r => r.Ingredients)
       .FirstOrDefaultAsync(r => r.Id == recipeDto.Id);

            if (existing is null)
            {
                logger.LogDebug(
                "Adding new recipe: {RecipeId} - {RecipeTitle} with {IngredientCount} ingredients",
            recipeDto.Id, recipeDto.Title, recipeDto.Ingredients.Count);

                var recipe = new Recipe
                {
                    Id = recipeDto.Id,
                    Title = recipeDto.Title,
                    Description = recipeDto.Description,
                    Instructions = recipeDto.Instructions,
                    PreparationTimeMinutes = recipeDto.PreparationTimeMinutes,
                    CookingTimeMinutes = recipeDto.CookingTimeMinutes,
                    Servings = recipeDto.Servings,
                    Image = recipeDto.Image,
                    ImageContentType = recipeDto.ImageContentType,
                    CategoryId = recipeDto.CategoryId,
                    CreatedAt = recipeDto.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = recipeDto.IsDeleted
                };

                foreach (var ingredientDto in recipeDto.Ingredients)
                {
                    recipe.Ingredients.Add(new RecipeIngredient
                    {
                        Id = ingredientDto.Id,
                        RecipeId = recipe.Id,
                        IngredientId = ingredientDto.IngredientId,
                        Quantity = ingredientDto.Quantity,
                        Unit = ingredientDto.Unit,
                        Notes = ingredientDto.Notes,
                        Order = ingredientDto.Order
                    });
                }

                db.Recipes.Add(recipe);
                addedRecipes++;
            }
            else if (recipeDto.UpdatedAt > existing.UpdatedAt)
            {
                logger.LogDebug(
                   "Updating recipe: {RecipeId} - {RecipeTitle} (client: {ClientUpdated}, server: {ServerUpdated}), ingredients: {OldCount} -> {NewCount}",
                recipeDto.Id, recipeDto.Title, recipeDto.UpdatedAt, existing.UpdatedAt,
              existing.Ingredients.Count, recipeDto.Ingredients.Count);

                existing.Title = recipeDto.Title;
                existing.Description = recipeDto.Description;
                existing.Instructions = recipeDto.Instructions;
                existing.PreparationTimeMinutes = recipeDto.PreparationTimeMinutes;
                existing.CookingTimeMinutes = recipeDto.CookingTimeMinutes;
                existing.Servings = recipeDto.Servings;
                existing.Image = recipeDto.Image;
                existing.ImageContentType = recipeDto.ImageContentType;
                existing.CategoryId = recipeDto.CategoryId;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = recipeDto.IsDeleted;

                // Aktualizuj sk³adniki
                db.RecipeIngredients.RemoveRange(existing.Ingredients);
                foreach (var ingredientDto in recipeDto.Ingredients)
                {
                    db.RecipeIngredients.Add(new RecipeIngredient
                    {
                        Id = ingredientDto.Id,
                        RecipeId = existing.Id,
                        IngredientId = ingredientDto.IngredientId,
                        Quantity = ingredientDto.Quantity,
                        Unit = ingredientDto.Unit,
                        Notes = ingredientDto.Notes,
                        Order = ingredientDto.Order
                    });
                }
                updatedRecipes++;
            }
            else
            {
                logger.LogDebug(
                          "Skipping recipe (server newer): {RecipeId} - {RecipeTitle} (client: {ClientUpdated}, server: {ServerUpdated})",
                  recipeDto.Id, recipeDto.Title, recipeDto.UpdatedAt, existing.UpdatedAt);
                skippedRecipes++;
            }
        }

        logger.LogInformation(
              "Recipes processed - Added: {Added}, Updated: {Updated}, Skipped: {Skipped}, Invalid: {Invalid}",
       addedRecipes, updatedRecipes, skippedRecipes, invalidRecipes);

        await db.SaveChangesAsync();
    }

    private static async Task<List<CategorySyncDto>> GetServerCategories(DateTime since, RecipeDbContext db)
    {
        return await db.Categories
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
            .ToListAsync();
    }

    private static async Task<List<IngredientSyncDto>> GetServerIngredients(DateTime since, RecipeDbContext db)
    {
        return await db.Ingredients
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
            .ToListAsync();
    }

    private static async Task<List<RecipeSyncDto>> GetServerRecipes(DateTime since, RecipeDbContext db)
    {
        return await db.Recipes
            .Include(r => r.Ingredients)
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
            .ToListAsync();
    }
}

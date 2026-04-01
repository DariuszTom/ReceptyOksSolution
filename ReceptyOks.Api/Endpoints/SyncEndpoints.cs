using Microsoft.AspNetCore.Mvc;
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

            logger.LogInformation("Sync started. LastSyncedAt: {LastSync}, ChangedCategories: {CatCount}, ChangedIngredients: {IngCount}, ChangedRecipes: {RecCount}, ChangedMealPlans: {MpCount}",
                lastSync, request.ChangedCategories.Count, request.ChangedIngredients.Count, request.ChangedRecipes.Count, request.ChangedMealPlans.Count);

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
                Recipes = await GetServerRecipes(lastSync, db),
                MealPlans = await GetServerMealPlans(lastSync, db)
            };

            logger.LogInformation(
                "Sync completed. SyncedAt: {SyncTime}, ReturnedCategories: {CatCount}, ReturnedIngredients: {IngCount}, ReturnedRecipes: {RecCount}, ReturnedMealPlans: {MpCount}",
                syncTime,
                response.Categories.Count,
                response.Ingredients.Count,
                response.Recipes.Count,
                response.MealPlans.Count);

            return Results.Ok(response);
        })
            .WithName("Sync")
            .WithMetadata(new RequestSizeLimitAttribute(200_000_000));

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
                .ToListAsync(),
                MealPlans = await db.MealPlans
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
            };

            logger.LogInformation("Full sync completed. Categories: {CatCount}, Ingredients: {IngCount}, Recipes: {RecCount}, MealPlans: {MpCount}",
                response.Categories.Count, response.Ingredients.Count, response.Recipes.Count, response.MealPlans.Count);

            return Results.Ok(response);
        })
            .WithName("FullSync");

        // POST - upload wszystkich danych z klienta (nadpisuje serwer)
        group.MapPost("/upload-all", async (SyncRequest request, RecipeDbContext db, ILogger<RecipeDbContext> logger) =>
        {
            logger.LogInformation("Upload-all started. Categories: {CatCount}, Ingredients: {IngCount}, Recipes: {RecCount}, MealPlans: {MpCount}",
                request.ChangedCategories.Count, request.ChangedIngredients.Count, request.ChangedRecipes.Count, request.ChangedMealPlans.Count);
            // Zastosuj wszystkie dane z klienta (upsert)
            await ApplyClientChanges(request, db, logger);

            var syncTime = DateTime.UtcNow;

            var response = new SyncResponse
            {
                SyncedAt = syncTime,
                Categories = [],
                Ingredients = [],
                Recipes = [],
                MealPlans = []
            };

            logger.LogInformation("Upload-all completed. SyncedAt: {SyncTime}", syncTime);

            return Results.Ok(response);
        })
            .WithName("UploadAll")
            .WithMetadata(new RequestSizeLimitAttribute(200_000_000));
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
        var skippedInvalidCategory = 0;
        var skippedIngredientRefs = 0;

        // Przepisy
        foreach (var recipeDto in request.ChangedRecipes)
        {
            // Validate CategoryId FK reference exists
            if (recipeDto.CategoryId.HasValue && !validCategoryIds.Contains(recipeDto.CategoryId.Value))
            {
                logger.LogWarning(
                    "Skipping recipe with invalid category reference: {RecipeId} - {RecipeTitle}, CategoryId: {CategoryId}",
                    recipeDto.Id, recipeDto.Title, recipeDto.CategoryId);
                skippedInvalidCategory++;
                continue;
            }

            // Filter out invalid ingredient references instead of skipping the whole recipe
            var invalidIngredientRefs = recipeDto.Ingredients.Where(ri => !validIngredientIds.Contains(ri.IngredientId)).ToList();

            var validIngredients = recipeDto.Ingredients.Where(ri => validIngredientIds.Contains(ri.IngredientId))
                .ToList();

            if (invalidIngredientRefs.Count > 0)
            {
                logger.LogWarning(
                    "Recipe {RecipeId} - {RecipeTitle} has {InvalidCount} invalid ingredient references (skipping them): {InvalidIds}",
                    recipeDto.Id, recipeDto.Title, invalidIngredientRefs.Count,
                    string.Join(", ", invalidIngredientRefs.Select(r => r.IngredientId)));
                skippedIngredientRefs += invalidIngredientRefs.Count;
            }

            var existing = await db.Recipes.Include(r => r.Ingredients).FirstOrDefaultAsync(r => r.Id == recipeDto.Id);

            if (existing is null)
            {
                logger.LogDebug("Adding new recipe: {RecipeId} - {RecipeTitle} with {IngredientCount} ingredients (valid: {ValidCount})",
                    recipeDto.Id, recipeDto.Title, recipeDto.Ingredients.Count, validIngredients.Count);

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

                foreach (var ingredientDto in validIngredients)
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
                logger.LogDebug("Updating recipe: {RecipeId} - {RecipeTitle} (client: {ClientUpdated}, server: {ServerUpdated}), ingredients: {OldCount} -> {NewCount} (valid: {ValidCount})",
                    recipeDto.Id, recipeDto.Title, recipeDto.UpdatedAt, existing.UpdatedAt,
                    existing.Ingredients.Count, recipeDto.Ingredients.Count, validIngredients.Count);

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

                // Aktualizuj sk³adniki (tylko poprawne referencje)
                db.RecipeIngredients.RemoveRange(existing.Ingredients);
                foreach (var ingredientDto in validIngredients)
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
            "Recipes processed - Added: {Added}, Updated: {Updated}, Skipped: {Skipped}, SkippedInvalidCategory: {InvalidCategory}, SkippedIngredientRefs: {SkippedRefs}",
            addedRecipes, updatedRecipes, skippedRecipes, skippedInvalidCategory, skippedIngredientRefs);

        await db.SaveChangesAsync();

        // Plany posi³ków
        var validRecipeIds = await db.Recipes.Select(r => r.Id).ToHashSetAsync();
        var addedMealPlans = 0;
        var updatedMealPlans = 0;
        var skippedMealPlans = 0;
        var skippedInvalidRecipe = 0;

        foreach (var mealPlanDto in request.ChangedMealPlans)
        {
            if (!validRecipeIds.Contains(mealPlanDto.RecipeId))
            {
                logger.LogWarning(
                    "Skipping meal plan with invalid recipe reference: {MealPlanId}, RecipeId: {RecipeId}",
                    mealPlanDto.Id, mealPlanDto.RecipeId);
                skippedInvalidRecipe++;
                continue;
            }

            var existing = await db.MealPlans.FindAsync(mealPlanDto.Id);
            if (existing is null)
            {
                logger.LogDebug("Adding new meal plan: {MealPlanId} for {Date}", mealPlanDto.Id, mealPlanDto.Date);
                db.MealPlans.Add(new MealPlan
                {
                    Id = mealPlanDto.Id,
                    Date = mealPlanDto.Date,
                    StartHour = mealPlanDto.StartHour,
                    DurationMinutes = mealPlanDto.DurationMinutes,
                    RecipeId = mealPlanDto.RecipeId,
                    Notes = mealPlanDto.Notes,
                    CreatedAt = mealPlanDto.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = mealPlanDto.IsDeleted
                });
                addedMealPlans++;
            }
            else if (mealPlanDto.UpdatedAt > existing.UpdatedAt)
            {
                logger.LogDebug(
                    "Updating meal plan: {MealPlanId} (client: {ClientUpdated}, server: {ServerUpdated})",
                    mealPlanDto.Id, mealPlanDto.UpdatedAt, existing.UpdatedAt);
                existing.Date = mealPlanDto.Date;
                existing.StartHour = mealPlanDto.StartHour;
                existing.DurationMinutes = mealPlanDto.DurationMinutes;
                existing.RecipeId = mealPlanDto.RecipeId;
                existing.Notes = mealPlanDto.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = mealPlanDto.IsDeleted;
                updatedMealPlans++;
            }
            else
            {
                logger.LogDebug(
                    "Skipping meal plan (server newer): {MealPlanId} (client: {ClientUpdated}, server: {ServerUpdated})",
                    mealPlanDto.Id, mealPlanDto.UpdatedAt, existing.UpdatedAt);
                skippedMealPlans++;
            }
        }

        logger.LogInformation(
            "MealPlans processed - Added: {Added}, Updated: {Updated}, Skipped: {Skipped}, SkippedInvalidRecipe: {InvalidRecipe}",
            addedMealPlans, updatedMealPlans, skippedMealPlans, skippedInvalidRecipe);

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

    private static async Task<List<MealPlanSyncDto>> GetServerMealPlans(DateTime since, RecipeDbContext db)
    {
        return await db.MealPlans
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
            .ToListAsync();
    }
}

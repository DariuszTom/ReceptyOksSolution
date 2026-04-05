namespace ReceptyOks.Api.Services;

/// <summary>
/// Service for handling synchronization operations between client and server.
/// Uses repository pattern for batch data access to minimize database round trips.
/// </summary>
public class SyncService : ISyncService
{
    private readonly RecipeDbContext _db;
    private readonly ISyncRepository _repository;
    private readonly ILogger<SyncService> _logger;

    public SyncService(RecipeDbContext db, ISyncRepository repository, ILogger<SyncService> logger)
    {
        _db = db;
        _repository = repository;
        _logger = logger;
    }

    public async Task<SyncResponse> SyncAsync(SyncRequest request, DateTime? lastSyncedAt)
    {
        var lastSync = lastSyncedAt ?? DateTime.MinValue;

        _logger.LogInformation(
            "Sync started. LastSyncedAt: {LastSync}, ChangedCategories: {CatCount}, ChangedIngredients: {IngCount}, ChangedRecipes: {RecCount}, ChangedMealPlans: {MpCount}",
            lastSync, request.ChangedCategories.Count, request.ChangedIngredients.Count,
            request.ChangedRecipes.Count, request.ChangedMealPlans.Count);

        // Apply client changes
        await ApplyClientChangesAsync(request).ConfigureAwait(false);

        // Capture sync time after applying changes
        var syncTime = DateTime.UtcNow;

        // Get server changes since last sync
        var response = new SyncResponse
        {
            SyncedAt = syncTime,
            Categories = await _repository.GetCategoriesModifiedSinceAsync(lastSync).ConfigureAwait(false),
            Ingredients = await _repository.GetIngredientsModifiedSinceAsync(lastSync).ConfigureAwait(false),
            Recipes = await _repository.GetRecipesModifiedSinceAsync(lastSync).ConfigureAwait(false),
            MealPlans = await _repository.GetMealPlansModifiedSinceAsync(lastSync).ConfigureAwait(false)
        };

        _logger.LogInformation(
            "Sync completed. SyncedAt: {SyncTime}, ReturnedCategories: {CatCount}, ReturnedIngredients: {IngCount}, ReturnedRecipes: {RecCount}, ReturnedMealPlans: {MpCount}",
            syncTime, response.Categories.Count, response.Ingredients.Count,
            response.Recipes.Count, response.MealPlans.Count);

        return response;
    }

    public async Task<SyncResponse> GetFullSyncAsync()
    {
        _logger.LogInformation("Full sync requested");

        var response = new SyncResponse
        {
            SyncedAt = DateTime.UtcNow,
            Categories = await _repository.GetAllCategoriesAsync().ConfigureAwait(false),
            Ingredients = await _repository.GetAllIngredientsAsync().ConfigureAwait(false),
            Recipes = await _repository.GetAllRecipesAsync().ConfigureAwait(false),
            MealPlans = await _repository.GetAllMealPlansAsync().ConfigureAwait(false)
        };

        _logger.LogInformation(
            "Full sync completed. Categories: {CatCount}, Ingredients: {IngCount}, Recipes: {RecCount}, MealPlans: {MpCount}",
            response.Categories.Count, response.Ingredients.Count,
            response.Recipes.Count, response.MealPlans.Count);

        return response;
    }

    public async Task<SyncResponse> UploadAllAsync(SyncRequest request)
    {
        _logger.LogInformation(
            "Upload-all started. Categories: {CatCount}, Ingredients: {IngCount}, Recipes: {RecCount}, MealPlans: {MpCount}",
            request.ChangedCategories.Count, request.ChangedIngredients.Count,
            request.ChangedRecipes.Count, request.ChangedMealPlans.Count);

        await ApplyClientChangesAsync(request).ConfigureAwait(false);

        var response = new SyncResponse
        {
            SyncedAt = DateTime.UtcNow,
            Categories = [],
            Ingredients = [],
            Recipes = [],
            MealPlans = []
        };

        _logger.LogInformation("Upload-all completed. SyncedAt: {SyncTime}", response.SyncedAt);

        return response;
    }

    /// <summary>
    /// Applies client changes using batch queries to minimize Azure SQL round trips.
    /// Loads all needed entities upfront (1 query per type) instead of N FindAsync calls.
    /// </summary>
    private async Task ApplyClientChangesAsync(SyncRequest request)
    {
        var stats = new SyncOperationStatistics();

        // De-duplicate request lists by Id to prevent duplicate key tracking errors
        var changedCategories = DeduplicateById(request.ChangedCategories);
        var changedIngredients = DeduplicateById(request.ChangedIngredients);
        var changedRecipes = DeduplicateById(request.ChangedRecipes);
        var changedMealPlans = DeduplicateById(request.ChangedMealPlans);

        // Process categories first
        await ProcessCategoriesAsync(changedCategories, stats.Categories).ConfigureAwait(false);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        // Process ingredients
        await ProcessIngredientsAsync(changedIngredients, stats.Ingredients).ConfigureAwait(false);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        // Process recipes with FK validation
        await ProcessRecipesAsync(changedRecipes, stats.Recipes).ConfigureAwait(false);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        // Process meal plans with FK validation
        await ProcessMealPlansAsync(changedMealPlans, stats.MealPlans).ConfigureAwait(false);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        // Log all statistics
        stats.LogAllSummaries(_logger);

        // Clear change tracker to free memory after large sync operations
        _db.ChangeTracker.Clear();
    }

    private async Task ProcessCategoriesAsync(List<CategorySyncDto> categories, SyncStatistics stats)
    {
        var categoryIds = categories.Select(c => c.Id).ToList();
        var existingCategories = await _repository.GetCategoriesByIdsAsync(categoryIds).ConfigureAwait(false);

        foreach (var categoryDto in categories)
        {
            if (!existingCategories.TryGetValue(categoryDto.Id, out var existing))
            {
                _logger.LogDebug("Adding new category: {CategoryId} - {CategoryName}",
                    categoryDto.Id, categoryDto.Name);

                _db.Categories.Add(new Category
                {
                    Id = categoryDto.Id,
                    Name = categoryDto.Name,
                    Description = categoryDto.Description,
                    IconName = categoryDto.IconName,
                    CreatedAt = categoryDto.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = categoryDto.IsDeleted
                });
                stats.Added++;
            }
            else if (categoryDto.UpdatedAt > existing.UpdatedAt)
            {
                _logger.LogDebug(
                    "Updating category: {CategoryId} - {CategoryName} (client: {ClientUpdated}, server: {ServerUpdated})",
                    categoryDto.Id, categoryDto.Name, categoryDto.UpdatedAt, existing.UpdatedAt);

                existing.Name = categoryDto.Name;
                existing.Description = categoryDto.Description;
                existing.IconName = categoryDto.IconName;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = categoryDto.IsDeleted;
                stats.Updated++;
            }
            else
            {
                _logger.LogDebug(
                    "Skipping category (server newer): {CategoryId} - {CategoryName} (client: {ClientUpdated}, server: {ServerUpdated})",
                    categoryDto.Id, categoryDto.Name, categoryDto.UpdatedAt, existing.UpdatedAt);
                stats.Skipped++;
            }
        }
    }

    private async Task ProcessIngredientsAsync(List<IngredientSyncDto> ingredients, SyncStatistics stats)
    {
        var ingredientIds = ingredients.Select(i => i.Id).ToList();
        var existingIngredients = await _repository.GetIngredientsByIdsAsync(ingredientIds).ConfigureAwait(false);

        foreach (var ingredientDto in ingredients)
        {
            if (!existingIngredients.TryGetValue(ingredientDto.Id, out var existing))
            {
                _logger.LogDebug("Adding new ingredient: {IngredientId} - {IngredientName}",
                    ingredientDto.Id, ingredientDto.Name);

                _db.Ingredients.Add(new Ingredient
                {
                    Id = ingredientDto.Id,
                    Name = ingredientDto.Name,
                    Unit = ingredientDto.Unit,
                    CreatedAt = ingredientDto.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = ingredientDto.IsDeleted
                });
                stats.Added++;
            }
            else if (ingredientDto.UpdatedAt > existing.UpdatedAt)
            {
                _logger.LogDebug(
                    "Updating ingredient: {IngredientId} - {IngredientName} (client: {ClientUpdated}, server: {ServerUpdated})",
                    ingredientDto.Id, ingredientDto.Name, ingredientDto.UpdatedAt, existing.UpdatedAt);

                existing.Name = ingredientDto.Name;
                existing.Unit = ingredientDto.Unit;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = ingredientDto.IsDeleted;
                stats.Updated++;
            }
            else
            {
                _logger.LogDebug(
                    "Skipping ingredient (server newer): {IngredientId} - {IngredientName} (client: {ClientUpdated}, server: {ServerUpdated})",
                    ingredientDto.Id, ingredientDto.Name, ingredientDto.UpdatedAt, existing.UpdatedAt);
                stats.Skipped++;
            }
        }
    }

    private async Task ProcessRecipesAsync(List<RecipeSyncDto> recipes, SyncStatistics stats)
    {
        // Validate FK references
        var referencedCategoryIds = recipes
            .Where(r => r.CategoryId.HasValue)
            .Select(r => r.CategoryId!.Value)
            .Distinct()
            .ToList();
        var validCategoryIds = await _repository.GetValidCategoryIdsAsync(referencedCategoryIds).ConfigureAwait(false);

        var referencedIngredientIds = recipes
            .SelectMany(r => r.Ingredients ?? Enumerable.Empty<RecipeIngredientSyncDto>())
            .Select(ri => ri.IngredientId)
            .Distinct()
            .ToList();
        var validIngredientIds = await _repository.GetValidIngredientIdsAsync(referencedIngredientIds).ConfigureAwait(false);

        var recipeIds = recipes.Select(r => r.Id).ToList();
        var existingRecipes = await _repository.GetRecipesWithIngredientsByIdsAsync(recipeIds).ConfigureAwait(false);

        foreach (var recipeDto in recipes)
        {
            // Validate CategoryId FK reference
            if (recipeDto.CategoryId.HasValue && !validCategoryIds.Contains(recipeDto.CategoryId.Value))
            {
                _logger.LogWarning(
                    "Skipping recipe with invalid category reference: {RecipeId} - {RecipeTitle}, CategoryId: {CategoryId}",
                    recipeDto.Id, recipeDto.Title, recipeDto.CategoryId);
                stats.SkippedInvalidReferences++;
                continue;
            }

            // Filter out invalid ingredient references
            var invalidIngredientRefs = recipeDto.Ingredients
                .Where(ri => !validIngredientIds.Contains(ri.IngredientId))
                .ToList();
            var validIngredients = recipeDto.Ingredients
                .Where(ri => validIngredientIds.Contains(ri.IngredientId))
                .ToList();

            if (invalidIngredientRefs.Count > 0)
            {
                _logger.LogWarning(
                    "Recipe {RecipeId} - {RecipeTitle} has {InvalidCount} invalid ingredient references (skipping them): {InvalidIds}",
                    recipeDto.Id, recipeDto.Title, invalidIngredientRefs.Count,
                    string.Join(", ", invalidIngredientRefs.Select(r => r.IngredientId)));
                stats.SkippedInvalidReferences += invalidIngredientRefs.Count;
            }

            if (!existingRecipes.TryGetValue(recipeDto.Id, out var existing))
            {
                _logger.LogDebug(
                    "Adding new recipe: {RecipeId} - {RecipeTitle} with {IngredientCount} ingredients (valid: {ValidCount})",
                    recipeDto.Id, recipeDto.Title, recipeDto.Ingredients.Count, validIngredients.Count);

                var recipe = CreateRecipeFromDto(recipeDto, validIngredients);
                _db.Recipes.Add(recipe);
                stats.Added++;
            }
            else if (recipeDto.UpdatedAt > existing.UpdatedAt)
            {
                _logger.LogDebug(
                    "Updating recipe: {RecipeId} - {RecipeTitle} (client: {ClientUpdated}, server: {ServerUpdated}), ingredients: {OldCount} -> {NewCount} (valid: {ValidCount})",
                    recipeDto.Id, recipeDto.Title, recipeDto.UpdatedAt, existing.UpdatedAt,
                    existing.Ingredients.Count, recipeDto.Ingredients.Count, validIngredients.Count);

                UpdateRecipeFromDto(existing, recipeDto, validIngredients);
                stats.Updated++;
            }
            else
            {
                _logger.LogDebug(
                    "Skipping recipe (server newer): {RecipeId} - {RecipeTitle} (client: {ClientUpdated}, server: {ServerUpdated})",
                    recipeDto.Id, recipeDto.Title, recipeDto.UpdatedAt, existing.UpdatedAt);
                stats.Skipped++;
            }
        }
    }

    private async Task ProcessMealPlansAsync(List<MealPlanSyncDto> mealPlans, SyncStatistics stats)
    {
        // Validate FK references
        var referencedRecipeIds = mealPlans.Select(mp => mp.RecipeId).Distinct().ToList();
        var validRecipeIds = await _repository.GetValidRecipeIdsAsync(referencedRecipeIds).ConfigureAwait(false);

        var mealPlanIds = mealPlans.Select(mp => mp.Id).ToList();
        var existingMealPlans = await _repository.GetMealPlansByIdsAsync(mealPlanIds).ConfigureAwait(false);

        foreach (var mealPlanDto in mealPlans)
        {
            if (!validRecipeIds.Contains(mealPlanDto.RecipeId))
            {
                _logger.LogWarning(
                    "Skipping meal plan with invalid recipe reference: {MealPlanId}, RecipeId: {RecipeId}",
                    mealPlanDto.Id, mealPlanDto.RecipeId);
                stats.SkippedInvalidReferences++;
                continue;
            }

            if (!existingMealPlans.TryGetValue(mealPlanDto.Id, out var existing))
            {
                _logger.LogDebug("Adding new meal plan: {MealPlanId} for {Date}",
                    mealPlanDto.Id, mealPlanDto.Date);

                _db.MealPlans.Add(new MealPlan
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
                stats.Added++;
            }
            else if (mealPlanDto.UpdatedAt > existing.UpdatedAt)
            {
                _logger.LogDebug(
                    "Updating meal plan: {MealPlanId} (client: {ClientUpdated}, server: {ServerUpdated})",
                    mealPlanDto.Id, mealPlanDto.UpdatedAt, existing.UpdatedAt);

                existing.Date = mealPlanDto.Date;
                existing.StartHour = mealPlanDto.StartHour;
                existing.DurationMinutes = mealPlanDto.DurationMinutes;
                existing.RecipeId = mealPlanDto.RecipeId;
                existing.Notes = mealPlanDto.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = mealPlanDto.IsDeleted;
                stats.Updated++;
            }
            else
            {
                _logger.LogDebug(
                    "Skipping meal plan (server newer): {MealPlanId} (client: {ClientUpdated}, server: {ServerUpdated})",
                    mealPlanDto.Id, mealPlanDto.UpdatedAt, existing.UpdatedAt);
                stats.Skipped++;
            }
        }
    }

    private Recipe CreateRecipeFromDto(RecipeSyncDto dto, List<RecipeIngredientSyncDto> validIngredients)
    {
        var recipe = new Recipe
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            Instructions = dto.Instructions,
            PreparationTimeMinutes = dto.PreparationTimeMinutes,
            CookingTimeMinutes = dto.CookingTimeMinutes,
            Servings = dto.Servings,
            Image = dto.Image,
            ImageContentType = dto.ImageContentType,
            CategoryId = dto.CategoryId,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = dto.IsDeleted
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

        return recipe;
    }

    private void UpdateRecipeFromDto(Recipe existing, RecipeSyncDto dto, List<RecipeIngredientSyncDto> validIngredients)
    {
        existing.Title = dto.Title;
        existing.Description = dto.Description;
        existing.Instructions = dto.Instructions;
        existing.PreparationTimeMinutes = dto.PreparationTimeMinutes;
        existing.CookingTimeMinutes = dto.CookingTimeMinutes;
        existing.Servings = dto.Servings;
        existing.Image = dto.Image;
        existing.ImageContentType = dto.ImageContentType;
        existing.CategoryId = dto.CategoryId;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.IsDeleted = dto.IsDeleted;

        // Update ingredients
        _db.RecipeIngredients.RemoveRange(existing.Ingredients);
        foreach (var ingredientDto in validIngredients)
        {
            _db.RecipeIngredients.Add(new RecipeIngredient
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
    }

    private static List<T> DeduplicateById<T>(List<T> items) where T : IHasId, IHasUpdatedAt
    {
        return items
            .GroupBy(i => i.Id)
            .Select(g => g.OrderByDescending(i => i.UpdatedAt).First())
            .ToList();
    }
}

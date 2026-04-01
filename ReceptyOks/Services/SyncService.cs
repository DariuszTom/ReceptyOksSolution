using Polly;
using Polly.Retry;
using ReceptyOks.Interfaces;
using ReceptyOks.Shared.DTOs;
using System.Net.Http.Json;

namespace ReceptyOks.Services;

public class SyncService : ISyncService
{
    private readonly LocalDatabase _localDb;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SyncService> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy = Policy
        .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, _, _, _) => outcome.Result?.Dispose());

    public SyncService(LocalDatabase localDb, HttpClient httpClient, ILogger<SyncService> logger)
    {
        _localDb = localDb;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAsync()
    {
        var result = new SyncResult();

        try
        {
            // Sprawdź połączenie
            var connectivity = Connectivity.Current.NetworkAccess;
            if (connectivity != NetworkAccess.Internet)
            {
                result.Success = false;
                result.Message = "Brak połączenia z internetem";
                return result;
            }

            var lastSync = await _localDb.GetLastSyncTimeAsync().ConfigureAwait(false);

            // Pobierz lokalne zmiany do wysłania
            var request = new SyncRequest
            {
                LastSyncedAt = lastSync,
                ChangedRecipes = await GetChangedRecipesAsync().ConfigureAwait(false),
                ChangedCategories = await GetChangedCategoriesAsync().ConfigureAwait(false),
                ChangedIngredients = await GetChangedIngredientsAsync().ConfigureAwait(false),
                ChangedMealPlans = await GetChangedMealPlansAsync().ConfigureAwait(false)
            };

            using var response = await _retryPolicy.ExecuteAsync(() =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync")
                {
                    Content = JsonContent.Create(request)
                };

                return _httpClient.SendAsync(httpRequest);
            }).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Sync failed with status code {StatusCode}", response.StatusCode);
                result.Success = false;
                result.Message = $"Błąd serwera: {response.StatusCode}";
                return result;
            }

            var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>().ConfigureAwait(false);

            if (syncResponse is null)
            {
                _logger.LogError("Sync failed: server returned null response");
                result.Success = false;
                result.Message = "Pusta odpowiedź serwera";
                return result;
            }

            // Zastosuj zmiany z serwera lokalnie
            var applyResult = await ApplyServerChangesAsync(syncResponse).ConfigureAwait(false);

            // Zawsze czyść dirty flags — serwer już przyjął zmiany klienta,
            // więc ponowne wysłanie spowodowałoby duplikaty/konflikty.
            await _localDb.ClearDirtyFlagsAsync().ConfigureAwait(false);

            // Raportuj liczbę faktycznie zastosowanych elementów (otrzymane - nieudane).
            result.CategoriesSynced = syncResponse.Categories.Count - applyResult.FailedCategories;
            result.IngredientsSynced = syncResponse.Ingredients.Count - applyResult.FailedIngredients;
            result.RecipesSynced = syncResponse.Recipes.Count - applyResult.FailedRecipes;
            result.MealPlansSynced = syncResponse.MealPlans.Count - applyResult.FailedMealPlans;

            if (applyResult.TotalFailed > 0)
            {
                // Nie przesuwaj LastSyncedAt — nieudane elementy muszą zostać
                // ponownie pobrane przy następnej synchronizacji.
                _logger.LogWarning("Sync partial: {FailedItems} items failed to apply locally, keeping LastSyncedAt at {LastSync}", applyResult.TotalFailed, lastSync);
                result.Success = true;
                result.Message = $"Synchronizacja częściowa: {applyResult.TotalFailed} elementów nie zostało zastosowanych";
            }
            else
            {
                await _localDb.SetLastSyncTimeAsync(syncResponse.SyncedAt).ConfigureAwait(false);
                result.Success = true;
                result.Message = "Synchronizacja zakończona pomyślnie";
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed with exception");
            result.Success = false;
            result.Message = $"Błąd synchronizacji: {ex.Message}";
        }

        return result;
    }

    public async Task<SyncResult> FullSyncAsync()
    {
        var result = new SyncResult();

        try
        {

            using var response = await _httpClient.GetAsync("/api/sync/full").ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Full sync failed with status code {StatusCode}", response.StatusCode);
                result.Success = false;
                result.Message = $"Błąd serwera: {response.StatusCode}";
                return result;
            }

            var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>().ConfigureAwait(false);

            if (syncResponse is null)
            {
                _logger.LogError("Full sync failed: server returned null response");
                result.Success = false;
                result.Message = "Pusta odpowiedź serwera";
                return result;
            }

            var applyResult = await ApplyServerChangesAsync(syncResponse).ConfigureAwait(false);

            result.CategoriesSynced = syncResponse.Categories.Count - applyResult.FailedCategories;
            result.IngredientsSynced = syncResponse.Ingredients.Count - applyResult.FailedIngredients;
            result.RecipesSynced = syncResponse.Recipes.Count - applyResult.FailedRecipes;
            result.MealPlansSynced = syncResponse.MealPlans.Count - applyResult.FailedMealPlans;

            if (applyResult.TotalFailed > 0)
            {
                _logger.LogWarning("Full sync partial: {FailedItems} items failed to apply locally", applyResult.TotalFailed);
                result.Success = true;
                result.Message = $"Pełna synchronizacja częściowa: {applyResult.TotalFailed} elementów nie zostało zastosowanych";
            }
            else
            {
                await _localDb.SetLastSyncTimeAsync(syncResponse.SyncedAt).ConfigureAwait(false);
                result.Success = true;
                result.Message = "Pełna synchronizacja zakończona";
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full sync failed with exception");
            result.Success = false;
            result.Message = $"Błąd: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Wysyła wszystkie lokalne przepisy i kategorie na backend w partiach,
    /// aby uniknąć przekroczenia limitu rozmiaru żądania (413 Request Entity Too Large).
    /// </summary>
    public async Task<SyncResult> UploadAllAsync()
    {
        var result = new SyncResult();

        try
        {
            var connectivity = Connectivity.Current.NetworkAccess;
            if (connectivity != NetworkAccess.Internet)
            {
                _logger.LogWarning("Upload-all aborted: no internet connection");
                result.Success = false;
                result.Message = "Brak połączenia z internetem";
                return result;
            }

            var allRecipes = await GetAllRecipesForUploadAsync().ConfigureAwait(false);
            var allCategories = await GetAllCategoriesForUploadAsync().ConfigureAwait(false);
            var allIngredients = await GetAllIngredientsForUploadAsync().ConfigureAwait(false);
            var allMealPlans = await GetAllMealPlansForUploadAsync().ConfigureAwait(false);

            // Batch recipes to avoid 413 (images make the payload large)
            const int batchSize = 10;
            var recipeBatches = allRecipes
                .Select((recipe, index) => (recipe, index))
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.recipe).ToList())
                .ToList();

            // If there are no recipes, still send one request with categories/ingredients
            if (recipeBatches.Count == 0)
                recipeBatches.Add([]);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            DateTime lastSyncedAt = default;

            for (int i = 0; i < recipeBatches.Count; i++)
            {
                var request = new SyncRequest
                {
                    LastSyncedAt = null,
                    ChangedRecipes = recipeBatches[i],
                    // Send categories/ingredients/mealplans only in the first batch
                    ChangedCategories = i == 0 ? allCategories : [],
                    ChangedIngredients = i == 0 ? allIngredients : [],
                    ChangedMealPlans = i == 0 ? allMealPlans : []
                };

                using var response = await _retryPolicy.ExecuteAsync(ct =>
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync/upload-all")
                    {
                        Content = JsonContent.Create(request),
                    };

                    return _httpClient.SendAsync(httpRequest, ct);
                }, timeoutCts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Upload-all batch {Batch} failed with status code {StatusCode}", i + 1, response.StatusCode);
                    result.Success = false;
                    result.Message = $"Błąd serwera (partia {i + 1}): {response.StatusCode}";
                    return result;
                }

                var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>().ConfigureAwait(false);

                if (syncResponse is null)
                {
                    _logger.LogError("Upload-all batch {Batch} failed: server returned null response", i + 1);
                    result.Success = false;
                    result.Message = "Pusta odpowiedź serwera";
                    return result;
                }

                lastSyncedAt = syncResponse.SyncedAt;
            }

            await _localDb.ClearDirtyFlagsAsync().ConfigureAwait(false);
            await _localDb.SetLastSyncTimeAsync(lastSyncedAt).ConfigureAwait(false);

            result.Success = true;
            result.Message = "Wszystkie dane zostały wysłane na serwer";
            result.RecipesSynced = allRecipes.Count;
            result.CategoriesSynced = allCategories.Count;
            result.IngredientsSynced = allIngredients.Count;
            result.MealPlansSynced = allMealPlans.Count;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload-all failed with exception");
            result.Success = false;
            result.Message = $"Błąd wysyłania: {ex.Message}";
        }

        return result;
    }

    private async Task<List<RecipeSyncDto>> GetAllRecipesForUploadAsync()
    {
        var recipes = await _localDb.GetRecipesAsync().ConfigureAwait(false);
        return await MapRecipesToSyncDtosAsync(recipes).ConfigureAwait(false);
    }

    private async Task<List<CategorySyncDto>> GetAllCategoriesForUploadAsync()
    {
        var categories = await _localDb.GetCategoriesAsync().ConfigureAwait(false);
        return MapCategoriesToSyncDtos(categories);
    }

    private async Task<List<IngredientSyncDto>> GetAllIngredientsForUploadAsync()
    {
        var ingredients = await _localDb.GetIngredientsAsync().ConfigureAwait(false);
        return MapIngredientsToSyncDtos(ingredients);
    }

    private async Task<List<RecipeSyncDto>> GetChangedRecipesAsync()
    {
        var dirtyRecipes = await _localDb.GetDirtyRecipesAsync().ConfigureAwait(false);
        return await MapRecipesToSyncDtosAsync(dirtyRecipes).ConfigureAwait(false);
    }

    private async Task<List<CategorySyncDto>> GetChangedCategoriesAsync()
    {
        var dirtyCategories = await _localDb.GetDirtyCategoriesAsync().ConfigureAwait(false);
        return MapCategoriesToSyncDtos(dirtyCategories);
    }

    private async Task<List<IngredientSyncDto>> GetChangedIngredientsAsync()
    {
        var dirtyIngredients = await _localDb.GetDirtyIngredientsAsync().ConfigureAwait(false);
        return MapIngredientsToSyncDtos(dirtyIngredients);
    }

    private async Task<List<MealPlanSyncDto>> GetChangedMealPlansAsync()
    {
        var dirtyMealPlans = await _localDb.GetDirtyMealPlansAsync().ConfigureAwait(false);
        return MapMealPlansToSyncDtos(dirtyMealPlans);
    }

    private async Task<List<MealPlanSyncDto>> GetAllMealPlansForUploadAsync()
    {
        var mealPlans = await _localDb.GetAllMealPlansAsync().ConfigureAwait(false);
        return MapMealPlansToSyncDtos(mealPlans);
    }

    private async Task<List<RecipeSyncDto>> MapRecipesToSyncDtosAsync(List<RecipeLocal> recipes)
    {
        var result = new List<RecipeSyncDto>();
        foreach (var recipe in recipes)
        {
            var ingredients = await _localDb.GetRecipeIngredientsAsync(recipe.Id).ConfigureAwait(false);
            result.Add(new RecipeSyncDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description,
                Instructions = recipe.Instructions,
                PreparationTimeMinutes = recipe.PreparationTimeMinutes,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Servings = recipe.Servings,
                Image = recipe.Image,
                ImageContentType = recipe.ImageContentType,
                CategoryId = recipe.CategoryId,
                CreatedAt = recipe.CreatedAt,
                UpdatedAt = recipe.UpdatedAt,
                IsDeleted = recipe.IsDeleted,
                Ingredients = ingredients.Select(i => new RecipeIngredientSyncDto
                {
                    Id = i.Id,
                    IngredientId = i.IngredientId,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Notes = i.Notes,
                    Order = i.Order
                }).ToList()
            });
        }
        return result;
    }

    private static List<CategorySyncDto> MapCategoriesToSyncDtos(List<CategoryLocal> categories)
    {
        return categories.Select(c => new CategorySyncDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            IconName = c.IconName,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            IsDeleted = c.IsDeleted
        }).ToList();
    }

    private static List<IngredientSyncDto> MapIngredientsToSyncDtos(List<IngredientLocal> ingredients)
    {
        return ingredients.Select(i => new IngredientSyncDto
        {
            Id = i.Id,
            Name = i.Name,
            Unit = i.Unit,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            IsDeleted = i.IsDeleted
        }).ToList();
    }

    private static List<MealPlanSyncDto> MapMealPlansToSyncDtos(List<MealPlanLocal> mealPlans)
    {
        return mealPlans.Select(mp => new MealPlanSyncDto
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
        }).ToList();
    }

    /// <summary>
    /// Stosuje zmiany z serwera lokalnie. Błąd pojedynczego elementu
    /// nie blokuje pozostałych — zapobiega pętli retry.
    /// Zwraca liczbę elementów, których nie udało się zastosować.
    /// </summary>
    private async Task<ApplyResult> ApplyServerChangesAsync(SyncResponse response)
    {
        var failedCategories = 0;
        var failedIngredients = 0;
        var failedRecipes = 0;
        var failedMealPlans = 0;

        // Kategorie
        foreach (var categoryDto in response.Categories)
        {
            try
            {
                await _localDb.ApplyServerCategoryAsync(new CategoryLocal
                {
                    Id = categoryDto.Id,
                    Name = categoryDto.Name,
                    Description = categoryDto.Description,
                    IconName = categoryDto.IconName,
                    CreatedAt = categoryDto.CreatedAt,
                    UpdatedAt = categoryDto.UpdatedAt,
                    IsDeleted = categoryDto.IsDeleted
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply server category {CategoryId}", categoryDto.Id);
                failedCategories++;
            }
        }

        // Składniki
        foreach (var ingredientDto in response.Ingredients)
        {
            try
            {
                await _localDb.ApplyServerIngredientAsync(new IngredientLocal
                {
                    Id = ingredientDto.Id,
                    Name = ingredientDto.Name,
                    Unit = ingredientDto.Unit,
                    CreatedAt = ingredientDto.CreatedAt,
                    UpdatedAt = ingredientDto.UpdatedAt,
                    IsDeleted = ingredientDto.IsDeleted
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply server ingredient {IngredientId}", ingredientDto.Id);
                failedIngredients++;
            }
        }

        // Przepisy
        foreach (var recipeDto in response.Recipes)
        {
            try
            {
                await _localDb.ApplyServerRecipeAsync(new RecipeLocal
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
                    UpdatedAt = recipeDto.UpdatedAt,
                    IsDeleted = recipeDto.IsDeleted
                }).ConfigureAwait(false);

                // Składniki przepisu
                var recipeIngredients = recipeDto.Ingredients.Select(i => new RecipeIngredientLocal
                {
                    Id = i.Id,
                    RecipeId = recipeDto.Id,
                    IngredientId = i.IngredientId,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Notes = i.Notes,
                    Order = i.Order
                }).ToList();

                await _localDb.SaveRecipeIngredientsAsync(recipeDto.Id, recipeIngredients).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply server recipe {RecipeId}", recipeDto.Id);
                failedRecipes++;
            }
        }

        // Plany posiłków
        foreach (var mealPlanDto in response.MealPlans)
        {
            try
            {
                await _localDb.ApplyServerMealPlanAsync(new MealPlanLocal
                {
                    Id = mealPlanDto.Id,
                    Date = mealPlanDto.Date,
                    StartHour = mealPlanDto.StartHour,
                    DurationMinutes = mealPlanDto.DurationMinutes,
                    RecipeId = mealPlanDto.RecipeId,
                    Notes = mealPlanDto.Notes,
                    CreatedAt = mealPlanDto.CreatedAt,
                    UpdatedAt = mealPlanDto.UpdatedAt,
                    IsDeleted = mealPlanDto.IsDeleted
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply server meal plan {MealPlanId}", mealPlanDto.Id);
                failedMealPlans++;
            }
        }

        var result = new ApplyResult(failedCategories, failedIngredients, failedRecipes, failedMealPlans);

        if (result.TotalFailed > 0)
        {
            _logger.LogWarning(
                "ApplyServerChanges completed with {TotalFailed} failed items (categories: {C}, ingredients: {I}, recipes: {R}, mealPlans: {M})",
                result.TotalFailed, failedCategories, failedIngredients, failedRecipes, failedMealPlans);
        }

        return result;
    }
}

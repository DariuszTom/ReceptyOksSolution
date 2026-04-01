using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using ReceptyOks.Data;
using ReceptyOks.Shared;
using ReceptyOks.Shared.DTOs;
using System.Net.Http.Json;

namespace ReceptyOks.Services;

public class SyncService
{
    private readonly LocalDatabase _localDb;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SyncService> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy = Policy
        .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

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

            var response = await _retryPolicy.ExecuteAsync(() =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync")
                {
                    Content = JsonContent.Create(request)
                };

                httpRequest.Headers.Add(GlobalConstants.ApiKeyHeaderName, "your-api-key");

                return _httpClient.SendAsync(httpRequest);
            });

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
            await ApplyServerChangesAsync(syncResponse);

            // Wyczyść flagi dirty
            await _localDb.ClearDirtyFlagsAsync();

            // Zapisz czas synchronizacji
            await _localDb.SetLastSyncTimeAsync(syncResponse.SyncedAt);

            result.Success = true;
            result.Message = "Synchronizacja zakończona pomyślnie";
            result.RecipesSynced = syncResponse.Recipes.Count;
            result.CategoriesSynced = syncResponse.Categories.Count;
            result.IngredientsSynced = syncResponse.Ingredients.Count;
            result.MealPlansSynced = syncResponse.MealPlans.Count;

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

            var response = await _httpClient.GetAsync("/api/sync/full");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Full sync failed with status code {StatusCode}", response.StatusCode);
                result.Success = false;
                result.Message = $"Błąd serwera: {response.StatusCode}";
                return result;
            }

            var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>();

            if (syncResponse is null)
            {
                _logger.LogError("Full sync failed: server returned null response");
                result.Success = false;
                result.Message = "Pusta odpowiedź serwera";
                return result;
            }

            await ApplyServerChangesAsync(syncResponse);
            await _localDb.SetLastSyncTimeAsync(syncResponse.SyncedAt);

            result.Success = true;
            result.Message = "Pełna synchronizacja zakończona";
            result.RecipesSynced = syncResponse.Recipes.Count;
            result.CategoriesSynced = syncResponse.Categories.Count;
            result.IngredientsSynced = syncResponse.Ingredients.Count;
            result.MealPlansSynced = syncResponse.MealPlans.Count;

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

            var allRecipes = await GetAllRecipesForUploadAsync();
            var allCategories = await GetAllCategoriesForUploadAsync();
            var allIngredients = await GetAllIngredientsForUploadAsync();
            var allMealPlans = await GetAllMealPlansForUploadAsync();

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

                var response = await _retryPolicy.ExecuteAsync(ct =>
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync/upload-all")
                    {
                        Content = JsonContent.Create(request),
                    };

                    httpRequest.Headers.Add(GlobalConstants.ApiKeyHeaderName, "your-api-key");

                    return _httpClient.SendAsync(httpRequest, ct);
                }, timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Upload-all batch {Batch} failed with status code {StatusCode}", i + 1, response.StatusCode);
                    result.Success = false;
                    result.Message = $"Błąd serwera (partia {i + 1}): {response.StatusCode}";
                    return result;
                }

                var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>();

                if (syncResponse is null)
                {
                    _logger.LogError("Upload-all batch {Batch} failed: server returned null response", i + 1);
                    result.Success = false;
                    result.Message = "Pusta odpowiedź serwera";
                    return result;
                }

                lastSyncedAt = syncResponse.SyncedAt;
            }

            await _localDb.ClearDirtyFlagsAsync();
            await _localDb.SetLastSyncTimeAsync(lastSyncedAt);

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
        var recipes = await _localDb.GetRecipesAsync();
        return await MapRecipesToSyncDtosAsync(recipes);
    }

    private async Task<List<CategorySyncDto>> GetAllCategoriesForUploadAsync()
    {
        var categories = await _localDb.GetCategoriesAsync();
        return MapCategoriesToSyncDtos(categories);
    }

    private async Task<List<IngredientSyncDto>> GetAllIngredientsForUploadAsync()
    {
        var ingredients = await _localDb.GetIngredientsAsync();
        return MapIngredientsToSyncDtos(ingredients);
    }

    private async Task<List<RecipeSyncDto>> GetChangedRecipesAsync()
    {
        var dirtyRecipes = await _localDb.GetDirtyRecipesAsync();
        return await MapRecipesToSyncDtosAsync(dirtyRecipes);
    }

    private async Task<List<CategorySyncDto>> GetChangedCategoriesAsync()
    {
        var dirtyCategories = await _localDb.GetDirtyCategoriesAsync();
        return MapCategoriesToSyncDtos(dirtyCategories);
    }

    private async Task<List<IngredientSyncDto>> GetChangedIngredientsAsync()
    {
        var dirtyIngredients = await _localDb.GetDirtyIngredientsAsync();
        return MapIngredientsToSyncDtos(dirtyIngredients);
    }

    private async Task<List<MealPlanSyncDto>> GetChangedMealPlansAsync()
    {
        var dirtyMealPlans = await _localDb.GetDirtyMealPlansAsync();
        return MapMealPlansToSyncDtos(dirtyMealPlans);
    }

    private async Task<List<MealPlanSyncDto>> GetAllMealPlansForUploadAsync()
    {
        var mealPlans = await _localDb.GetAllMealPlansAsync();
        return MapMealPlansToSyncDtos(mealPlans);
    }

    private async Task<List<RecipeSyncDto>> MapRecipesToSyncDtosAsync(List<RecipeLocal> recipes)
    {
        var result = new List<RecipeSyncDto>();
        foreach (var recipe in recipes)
        {
            var ingredients = await _localDb.GetRecipeIngredientsAsync(recipe.Id);
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

    private async Task ApplyServerChangesAsync(SyncResponse response)
    {
        // Kategorie
        foreach (var categoryDto in response.Categories)
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
            });
        }

        // Składniki
        foreach (var ingredientDto in response.Ingredients)
        {
            await _localDb.ApplyServerIngredientAsync(new IngredientLocal
            {
                Id = ingredientDto.Id,
                Name = ingredientDto.Name,
                Unit = ingredientDto.Unit,
                CreatedAt = ingredientDto.CreatedAt,
                UpdatedAt = ingredientDto.UpdatedAt,
                IsDeleted = ingredientDto.IsDeleted
            });
        }

        // Przepisy
        foreach (var recipeDto in response.Recipes)
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
            });

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

            await _localDb.SaveRecipeIngredientsAsync(recipeDto.Id, recipeIngredients);
        }

        // Plany posiłków
        foreach (var mealPlanDto in response.MealPlans)
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
            });
        }
    }
}

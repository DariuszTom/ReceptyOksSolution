using System.Net.Http.Json;
using ReceptyOks.Data;
using ReceptyOks.Shared.DTOs;

namespace ReceptyOks.Services;

public class SyncService
{
    private readonly LocalDatabase _localDb;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public SyncService(LocalDatabase localDb, HttpClient httpClient)
    {
        _localDb = localDb;
        _httpClient = httpClient;
        // URL bêdzie konfigurowany przez Aspire service discovery lub lokalnie
        _baseUrl = "http://localhost:5100";
    }

    public async Task<SyncResult> SyncAsync()
    {
        var result = new SyncResult();

        try
        {
            // SprawdŸ po³¹czenie
            var connectivity = Connectivity.Current.NetworkAccess;
            if (connectivity != NetworkAccess.Internet)
            {
                result.Success = false;
                result.Message = "Brak po³¹czenia z internetem";
                return result;
            }

            var lastSync = await _localDb.GetLastSyncTimeAsync();

            // Pobierz lokalne zmiany do wys³ania
            var request = new SyncRequest
            {
                LastSyncedAt = lastSync,
                ChangedRecipes = await GetChangedRecipesAsync(),
                ChangedCategories = await GetChangedCategoriesAsync(),
                ChangedIngredients = await GetChangedIngredientsAsync()
            };

            // Wyœlij do serwera i pobierz odpowiedŸ
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/sync", request);
            
            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.Message = $"B³¹d serwera: {response.StatusCode}";
                return result;
            }

            var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>();
            
            if (syncResponse is null)
            {
                result.Success = false;
                result.Message = "Pusta odpowiedŸ serwera";
                return result;
            }

            // Zastosuj zmiany z serwera lokalnie
            await ApplyServerChangesAsync(syncResponse);

            // Wyczyœæ flagi dirty
            await _localDb.ClearDirtyFlagsAsync();

            // Zapisz czas synchronizacji
            await _localDb.SetLastSyncTimeAsync(syncResponse.SyncedAt);

            result.Success = true;
            result.Message = "Synchronizacja zakoñczona pomyœlnie";
            result.RecipesSynced = syncResponse.Recipes.Count;
            result.CategoriesSynced = syncResponse.Categories.Count;
            result.IngredientsSynced = syncResponse.Ingredients.Count;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"B³¹d synchronizacji: {ex.Message}";
        }

        return result;
    }

    public async Task<SyncResult> FullSyncAsync()
    {
        var result = new SyncResult();

        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sync/full");
            
            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.Message = $"B³¹d serwera: {response.StatusCode}";
                return result;
            }

            var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>();
            
            if (syncResponse is null)
            {
                result.Success = false;
                result.Message = "Pusta odpowiedŸ serwera";
                return result;
            }

            await ApplyServerChangesAsync(syncResponse);
            await _localDb.SetLastSyncTimeAsync(syncResponse.SyncedAt);

            result.Success = true;
            result.Message = "Pe³na synchronizacja zakoñczona";
            result.RecipesSynced = syncResponse.Recipes.Count;
            result.CategoriesSynced = syncResponse.Categories.Count;
            result.IngredientsSynced = syncResponse.Ingredients.Count;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"B³¹d: {ex.Message}";
        }

        return result;
    }

    private async Task<List<RecipeSyncDto>> GetChangedRecipesAsync()
    {
        var dirtyRecipes = await _localDb.GetDirtyRecipesAsync();
        var result = new List<RecipeSyncDto>();

        foreach (var recipe in dirtyRecipes)
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

    private async Task<List<CategorySyncDto>> GetChangedCategoriesAsync()
    {
        var dirtyCategories = await _localDb.GetDirtyCategoriesAsync();
        return dirtyCategories.Select(c => new CategorySyncDto
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

    private async Task<List<IngredientSyncDto>> GetChangedIngredientsAsync()
    {
        var dirtyIngredients = await _localDb.GetDirtyIngredientsAsync();
        return dirtyIngredients.Select(i => new IngredientSyncDto
        {
            Id = i.Id,
            Name = i.Name,
            Unit = i.Unit,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            IsDeleted = i.IsDeleted
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

        // Sk³adniki
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

            // Sk³adniki przepisu
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
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RecipesSynced { get; set; }
    public int CategoriesSynced { get; set; }
    public int IngredientsSynced { get; set; }
}

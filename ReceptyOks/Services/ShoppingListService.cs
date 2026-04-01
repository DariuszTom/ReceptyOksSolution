using Polly;
using Polly.Retry;
using ReceptyOks.Misc;
using ReceptyOks.Shared.DTOs;
using ReceptyOks.Shared.Models;
using System.Net.Http.Json;

namespace ReceptyOks.Services;

/// <summary>
/// Service for consuming shopping list API endpoints.
/// </summary>
public class ShoppingListService(HttpClient httpClient) : IShoppingListService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && r.StatusCode != System.Net.HttpStatusCode.NotFound)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    /// <summary>
    /// Gets all shopping list items.
    /// </summary>
    /// <param name="includeBought">Whether to include bought items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of shopping list items.</returns>
    public async Task<ShoppingListResult<List<ShoppingListItem>>> GetAllAsync(bool includeBought = false, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<List<ShoppingListItem>>.Failure("Brak połączenia z internetem");
            }

            var url = includeBought ? "/api/shopping-list?includeBought=true" : "/api/shopping-list";
            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.GetAsync(url, ct), cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<List<ShoppingListItem>>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var items = await response.Content.ReadFromJsonAsync<List<ShoppingListItem>>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return ShoppingListResult<List<ShoppingListItem>>.Success(items ?? []);
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<List<ShoppingListItem>>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<List<ShoppingListItem>>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a single shopping list item by ID.
    /// </summary>
    public async Task<ShoppingListResult<ShoppingListItem>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.GetAsync($"/api/shopping-list/{id}", ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Element nie został znaleziony");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<ShoppingListItem>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var item = await response.Content.ReadFromJsonAsync<ShoppingListItem>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return item is not null
                ? ShoppingListResult<ShoppingListItem>.Success(item)
                : ShoppingListResult<ShoppingListItem>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<ShoppingListItem>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<ShoppingListItem>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a new shopping list item.
    /// </summary>
    public async Task<ShoppingListResult<ShoppingListItem>> AddAsync(ShoppingListItem item, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.PostAsJsonAsync("/api/shopping-list", item, ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                return ShoppingListResult<ShoppingListItem>.Failure(error?.Error ?? "Nieprawidłowe dane");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<ShoppingListItem>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var createdItem = await response.Content.ReadFromJsonAsync<ShoppingListItem>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return createdItem is not null
                ? ShoppingListResult<ShoppingListItem>.Success(createdItem)
                : ShoppingListResult<ShoppingListItem>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<ShoppingListItem>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<ShoppingListItem>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds multiple shopping list items at once.
    /// </summary>
    public async Task<ShoppingListResult<List<ShoppingListItem>>> AddBulkAsync(List<ShoppingListItem> items, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<List<ShoppingListItem>>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.PostAsJsonAsync("/api/shopping-list/bulk", items, ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                return ShoppingListResult<List<ShoppingListItem>>.Failure(error?.Error ?? "Nieprawidłowe dane");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<List<ShoppingListItem>>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var createdItems = await response.Content.ReadFromJsonAsync<List<ShoppingListItem>>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return ShoppingListResult<List<ShoppingListItem>>.Success(createdItems ?? []);
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<List<ShoppingListItem>>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<List<ShoppingListItem>>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing shopping list item.
    /// </summary>
    public async Task<ShoppingListResult<ShoppingListItem>> UpdateAsync(Guid id, ShoppingListItem item, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.PutAsJsonAsync($"/api/shopping-list/{id}", item, ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Element nie został znaleziony");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                return ShoppingListResult<ShoppingListItem>.Failure(error?.Error ?? "Nieprawidłowe dane");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<ShoppingListItem>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var updatedItem = await response.Content.ReadFromJsonAsync<ShoppingListItem>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return updatedItem is not null
                ? ShoppingListResult<ShoppingListItem>.Success(updatedItem)
                : ShoppingListResult<ShoppingListItem>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<ShoppingListItem>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<ShoppingListItem>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks an item as bought.
    /// </summary>
    public async Task<ShoppingListResult<ShoppingListItem>> MarkAsBoughtAsync(Guid id, string? boughtBy = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Brak połączenia z internetem");
            }

            var request = new BoughtRequest(boughtBy);
            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.PatchAsJsonAsync($"/api/shopping-list/{id}/bought", request, ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Element nie został znaleziony");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<ShoppingListItem>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var item = await response.Content.ReadFromJsonAsync<ShoppingListItem>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return item is not null
                ? ShoppingListResult<ShoppingListItem>.Success(item)
                : ShoppingListResult<ShoppingListItem>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<ShoppingListItem>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<ShoppingListItem>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Unmarks an item as bought.
    /// </summary>
    public async Task<ShoppingListResult<ShoppingListItem>> MarkAsUnboughtAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.PatchAsync($"/api/shopping-list/{id}/unbought", null, ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ShoppingListResult<ShoppingListItem>.Failure("Element nie został znaleziony");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<ShoppingListItem>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var item = await response.Content.ReadFromJsonAsync<ShoppingListItem>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return item is not null
                ? ShoppingListResult<ShoppingListItem>.Success(item)
                : ShoppingListResult<ShoppingListItem>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<ShoppingListItem>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<ShoppingListItem>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks multiple items as bought.
    /// </summary>
    public async Task<ShoppingListResult<BulkOperationResponse>> MarkMultipleAsBoughtAsync(List<Guid> ids, string? boughtBy = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<BulkOperationResponse>.Failure("Brak połączenia z internetem");
            }

            var request = new BulkBoughtRequest(ids, boughtBy);
            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.PatchAsJsonAsync("/api/shopping-list/bulk/bought", request, ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                return ShoppingListResult<BulkOperationResponse>.Failure(error?.Error ?? "Nieprawidłowe dane");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<BulkOperationResponse>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return result is not null
                ? ShoppingListResult<BulkOperationResponse>.Success(result)
                : ShoppingListResult<BulkOperationResponse>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<BulkOperationResponse>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<BulkOperationResponse>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a shopping list item (soft delete).
    /// </summary>
    public async Task<ShoppingListResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<bool>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.DeleteAsync($"/api/shopping-list/{id}", ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ShoppingListResult<bool>.Failure("Element nie został znaleziony");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<bool>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            return ShoppingListResult<bool>.Success(true);
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<bool>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<bool>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all bought items from the list.
    /// </summary>
    public async Task<ShoppingListResult<BulkOperationResponse>> ClearBoughtAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<BulkOperationResponse>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.DeleteAsync("/api/shopping-list/clear-bought", ct), cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<BulkOperationResponse>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return result is not null
                ? ShoppingListResult<BulkOperationResponse>.Success(result)
                : ShoppingListResult<BulkOperationResponse>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<BulkOperationResponse>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<BulkOperationResponse>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all items from the list.
    /// </summary>
    public async Task<ShoppingListResult<BulkOperationResponse>> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<BulkOperationResponse>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.DeleteAsync("/api/shopping-list/clear-all", ct), cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<BulkOperationResponse>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<BulkOperationResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return result is not null
                ? ShoppingListResult<BulkOperationResponse>.Success(result)
                : ShoppingListResult<BulkOperationResponse>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<BulkOperationResponse>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<BulkOperationResponse>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets shopping list statistics.
    /// </summary>
    public async Task<ShoppingListResult<ShoppingListStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<ShoppingListStats>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.GetAsync("/api/shopping-list/stats", ct), cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<ShoppingListStats>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            var stats = await response.Content.ReadFromJsonAsync<ShoppingListStats>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return stats is not null
                ? ShoppingListResult<ShoppingListStats>.Success(stats)
                : ShoppingListResult<ShoppingListStats>.Failure("Pusta odpowiedź serwera");
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<ShoppingListStats>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<ShoppingListStats>.Failure($"Błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Permanently deletes an item from the database (hard delete).
    /// </summary>
    public async Task<ShoppingListResult<bool>> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!HasInternetConnection())
            {
                return ShoppingListResult<bool>.Failure("Brak połączenia z internetem");
            }

            var response = await _retryPolicy.ExecuteAsync(ct =>
                _httpClient.DeleteAsync($"/api/shopping-list/{id}/permanent", ct), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ShoppingListResult<bool>.Failure("Element nie został znaleziony");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShoppingListResult<bool>.Failure($"Błąd serwera: {response.StatusCode}");
            }

            return ShoppingListResult<bool>.Success(true);
        }
        catch (OperationCanceledException)
        {
            return ShoppingListResult<bool>.Failure("Operacja została anulowana");
        }
        catch (Exception ex)
        {
            return ShoppingListResult<bool>.Failure($"Błąd: {ex.Message}");
        }
    }

    private static bool HasInternetConnection()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }
}

/// <summary>
/// Error response from API.
/// </summary>
internal record ErrorResponse(string? Error);

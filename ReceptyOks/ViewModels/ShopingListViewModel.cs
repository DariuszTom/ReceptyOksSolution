using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using ReceptyOks.Misc;
using ReceptyOks.Models;
using ReceptyOks.Services;
using ReceptyOks.Shared;
using ReceptyOks.Shared.Models;
using System.Collections.ObjectModel;
using UraniumUI.Extensions;

namespace ReceptyOks.ViewModels;

/// <summary>
/// Message to pass shopping list items to the ShopingListViewModel.
/// </summary>
public sealed class AddShoppingItemsMessage(List<ShoppingListItem> items)
{
    public List<ShoppingListItem> Items { get; } = items;
}

public partial class ShopingListViewModel : ObservableObject
{
    private readonly IShoppingListService _shoppingListService;
    private readonly UserService _userService;
    private readonly LocalDatabase _database;
    private readonly ILogger<ShopingListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ShoppingListItem> items = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool includeBoughtItems;

    [ObservableProperty]
    private string newItemName = string.Empty;

    [ObservableProperty]
    private string newItemQuantity = string.Empty;

    [ObservableProperty]
    private Jednostki selectedUnit = Jednostki.Brak;

    /// <summary>
    /// Available units for the picker.
    /// </summary>
    public IReadOnlyList<Jednostki> AvailableUnits { get; } = Enum.GetValues(typeof(Jednostki)).Cast<Jednostki>().ToList();

    /// <summary>
    /// Ingredient name suggestions from local database for autocomplete.
    /// </summary>
    [ObservableProperty]
    private IEnumerable<string> ingredientSuggestions = [];

    public ShopingListViewModel(IShoppingListService shoppingListService, LocalDatabase database, ILogger<ShopingListViewModel> logger)
    {
        _shoppingListService = shoppingListService;
        _database = database;
        _logger = logger;
        _userService = UserService.Instance.Value;
        // Register to receive shopping items from other ViewModels
        WeakReferenceMessenger.Default.Register<AddShoppingItemsMessage>(this, async (r, m) =>
              {
                  await ((ShopingListViewModel)r).AddItemsFromMessageAsync(m.Items);
              });
        LoadItemsAsync().FireAndForget();
        LoadIngredientSuggestionsAsync().FireAndForget();
    }

    /// <summary>
    /// Loads ingredient names from local database for autocomplete suggestions.
    /// </summary>
    private async Task LoadIngredientSuggestionsAsync()
    {
        try
        {
            var ingredients = await _database.GetIngredientsAsync();
            IngredientSuggestions = [.. ingredients.Select(i => i.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ingredient suggestions");
        }
    }

    /// <summary>
    /// Adds multiple items received from another ViewModel.
    /// </summary>
    private async Task AddItemsFromMessageAsync(List<ShoppingListItem> itemsToAdd)
    {
        if (itemsToAdd.Count == 0) return;

        try
        {
            IsLoading = true;

            var result = await _shoppingListService.AddBulkAsync(itemsToAdd);

            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var item in result.Data)
                {
                    Items.Add(item);
                }
                UpdateBadgeCount();
            }
            else
            {
                _logger.LogWarning("Failed to add bulk items: {Error}", result.ErrorMessage);
                await SnackBarHelper.ShowErrorSnackbarAsync(result.ErrorMessage ?? "Nie udało się dodać produktów");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bulk shopping list items");
            await SnackBarHelper.ShowErrorSnackbarAsync("Wystąpił błąd podczas dodawania produktów");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads all shopping list items from the backend.
    /// </summary>
    [RelayCommand]
    private async Task LoadItemsAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            IsLoading = true;

            var result = await _shoppingListService.GetAllAsync(IncludeBoughtItems, cancellationToken);

            if (result.IsSuccess)
            {
                Items = new ObservableCollection<ShoppingListItem>(result.Data ?? []);
                UpdateBadgeCount();
            }
            else
            {
                _logger.LogWarning("Failed to load shopping list: {Error}", result.ErrorMessage);
                await SnackBarHelper.ShowErrorSnackbarAsync(result.ErrorMessage ?? "Nie udało się załadować listy");
            }
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled (e.g., user triggered refresh again), ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading shopping list items");
            await SnackBarHelper.ShowErrorSnackbarAsync("Wystąpił błąd podczas ładowania listy");
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Refreshes the shopping list (pull-to-refresh).
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsRefreshing = true;
        await LoadItemsAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new item to the shopping list.
    /// </summary>
    [RelayCommand]
    private async Task AddItemAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(NewItemName))
        {
            await SnackBarHelper.ShowErrorSnackbarAsync("Nazwa produktu jest wymagana");
            return;
        }

        try
        {
            IsLoading = true;

            decimal? parsedQuantity = null;
            if (!string.IsNullOrWhiteSpace(NewItemQuantity) && decimal.TryParse(NewItemQuantity, out var qty))
            {
                parsedQuantity = qty;
            }

            var user = await _userService.GetUserAsync();

            var newItem = new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                Name = NewItemName.Trim(),
                Quantity = parsedQuantity,
                BoughtBy = user?.Name,
                Unit = SelectedUnit == Jednostki.Brak ? null : SelectedUnit.ToString(),
                IsBought = false
            };

            var result = await _shoppingListService.AddAsync(newItem, cancellationToken);

            if (result.IsSuccess && result.Data is not null)
            {
                Items.Add(result.Data);
                UpdateBadgeCount();
                ClearNewItemForm();
                _logger.LogInformation("Added shopping list item: {Name}", result.Data.Name);
            }
            else
            {
                _logger.LogWarning("Failed to add shopping list item: {Error}", result.ErrorMessage);
                await SnackBarHelper.ShowErrorSnackbarAsync(result.ErrorMessage ?? "Nie udało się dodać produktu");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding shopping list item");
            await SnackBarHelper.ShowErrorSnackbarAsync("Wystąpił błąd podczas dodawania produktu");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Toggles the bought status of an item.
    /// </summary>
    [RelayCommand]
    private async Task ToggleBoughtAsync(ShoppingListItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            ShoppingListResult<ShoppingListItem> result;
            if (item.IsBought)
            {
                result = await _shoppingListService.MarkAsUnboughtAsync(item.Id, cancellationToken);
            }
            else
            {
                var user = await _userService.GetUserAsync();
                var boughtBy = user?.Name;
                result = await _shoppingListService.MarkAsBoughtAsync(item.Id, boughtBy, cancellationToken);
            }

            if (result.IsSuccess && result.Data is not null)
            {
                var index = Items.IndexOf(item);
                if (index >= 0)
                {
                    // Use RemoveAt/Insert to properly trigger UI update
                    Items.RemoveAt(index);
                    Items.Insert(index, result.Data);
                }
                UpdateBadgeCount();
            }
            else
            {
                _logger.LogWarning("Failed to toggle bought status: {Error}", result.ErrorMessage);
                await SnackBarHelper.ShowErrorSnackbarAsync(result.ErrorMessage ?? "Nie udało się zmienić statusu produktu");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling bought status for item {Id}", item.Id);
            await SnackBarHelper.ShowErrorSnackbarAsync("Wystąpił błąd podczas aktualizacji produktu");
        }
    }

    /// <summary>
    /// Deletes an item from the shopping list (soft delete).
    /// </summary>
    [RelayCommand]
    private async Task DeleteItemAsync(ShoppingListItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            var result = await _shoppingListService.DeleteAsync(item.Id, cancellationToken);

            if (result.IsSuccess)
            {
                Items.Remove(item);
                UpdateBadgeCount();
            }
            else
            {
                _logger.LogWarning("Failed to delete shopping list item: {Error}", result.ErrorMessage);
                await SnackBarHelper.ShowErrorSnackbarAsync(result.ErrorMessage ?? "Nie udało się usunąć produktu");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shopping list item {Id}", item.Id);
            await SnackBarHelper.ShowErrorSnackbarAsync("Wystąpił błąd podczas usuwania produktu");
        }
    }

    /// <summary>
    /// Permanently deletes an item from the database (hard delete).
    /// </summary>
    [RelayCommand]
    private async Task HardDeleteItemAsync(ShoppingListItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            // Show confirmation dialog
            bool confirm = await Shell.Current.DisplayAlertAsync("Usunąć na zawsze?", $"Element '{item.Name}' zostanie trwale usunięty z bazy danych. Tej operacji nie można cofnąć.",
           "Usuń",
          "Anuluj");

            if (!confirm) return;

            var result = await _shoppingListService.HardDeleteAsync(item.Id, cancellationToken);

            if (result.IsSuccess)
            {
                Items.Remove(item);
                UpdateBadgeCount();
            }
            else
            {
                _logger.LogWarning("Failed to hard delete shopping list item: {Error}", result.ErrorMessage);
                await SnackBarHelper.ShowErrorSnackbarAsync(result.ErrorMessage ?? "Nie udało się trwale usunąć produktu");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hard deleting shopping list item {Id}", item.Id);
            await SnackBarHelper.ShowErrorSnackbarAsync("Wystąpił błąd podczas trwałego usuwania produktu");
        }
    }

    /// <summary>
    /// Clears all bought items from the list.
    /// </summary>
    [RelayCommand]
    private async Task ClearBoughtItemsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Show confirmation dialog
            bool confirm = await Shell.Current.DisplayAlertAsync(
                "Usunąć na zawsze kupione produkty?",
                "Wszystkie kupione produkty zostaną trwale usunięte z bazy danych. Tej operacji nie można cofnąć.",
                "Usuń",
                "Anuluj");
            if (!confirm) return;
            IsLoading = true;

            var result = await _shoppingListService.ClearBoughtAsync(cancellationToken);

            if (result.IsSuccess)
            {
                await LoadItemsAsync(cancellationToken);
            }
            else
            {
                await SnackBarHelper.ShowErrorSnackbarAsync(result.ErrorMessage ?? "Nie udało się wyczyścić listy");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing bought items");
            await SnackBarHelper.ShowErrorSnackbarAsync("Wystąpił błąd podczas czyszczenia listy");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Generates formatted text for the shopping list (for copy/share).
    /// </summary>
    public string GetShoppingListText()
    {
        if (Items.Count == 0)
        {
            return "Lista zakupów jest pusta";
        }

        var lines = Items
            .Where(i => !i.IsBought)
            .Select(i =>
            {
                var quantityText = i.Quantity.HasValue
                    ? $"{i.Quantity} {i.Unit ?? ""}".Trim()
                    : string.Empty;

                return string.IsNullOrEmpty(quantityText)
                    ? $"• {i.Name}"
                    : $"• {i.Name} - {quantityText}";
            });

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Copies the shopping list to clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyListAsync()
    {
        var text = GetShoppingListText();
        await Clipboard.Default.SetTextAsync(text);
        await SnackBarHelper.ShowInfoSnackbarAsync("Skopiowano do schowka");
    }

    partial void OnIncludeBoughtItemsChanged(bool value)
    {
        LoadItemsCommand.Execute(default(CancellationToken));
    }

    private void ClearNewItemForm()
    {
        NewItemName = string.Empty;
        NewItemQuantity = string.Empty;
        SelectedUnit = Jednostki.Brak;
    }

    /// <summary>
    /// Updates the app badge with the count of unbought items.
    /// </summary>
    private void UpdateBadgeCount()
    {
        var unboughtCount = (uint)Items.Count(i => !i.IsBought);
        WeakReferenceMessenger.Default.Send(new BadgeCountMessage(unboughtCount));
    }

}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ReceptyOks.Services;
using ReceptyOks.Shared;
using ReceptyOks.Shared.Models;
using System.Collections.ObjectModel;

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
    private readonly ShoppingListService _shoppingListService;
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
    private string? errorMessage;

    [ObservableProperty]
    private string newItemName = string.Empty;

    [ObservableProperty]
    private decimal? newItemQuantity;

    [ObservableProperty]
    private Jednostki selectedUnit = Jednostki.Brak;

    /// <summary>
    /// Available units for the picker.
    /// </summary>
    public IReadOnlyList<Jednostki> AvailableUnits { get; } = Enum.GetValues(typeof(Jednostki)).Cast<Jednostki>().ToList();

    public ShopingListViewModel(ShoppingListService shoppingListService, ILogger<ShopingListViewModel> logger)
    {
        _shoppingListService = shoppingListService;
        _logger = logger;

        // Register to receive shopping items from other ViewModels
        WeakReferenceMessenger.Default.Register<AddShoppingItemsMessage>(this, async (r, m) =>
        {
            await ((ShopingListViewModel)r).AddItemsFromMessageAsync(m.Items);
        });
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
            ErrorMessage = null;

            var result = await _shoppingListService.AddBulkAsync(itemsToAdd);

            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var item in result.Data)
                {
                    Items.Add(item);
                }
                _logger.LogInformation("Added {Count} items to shopping list", result.Data.Count);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                _logger.LogWarning("Failed to add bulk items: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Wystąpił błąd podczas dodawania produktów";
            _logger.LogError(ex, "Error adding bulk shopping list items");
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
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var result = await _shoppingListService.GetAllAsync(IncludeBoughtItems, cancellationToken);

            if (result.IsSuccess)
            {
                Items = new ObservableCollection<ShoppingListItem>(result.Data ?? []);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                _logger.LogWarning("Failed to load shopping list: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Wystąpił błąd podczas ładowania listy";
            _logger.LogError(ex, "Error loading shopping list items");
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
            ErrorMessage = "Nazwa produktu jest wymagana";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var newItem = new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                Name = NewItemName.Trim(),
                Quantity = NewItemQuantity,
                Unit = SelectedUnit == Jednostki.Brak ? null : SelectedUnit.ToString(),
                IsBought = false
            };

            var result = await _shoppingListService.AddAsync(newItem, cancellationToken);

            if (result.IsSuccess && result.Data is not null)
            {
                Items.Add(result.Data);
                ClearNewItemForm();
                _logger.LogInformation("Added shopping list item: {Name}", result.Data.Name);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                _logger.LogWarning("Failed to add shopping list item: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Wystąpił błąd podczas dodawania produktu";
            _logger.LogError(ex, "Error adding shopping list item");
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
            ErrorMessage = null;

            var result = item.IsBought
                ? await _shoppingListService.MarkAsUnboughtAsync(item.Id, cancellationToken)
                : await _shoppingListService.MarkAsBoughtAsync(item.Id, cancellationToken: cancellationToken);

            if (result.IsSuccess && result.Data is not null)
            {
                var index = Items.IndexOf(item);
                if (index >= 0)
                {
                    Items[index] = result.Data;
                }
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                _logger.LogWarning("Failed to toggle bought status: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Wystąpił błąd podczas aktualizacji produktu";
            _logger.LogError(ex, "Error toggling bought status for item {Id}", item.Id);
        }
    }

    /// <summary>
    /// Deletes an item from the shopping list.
    /// </summary>
    [RelayCommand]
    private async Task DeleteItemAsync(ShoppingListItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            ErrorMessage = null;

            var result = await _shoppingListService.DeleteAsync(item.Id, cancellationToken);

            if (result.IsSuccess)
            {
                Items.Remove(item);
                _logger.LogInformation("Deleted shopping list item: {Name}", item.Name);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                _logger.LogWarning("Failed to delete shopping list item: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Wystąpił błąd podczas usuwania produktu";
            _logger.LogError(ex, "Error deleting shopping list item {Id}", item.Id);
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
            IsLoading = true;
            ErrorMessage = null;

            var result = await _shoppingListService.ClearBoughtAsync(cancellationToken);

            if (result.IsSuccess)
            {
                await LoadItemsAsync(cancellationToken);
                _logger.LogInformation("Cleared bought items from shopping list");
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                _logger.LogWarning("Failed to clear bought items: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Wystąpił błąd podczas czyszczenia listy";
            _logger.LogError(ex, "Error clearing bought items");
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
        _logger.LogInformation("Shopping list copied to clipboard");
    }

    partial void OnIncludeBoughtItemsChanged(bool value)
    {
        LoadItemsCommand.Execute(default(CancellationToken));
    }

    private void ClearNewItemForm()
    {
        NewItemName = string.Empty;
        NewItemQuantity = null;
        SelectedUnit = Jednostki.Brak;
    }
}

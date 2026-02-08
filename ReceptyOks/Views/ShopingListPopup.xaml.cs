using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using ReceptyOks.Services;
using ReceptyOks.Shared.Models;
using ReceptyOks.ViewModels;
using System.Collections.ObjectModel;

namespace ReceptyOks.Views;

/// <summary>
/// Popup wyświetlający wygenerowaną listę zakupów z opcją kopiowania.
/// </summary>
public partial class ShopingListPopup : ContentView
{
    public static readonly BindableProperty ShoppingListTextProperty =
        BindableProperty.Create(
            nameof(ShoppingListText),
            typeof(string),
            typeof(ShopingListPopup),
            string.Empty);

    public static readonly BindableProperty ShoppingListItemsProperty =
        BindableProperty.Create(
            nameof(ShoppingListItems),
            typeof(ObservableCollection<ShoppingListItemDto>),
            typeof(ShopingListPopup),
            new ObservableCollection<ShoppingListItemDto>());

    public string ShoppingListText
    {
        get => (string)GetValue(ShoppingListTextProperty);
        set => SetValue(ShoppingListTextProperty, value);
    }

    /// <summary>
    /// Structured shopping list items that can be used for further processing or display.
    /// </summary>
    public ObservableCollection<ShoppingListItemDto> ShoppingListItems
    {
        get => (ObservableCollection<ShoppingListItemDto>)GetValue(ShoppingListItemsProperty);
        set => SetValue(ShoppingListItemsProperty, value);
    }

    public ShopingListPopup()
    {
        InitializeComponent();
    }

    public ShopingListPopup(string shoppingListText) : this()
    {
        ShoppingListText = shoppingListText;
    }

    public ShopingListPopup(string shoppingListText, IEnumerable<ShoppingListItemDto> items) : this()
    {
        ShoppingListText = shoppingListText;
        ShoppingListItems = new ObservableCollection<ShoppingListItemDto>(items);
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        // Copy formatted list with items
        var textToCopy = FormatShoppingListForClipboard();
        await Clipboard.Default.SetTextAsync(textToCopy);
        await SnackBarHelper.ShowInfoSnackbarAsync("Skopiowano do schowka");
    }

    private string FormatShoppingListForClipboard()
    {
        if (ShoppingListItems.Count == 0)
            return ShoppingListText;

        var lines = new List<string> { ShoppingListText, string.Empty, "Lista zakupów:" };
        foreach (var item in ShoppingListItems)
        {
            var quantity = item.Quantity.HasValue ? $"{item.Quantity}" : "";
            var unit = item.Unit.HasValue ? $" {item.Unit}" : "";
            var note = !string.IsNullOrWhiteSpace(item.Note) ? $" ({item.Note})" : "";
            lines.Add($"- {item.Name}: {quantity}{unit}{note}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        var page = Shell.Current.CurrentPage;
        await page.ClosePopupAsync(CancellationToken.None);
    }

    private async void OnAddToListClicked(object? sender, EventArgs e)
    {
        if (ShoppingListItems.Count == 0)
        {
            await SnackBarHelper.ShowInfoSnackbarAsync("Brak produktów do dodania");
            return;
        }

        var itemsToAdd = ShoppingListItems.Select(dto => dto.ToEntity()).ToList();
        WeakReferenceMessenger.Default.Send(new AddShoppingItemsMessage(itemsToAdd));

        await SnackBarHelper.ShowInfoSnackbarAsync($"Dodano {itemsToAdd.Count} produktów do listy zakupów");

        var page = Shell.Current.CurrentPage;
        await page.ClosePopupAsync(CancellationToken.None);
    }
}
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;

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

	public string ShoppingListText
	{
		get => (string)GetValue(ShoppingListTextProperty);
		set => SetValue(ShoppingListTextProperty, value);
	}

	public ShopingListPopup()
	{
		InitializeComponent();
	}

	public ShopingListPopup(string shoppingListText) : this()
	{
		ShoppingListText = shoppingListText;
	}

	private async void OnCopyClicked(object? sender, EventArgs e)
	{
		await Clipboard.Default.SetTextAsync(ShoppingListText);
		await Toast.Make("Skopiowano do schowka").Show();
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		var page = Shell.Current.CurrentPage;
		await page.ClosePopupAsync(CancellationToken.None);
	}
}
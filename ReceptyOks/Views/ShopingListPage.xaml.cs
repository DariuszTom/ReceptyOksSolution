
using ReceptyOks.Shared.Models;
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class ShopingListPage : ContentPage
{
	private readonly ShopingListViewModel _viewModel;
	private bool _isUpdatingFromCode;

	public ShopingListPage(ShopingListViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	private async void OnItemCheckedChanged(object? sender, CheckedChangedEventArgs e)
	{
		// Prevent re-entry when we update the checkbox from code
		if (_isUpdatingFromCode) return;

		if (sender is CheckBox checkBox && checkBox.BindingContext is ShoppingListItem item)
		{
			// The checkbox was clicked by user - determine desired state based on click
			// Since binding is OneWay, e.Value reflects what user wants
			bool wantsBought = e.Value;

			// Only call API if state actually differs
			if (item.IsBought != wantsBought)
			{
				if (_viewModel.ToggleBoughtCommand.CanExecute(item))
				{
					_viewModel.ToggleBoughtCommand.Execute(item);
				}
			}
			else
			{
				// User clicked but item already has that state - revert checkbox
				_isUpdatingFromCode = true;
				checkBox.IsChecked = item.IsBought;
				_isUpdatingFromCode = false;
			}
		}
	}
}
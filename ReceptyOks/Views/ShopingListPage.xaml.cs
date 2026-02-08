
using ReceptyOks.Shared.Models;
using ReceptyOks.ViewModels;
using InputKitCheckBox = InputKit.Shared.Controls.CheckBox;

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

    private void OnItemCheckChanged(object? sender, EventArgs e)
    {
        // Prevent re-entry when we update the checkbox from code
        if (_isUpdatingFromCode) return;

        if (sender is InputKitCheckBox checkBox && checkBox.BindingContext is ShoppingListItem item)
        {
            // The checkbox was clicked by user - determine desired state based on click
            // Since binding is OneWay, checkBox.IsChecked reflects what user wants
            bool wantsBought = checkBox.IsChecked;

            // Only call API if state actually differs
            if (item.IsBought != wantsBought)
            {
                if (_viewModel.ToggleBoughtCommand.CanExecute(item))
                {
                    _viewModel.ToggleBoughtCommand.Execute(item);
                }
            }

            _isUpdatingFromCode = true;
            checkBox.IsChecked = item.IsBought;
            _isUpdatingFromCode = false;
        }
    }
}

using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class ShopingListPage : ContentPage
{
	public ShopingListPage(ShopingListViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
    }
}
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class CategoriesPage : ContentPage
{
    public CategoriesPage(CategoriesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CategoriesViewModel vm)
        {
            vm.LoadCategoriesCommand.Execute(null);
        }
    }
}

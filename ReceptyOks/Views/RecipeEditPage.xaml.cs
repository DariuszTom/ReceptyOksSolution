using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipeEditPage : ContentPage
{
    public RecipeEditPage(RecipeEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RecipeEditViewModel vm && string.IsNullOrEmpty(vm.RecipeIdParam))
        {
            vm.InitializeCommand.Execute(null);
        }
    }
}

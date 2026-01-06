using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipesPage : ContentPage
{
    public RecipesPage(RecipesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RecipesViewModel vm)
        {
            vm.LoadRecipesCommand.Execute(null);
        }
    }
}

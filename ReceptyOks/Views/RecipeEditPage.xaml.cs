using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipeEditPage : ContentPage
{
    public RecipeEditPage(RecipeEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RecipeEditViewModel vm)
        {
            // Zawsze wywo³aj inicjalizacjê przy pojawieniu siê strony
            // InitializeAsync sprawdzi czy ju¿ jest zainicjalizowane
            await vm.InitializeCommand.ExecuteAsync(null);
        }
    }
}

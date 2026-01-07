using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RandomRecipePage : ContentPage
{
    private readonly RandomRecipeViewModel _viewModel;

    public RandomRecipePage(RandomRecipeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataCommand.ExecuteAsync(null);
    }
}

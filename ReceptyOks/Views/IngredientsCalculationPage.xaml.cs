using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class IngredientsCalculationPage : ContentPage
{
	public IngredientsCalculationPage(IngredientsCalculationViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
    }
}
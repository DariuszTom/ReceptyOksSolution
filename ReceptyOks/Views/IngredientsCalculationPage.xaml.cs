using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class IngredientsCalculationPage : ContentPage
{
	public IngredientsCalculationPage(IngredientsCalculationViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (BindingContext is IngredientsCalculationViewModel vm)
		{
			vm.LoadRecipesCommand.Execute(null);
		}
	}
}
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class AppStatusView : ContentPage
{
	public AppStatusView(AppStatusViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Auto-check health when page appears
		if (BindingContext is AppStatusViewModel vm)
		{
			await vm.CheckHealthAsync();
		}
	}
}
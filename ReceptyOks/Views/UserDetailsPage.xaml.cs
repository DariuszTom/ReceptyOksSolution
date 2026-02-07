using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class UserDetailsPage : ContentPage
{
	private readonly UserDetailsViewModel _viewModel;

	public UserDetailsPage(UserDetailsViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (_viewModel.LoadUserDetailsCommand.CanExecute(null))
		{
			await _viewModel.LoadUserDetailsCommand.ExecuteAsync(null);
		}
	}
}
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class HomeSeekerPage : ContentPage
{
    private readonly HomeSeekerViewModel _viewModel;
    private bool _initialized;

    public HomeSeekerPage(HomeSeekerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Coming back from the report page should not reset scroll position / paging.
        if (_initialized)
            return;

        _initialized = true;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}

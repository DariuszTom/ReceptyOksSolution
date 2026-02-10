using ReceptyOks.BlazorComponents.Services;
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipeDetailPage : ContentPage
{
    private readonly RecipeDetailViewModel _viewModel;
    private readonly HtmlViewerState _viewerState;

    public RecipeDetailPage(RecipeDetailViewModel viewModel, HtmlViewerState viewerState)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _viewerState = viewerState;
        BindingContext = viewModel;

        // Subscribe to ViewModel property changes to update Blazor component
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Defer content update to give BlazorWebView time to initialize
        // This helps avoid the race condition on Android where the component
        // may not be ready when the page constructor runs
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), UpdateInstructionsHtml);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.Recipe))
        {
            UpdateInstructionsHtml();
        }
    }

    private void UpdateInstructionsHtml()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _viewerState.Content = _viewModel.Recipe?.Instructions ?? string.Empty;
        });
    }
}

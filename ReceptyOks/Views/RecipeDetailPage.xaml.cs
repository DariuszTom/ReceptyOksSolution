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

        // Initialize content via shared state
        UpdateInstructionsHtml();

        // Subscribe to ViewModel property changes to update Blazor component
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
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

using Microsoft.AspNetCore.Components.WebView;
using ReceptyOks.BlazorComponents.Services;
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipeDetailPage : ContentPage
{
    private readonly RecipeDetailViewModel _viewModel;
    private readonly HtmlViewerState _viewerState;
    private bool _isBlazorInitialized;

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
        // Always update content - it will be queued if Blazor isn't ready yet
        UpdateInstructionsHtml();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _isBlazorInitialized = false;
    }

    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        _isBlazorInitialized = true;
        // Now that Blazor is ready, push content to the component
        UpdateInstructionsHtml();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.Recipe) && _isBlazorInitialized)
        {
            UpdateInstructionsHtml();
        }
    }

    private void UpdateInstructionsHtml()
    {
        var html = _viewModel.Recipe?.Instructions ?? string.Empty;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Always set content; HtmlViewerState will queue it until Blazor signals ready
            _viewerState.Content = html;
        });
    }
}

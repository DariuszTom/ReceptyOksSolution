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

        SetupBlazorViewerParameters();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
    }

    private bool IsDarkTheme => Application.Current?.RequestedTheme == AppTheme.Dark;

    private void SetupBlazorViewerParameters()
    {
        InstructionsRoot.Parameters = new Dictionary<string, object?>
        {
            { "IsDarkTheme", IsDarkTheme }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.Refresh();
        UpdateInstructionsHtml();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _isBlazorInitialized = false;

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }
    }

    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        _isBlazorInitialized = true;

#if ANDROID
        // Prevent the parent ScrollView from intercepting vertical touch events
        // so the Android WebView can scroll its own content.
        var androidWebView = e.WebView;
        androidWebView.Touch += (s, args) =>
        {
            androidWebView.Parent?.RequestDisallowInterceptTouchEvent(true);
            args.Handled = false;
        };
#endif

        UpdateInstructionsHtml();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            InstructionsRoot.Parameters = new Dictionary<string, object?>
            {
                { "IsDarkTheme", IsDarkTheme }
            };
        });
    }

    private const double DefaultViewerHeight = 450;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.Recipe) && _isBlazorInitialized)
        {
            UpdateInstructionsHtml();
        }
        else if (e.PropertyName == nameof(_viewModel.IsInstructionsFullScreen))
        {
            MainThread.BeginInvokeOnMainThread(UpdateViewerHeight);
        }
    }

    private void UpdateViewerHeight()
    {
        if (_viewModel.IsInstructionsFullScreen)
        {
            // Fill available page height, leaving room for the header row (~70px)
            var availableHeight = this.Height - 70;
            InstructionsBorder.HeightRequest = availableHeight > DefaultViewerHeight ? availableHeight : DefaultViewerHeight;
        }
        else
        {
            InstructionsBorder.HeightRequest = DefaultViewerHeight;
        }
    }

    private void UpdateInstructionsHtml()
    {
        var html = _viewModel.Recipe?.Instructions ?? string.Empty;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _viewerState.Content = html;
        });
    }
}

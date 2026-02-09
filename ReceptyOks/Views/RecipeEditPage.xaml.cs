using Microsoft.AspNetCore.Components;
using ReceptyOks.BlazorComponents.Services;
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipeEditPage : ContentPage
{
    private readonly RecipeEditViewModel _viewModel;
    private readonly InstructionsEditorState _editorState;
    private string _currentContent = string.Empty;

    public RecipeEditPage(RecipeEditViewModel viewModel, InstructionsEditorState editorState)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _editorState = editorState;
        BindingContext = viewModel;

        // Set up Blazor component parameters
        SetupBlazorEditorParameters();

        // Subscribe to ViewModel property changes to update Blazor component
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Subscribe to state changes coming from the Blazor editor
        _editorState.ContentChanged += OnEditorStateContentChanged;

        // Subscribe to app theme changes
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
    }

    private bool IsDarkTheme => Application.Current?.RequestedTheme == AppTheme.Dark;

    private void SetupBlazorEditorParameters()
    {
        InstructionsEditorRoot.Parameters = new Dictionary<string, object?>
        {
            { "Content", _viewModel.Instructions ?? string.Empty },
            { "ContentChanged", EventCallback.Factory.Create<string>(this, OnBlazorContentChanged) },
            { "Placeholder", "Wprowadź instrukcje przygotowania..." },
            { "IsDarkTheme", IsDarkTheme }
        };
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        // Update Blazor component when theme changes
        MainThread.BeginInvokeOnMainThread(UpdateBlazorEditorContent);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeEditViewModel.Instructions))
        {
            // Push content to Blazor via shared state
            _currentContent = _viewModel.Instructions ?? string.Empty;
            _editorState.Content = _currentContent;
        }
    }

    private void OnEditorStateContentChanged(object? sender, string newContent)
    {
        _currentContent = newContent;
        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] Editor state content changed, length: {newContent?.Length ?? 0}");
    }

    private void UpdateBlazorEditorContent()
    {
        if (InstructionsEditorRoot?.Parameters is not null)
        {
            InstructionsEditorRoot.Parameters = new Dictionary<string, object?>
            {
                { "Content", _currentContent },
                { "ContentChanged", EventCallback.Factory.Create<string>(this, OnBlazorContentChanged) },
                { "Placeholder", "Wprowadź instrukcje przygotowania..." },
                { "IsDarkTheme", IsDarkTheme }
            };
        }
    }

    private void OnBlazorContentChanged(string newContent)
    {
        _currentContent = newContent;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RecipeEditViewModel vm)
        {
     await vm.InitializeCommand.ExecuteAsync(null);

            // Defer content update to give BlazorWebView time to initialize
       // This helps avoid the race condition on Android where the component
            // may not be ready when the page appears
  Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
     {
    _currentContent = vm.Instructions ?? string.Empty;
                _editorState.Content = _currentContent;
 UpdateBlazorEditorContent();
            });
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _editorState.ContentChanged -= OnEditorStateContentChanged;

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }
    }

    /// <summary>
    /// Metoda publiczna do pobierania HTML przed zapisem - wywo\u0142ywana z ViewModelu
    /// </summary>
    public Task<string> GetInstructionsHtmlAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] HTML retrieved: {_currentContent}");
        return Task.FromResult(_currentContent);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // 1. Pobierz HTML z Blazor komponentu (zsynchronizowany przez state service)
        var html = _currentContent;

        // 2. Zapisz do ViewModelu
        _viewModel.Instructions = html;

        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] HTML saved: {html}");

        if (_viewModel.SaveCommand.CanExecute(null))
        {
            await _viewModel.SaveCommand.ExecuteAsync(null);
        }
    }
}

using Microsoft.AspNetCore.Components;
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipeEditPage : ContentPage
{
    private readonly RecipeEditViewModel _viewModel;
    private string _currentContent = string.Empty;

    public RecipeEditPage(RecipeEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Set up Blazor component parameters
        SetupBlazorEditorParameters();

        // Subscribe to ViewModel property changes to update Blazor component
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void SetupBlazorEditorParameters()
    {
        InstructionsEditorRoot.Parameters = new Dictionary<string, object?>
        {
            { "Content", _viewModel.Instructions ?? string.Empty },
            { "ContentChanged", EventCallback.Factory.Create<string>(this, OnBlazorContentChanged) },
     { "Placeholder", "WprowadŸ instrukcje przygotowania..." }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeEditViewModel.Instructions))
        {
            // Update Blazor component when ViewModel's Instructions change
            _currentContent = _viewModel.Instructions ?? string.Empty;
            UpdateBlazorEditorContent();
        }
    }

    private void UpdateBlazorEditorContent()
    {
        if (InstructionsEditorRoot?.Parameters is not null)
        {
            InstructionsEditorRoot.Parameters = new Dictionary<string, object?>
    {
        { "Content", _currentContent },
          { "ContentChanged", EventCallback.Factory.Create<string>(this, OnBlazorContentChanged) },
        { "Placeholder", "WprowadŸ instrukcje przygotowania..." }
      };
        }
    }

    private void OnBlazorContentChanged(string newContent)
    {
        _currentContent = newContent;
        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] Blazor content changed, length: {newContent?.Length ?? 0}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RecipeEditViewModel vm)
        {
            // Zawsze wywo³aj inicjalizacjê przy pojawieniu siê strony
            // InitializeAsync sprawdzi czy ju¿ jest zainicjalizowane
            await vm.InitializeCommand.ExecuteAsync(null);

            // Update Blazor component with initial content after ViewModel initialization
            _currentContent = vm.Instructions ?? string.Empty;
            UpdateBlazorEditorContent();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Metoda publiczna do pobierania HTML przed zapisem - wywo³ywana z ViewModelu
    /// </summary>
    public Task<string> GetInstructionsHtmlAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] HTML retrieved: {_currentContent}");
        return Task.FromResult(_currentContent);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // 1. Pobierz HTML z Blazor komponentu (ju¿ zsynchronizowany przez callback)
        var html = _currentContent;

        // 2. Zapisz do ViewModelu
        _viewModel.Instructions = html;

        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] HTML saved: {html}");

        // 3. Wywo³aj SaveCommand z ViewModelu
        if (_viewModel.SaveCommand.CanExecute(null))
        {
            await _viewModel.SaveCommand.ExecuteAsync(null);
        }
    }
}

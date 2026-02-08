using ReceptyOks.ViewModels;
using System.Web;

namespace ReceptyOks.Views;

public partial class RecipeEditPage : ContentPage
{
    private readonly RecipeEditViewModel _viewModel;
    
    public RecipeEditPage(RecipeEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RecipeEditViewModel vm)
        {
            // Zawsze wywo³aj inicjalizacjê przy pojawieniu siê strony
            // InitializeAsync sprawdzi czy ju¿ jest zainicjalizowane
            await vm.InitializeCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// Metoda publiczna do pobierania HTML przed zapisem - wywo³ywana z ViewModelu
    /// </summary>
    public async Task<string> GetInstructionsHtmlAsync()
    {
        if (InstructionsEditor == null) return string.Empty;
        
        var html = await InstructionsEditor.GetContentAsync();
        
        // Unescape HTML (JavaScript zwraca escaped, np. \u003C zamiast <)
        html = System.Text.RegularExpressions.Regex.Unescape(html);
        
        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] HTML retrieved: {html}");
        
        return html;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // 1. Pobierz HTML z edytora
        var html = await InstructionsEditor.GetContentAsync();
        
        // 2. Unescape HTML (JavaScript zwraca \u003C zamiast <)
        html = System.Text.RegularExpressions.Regex.Unescape(html);
        
        // 3. Zapisz do ViewModelu
        _viewModel.Instructions = html;
        
        System.Diagnostics.Debug.WriteLine($"[RecipeEditPage] HTML saved: {html}");
        
        // 4. Wywo³aj SaveCommand z ViewModelu
        if (_viewModel.SaveCommand.CanExecute(null))
        {
            await _viewModel.SaveCommand.ExecuteAsync(null);
        }
    }
}

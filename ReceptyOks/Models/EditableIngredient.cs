using CommunityToolkit.Mvvm.ComponentModel;
using ReceptyOks.Data;
using ReceptyOks.Shared;

namespace ReceptyOks.ViewModels;

public partial class EditableIngredient : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private IngredientLocal? selectedIngredient;

    [ObservableProperty]
    private string ingredientName = string.Empty;

    [ObservableProperty]
    private decimal quantity;

    [ObservableProperty]
    private Jednostki selectedUnit = Jednostki.Brak;

    [ObservableProperty]
    private string notes = string.Empty;

    partial void OnSelectedIngredientChanged(IngredientLocal? value)
    {
        if (value is not null)
        {
            IngredientName = value.Name;
        }
    }

    partial void OnIngredientNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && SelectedIngredient?.Name != value)
        {
            SelectedIngredient = null;
        }
    }
}

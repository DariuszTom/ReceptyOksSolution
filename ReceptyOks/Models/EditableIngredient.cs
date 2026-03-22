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

    /// <summary>
    /// Display text for the ingredient chip (e.g. "Mąka 200g").
    /// </summary>
    public string ChipDisplayName
    {
        get
        {
            var parts = new List<string> { IngredientName };
            if (Quantity > 0)
            {
                var unitText = SelectedUnit != Jednostki.Brak ? SelectedUnit.ToString() : "";
                parts.Add($"{Quantity} {unitText}");
            }
            return string.Join(" ", parts);
        }
    }

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
        OnPropertyChanged(nameof(ChipDisplayName));
    }
}

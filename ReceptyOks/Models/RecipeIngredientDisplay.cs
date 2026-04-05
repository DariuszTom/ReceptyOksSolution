using ReceptyOks.Interfaces;
using ReceptyOks.Shared.Interfaces;

namespace ReceptyOks.ViewModels;

public class RecipeIngredientDisplay : IIngredient
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public string DisplayText => $"{Quantity} {Unit} {Name}" + (string.IsNullOrEmpty(Notes) ? "" : $" ({Notes})");
}

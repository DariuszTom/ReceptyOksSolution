namespace ReceptyOks.Shared.Interfaces;

/// <summary>
/// Interface for ingredient usage in a recipe (with quantity and unit).
/// </summary>
public interface IIngredient
{
    /// <summary>
    /// Name of the ingredient.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Quantity of the ingredient used.
    /// </summary>
    decimal Quantity { get; set; }

    /// <summary>
    /// Unit for this specific usage, e.g. "g", "łyżki".
    /// </summary>
    string? Unit { get; set; }
}
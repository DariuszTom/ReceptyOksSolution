namespace ReceptyOks.Shared.Interfaces;

public interface IIngredient
{
    string Name { get; }
    decimal Quantity { get; set; }
    string Unit { get; set; }
}
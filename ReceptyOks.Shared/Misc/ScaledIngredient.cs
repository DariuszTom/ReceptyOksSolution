using ReceptyOks.Shared.Interfaces;

namespace ReceptyOks.Shared.Misc
{
    /// <summary>
    /// Represents a scaled ingredient with its original and new quantities.
    /// Implements <see cref="IIngredient"/> for compatibility with existing ingredient systems.
    /// </summary>
    public record ScaledIngredient : IIngredient
    {
        /// <summary>
        /// Name of the ingredient.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Original quantity from the recipe.
        /// </summary>
        public decimal OriginalQuantity { get; }

        /// <summary>
        /// Scaled quantity after applying the multiplier.
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Unit of the ingredient.
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Gets the scaled quantity rounded up (useful for items like eggs).
        /// </summary>
        public int ScaledQuantityRoundedUp => (int)Math.Ceiling(Quantity);

        /// <summary>
        /// Creates a new scaled ingredient.
        /// </summary>
        public ScaledIngredient(string name, decimal originalQuantity, decimal scaledQuantity, string unit)
        {
            Name = name;
            OriginalQuantity = originalQuantity;
            Quantity = scaledQuantity;
            Unit = unit;
        }
    }
}

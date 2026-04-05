using ReceptyOks.Shared.Interfaces;

namespace ReceptyOks.Shared.Misc
{
    /// <summary>
    /// Represents the shape of a baking form/pan.
    /// </summary>
    public enum FormShape
    {
        Rectangular,
        Circular
    }

    public static class FormCalculator
    {
        /// <summary>
        /// Calculates the multiplier for scaling ingredients from one form to another.
        /// </summary>
        /// <param name="originalForm">The original baking form from the recipe.</param>
        /// <param name="newForm">The new baking form to scale to.</param>
        /// <returns>The multiplier to apply to all ingredients.</returns>
        public static decimal CalculateMultiplier(BakingForm originalForm, BakingForm newForm)
        {
            ArgumentNullException.ThrowIfNull(originalForm);
            ArgumentNullException.ThrowIfNull(newForm);

            decimal originalArea = (decimal)originalForm.CalculateArea();
            decimal newArea = (decimal)newForm.CalculateArea();

            return newArea / originalArea;
        }

        /// <summary>
        /// Scales a single ingredient quantity by the given multiplier.
        /// </summary>
        /// <param name="originalQuantity">The original quantity from the recipe.</param>
        /// <param name="multiplier">The scaling multiplier.</param>
        /// <returns>The scaled quantity.</returns>
        public static decimal ScaleIngredient(decimal originalQuantity, decimal multiplier)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(originalQuantity);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);

            return originalQuantity * multiplier;
        }

        /// <summary>
        /// Scales a list of ingredients from one form size to another.
        /// Automatically rounds countable units (Sztuka, Opakowanie, Zabek) up to whole numbers.
        /// </summary>
        /// <param name="ingredients">Collection of ingredients implementing IIngredient.</param>
        /// <param name="originalForm">The original baking form from the recipe.</param>
        /// <param name="newForm">The new baking form to scale to.</param>
        /// <param name="precision">Decimal precision for measurable ingredients (default: 1).</param>
        /// <returns>List of scaled ingredients with original and new quantities.</returns>
        public static List<ScaledIngredient> ScaleIngredients(
            IEnumerable<IIngredient> ingredients,
            BakingForm originalForm,
            BakingForm newForm,
            int precision = 1)
        {
            ArgumentNullException.ThrowIfNull(ingredients);
            ArgumentNullException.ThrowIfNull(originalForm);
            ArgumentNullException.ThrowIfNull(newForm);

            decimal multiplier = CalculateMultiplier(originalForm, newForm);
            var scaledIngredients = new List<ScaledIngredient>();

            foreach (var ingredient in ingredients)
            {
                decimal scaledQuantity = ScaleIngredient(ingredient.Quantity, multiplier);
                var unit = UnitsExtensions.Parse(ingredient.Unit);

                // Round based on unit type: countable units round up, measurable units use precision
                scaledQuantity = unit.IsCountable()
                    ? Math.Ceiling(scaledQuantity)
                    : Math.Round(scaledQuantity, precision);

                scaledIngredients.Add(new ScaledIngredient(
                    ingredient.Name,
                    ingredient.Quantity,
                    scaledQuantity,
                    ingredient.Unit));
            }

            return scaledIngredients;
        }

        /// <summary>
        /// Calculates the area of a rectangular form.
        /// </summary>
        /// <param name="width">Width in cm.</param>
        /// <param name="length">Length in cm.</param>
        /// <returns>Area in square centimeters.</returns>
        public static double CalculateRectangularArea(double width, double length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            return width * length;
        }

        /// <summary>
        /// Calculates the area of a circular form.
        /// </summary>
        /// <param name="diameter">Diameter in cm.</param>
        /// <returns>Area in square centimeters.</returns>
        public static double CalculateCircularArea(double diameter)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diameter);

            double radius = diameter / 2;
            return Math.PI * radius * radius;
        }
    }
}

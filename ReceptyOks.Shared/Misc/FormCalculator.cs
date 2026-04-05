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
        /// </summary>
        /// <param name="ingredients">Collection of ingredients implementing IIngredient.</param>
        /// <param name="originalForm">The original baking form from the recipe.</param>
        /// <param name="newForm">The new baking form to scale to.</param>
        /// <returns>List of scaled ingredients with original and new quantities.</returns>
        public static List<ScaledIngredient> ScaleIngredients(
            IEnumerable<IIngredient> ingredients,
            BakingForm originalForm,
            BakingForm newForm)
        {
            ArgumentNullException.ThrowIfNull(ingredients);
            ArgumentNullException.ThrowIfNull(originalForm);
            ArgumentNullException.ThrowIfNull(newForm);

            decimal multiplier = CalculateMultiplier(originalForm, newForm);
            var scaledIngredients = new List<ScaledIngredient>();

            foreach (var ingredient in ingredients)
            {
                decimal scaledQuantity = ScaleIngredient(ingredient.Quantity, multiplier);
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

        /// <summary>
        /// Rounds an ingredient quantity appropriately based on the ingredient type.
        /// For countable items (like eggs), rounds up to the nearest whole number.
        /// For measurable items, rounds to a practical precision.
        /// </summary>
        /// <param name="quantity">The quantity to round.</param>
        /// <param name="isCountable">Whether the ingredient is countable (e.g., eggs).</param>
        /// <param name="precision">Decimal precision for measurable ingredients (default: 1).</param>
        /// <returns>The rounded quantity.</returns>
        public static decimal RoundIngredient(decimal quantity, bool isCountable, int precision = 1)
        {
            if (isCountable)
            {
                return Math.Ceiling(quantity);
            }

            return Math.Round(quantity, precision);
        }

        /// <summary>
        /// Rounds an ingredient quantity based on its unit type.
        /// Countable units (Sztuka, Opakowanie, Zabek) are rounded up.
        /// Measurable units are rounded to the specified precision.
        /// </summary>
        /// <param name="quantity">The quantity to round.</param>
        /// <param name="unit">The unit of the ingredient.</param>
        /// <param name="precision">Decimal precision for measurable ingredients (default: 1).</param>
        /// <returns>The rounded quantity.</returns>
        public static decimal RoundIngredient(decimal quantity, Units unit, int precision = 1)
        {
            return RoundIngredient(quantity, unit.IsCountable(), precision);
        }
    }
}

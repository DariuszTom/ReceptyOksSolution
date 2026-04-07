namespace ReceptyOks.Shared.Misc
{
    /// <summary>
    /// Represents a baking form with its dimensions.
    /// </summary>
    public record BakingForm
    {
        /// <summary>
        /// Shape of the baking form.
        /// </summary>
        public FormShape Shape { get; set; }

        /// <summary>
        /// Width in centimeters (for rectangular forms).
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Length in centimeters (for rectangular forms).
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// Diameter in centimeters (for circular forms).
        /// </summary>
        public double Diameter { get; set; }

        /// <summary>
        /// Creates a rectangular baking form.
        /// </summary>
        /// <param name="width">Width in cm.</param>
        /// <param name="length">Length in cm.</param>
        public static BakingForm Rectangular(double width, double length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            return new BakingForm
            {
                Shape = FormShape.Rectangular,
                Width = width,
                Length = length
            };
        }

        /// <summary>
        /// Creates a circular baking form.
        /// </summary>
        /// <param name="diameter">Diameter in cm.</param>
        public static BakingForm Circular(double diameter)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diameter);

            return new BakingForm
            {
                Shape = FormShape.Circular,
                Diameter = diameter
            };
        }

        /// <summary>
        /// Calculates the surface area of the baking form in square centimeters.
        /// </summary>
        public double CalculateArea()
        {
            return Shape switch
            {
                FormShape.Rectangular => Width * Length,
                FormShape.Circular => Math.PI * Math.Pow(Diameter / 2, 2),
                _ => throw new InvalidOperationException($"Unknown form shape: {Shape}")
            };
        }
    }
}

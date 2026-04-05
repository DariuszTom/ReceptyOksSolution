using ReceptyOks.Shared.Interfaces;

namespace ReceptyOks.Shared.Misc.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="FormCalculator"/> class.
    /// </summary>
    [TestFixture]
    public class FormCalculatorTests
    {
        #region CalculateMultiplier Tests

        [Test]
        public void CalculateMultiplier_SameRectangularForms_ReturnsOne()
        {
            // Arrange
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(20, 30);

            // Act
            var result = FormCalculator.CalculateMultiplier(original, newForm);

            // Assert
            Assert.That(result, Is.EqualTo(1m));
        }

        [Test]
        public void CalculateMultiplier_LargerRectangularForm_ReturnsGreaterThanOne()
        {
            // Arrange
            var original = BakingForm.Rectangular(20, 30);  // 600 cm²
            var newForm = BakingForm.Rectangular(25, 35);   // 875 cm²

            // Act
            var result = FormCalculator.CalculateMultiplier(original, newForm);

            // Assert
            Assert.That(result, Is.GreaterThan(1m));
            Assert.That(result, Is.EqualTo(875m / 600m).Within(0.0001m));
        }

        [Test]
        public void CalculateMultiplier_SmallerRectangularForm_ReturnsLessThanOne()
        {
            // Arrange
            var original = BakingForm.Rectangular(25, 35);  // 875 cm²
            var newForm = BakingForm.Rectangular(20, 30);   // 600 cm²

            // Act
            var result = FormCalculator.CalculateMultiplier(original, newForm);

            // Assert
            Assert.That(result, Is.LessThan(1m));
        }

        [Test]
        public void CalculateMultiplier_SameCircularForms_ReturnsOne()
        {
            // Arrange
            var original = BakingForm.Circular(24);
            var newForm = BakingForm.Circular(24);

            // Act
            var result = FormCalculator.CalculateMultiplier(original, newForm);

            // Assert
            Assert.That(result, Is.EqualTo(1m));
        }

        [Test]
        public void CalculateMultiplier_LargerCircularForm_ReturnsGreaterThanOne()
        {
            // Arrange
            var original = BakingForm.Circular(20);  // π * 10² ≈ 314.16 cm²
            var newForm = BakingForm.Circular(26);   // π * 13² ≈ 530.93 cm²

            // Act
            var result = FormCalculator.CalculateMultiplier(original, newForm);

            // Assert
            Assert.That(result, Is.GreaterThan(1m));
        }

        [Test]
        public void CalculateMultiplier_RectangularToCircular_CalculatesCorrectly()
        {
            // Arrange
            var rectangular = BakingForm.Rectangular(20, 30);  // 600 cm²
            var circular = BakingForm.Circular(20);            // π * 10² ≈ 314.16 cm²

            // Act
            var result = FormCalculator.CalculateMultiplier(rectangular, circular);

            // Assert
            var expectedMultiplier = (decimal)(Math.PI * 100) / 600m;
            Assert.That(result, Is.EqualTo(expectedMultiplier).Within(0.0001m));
        }

        [Test]
        public void CalculateMultiplier_NullOriginalForm_ThrowsArgumentNullException()
        {
            // Arrange
            var newForm = BakingForm.Rectangular(20, 30);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => FormCalculator.CalculateMultiplier(null!, newForm));
        }

        [Test]
        public void CalculateMultiplier_NullNewForm_ThrowsArgumentNullException()
        {
            // Arrange
            var original = BakingForm.Rectangular(20, 30);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => FormCalculator.CalculateMultiplier(original, null!));
        }

        #endregion

        #region ScaleIngredient Tests

        [TestCase(100, 1.5, 150)]
        [TestCase(200, 2.0, 400)]
        [TestCase(50, 0.5, 25)]
        [TestCase(0, 1.5, 0)]
        public void ScaleIngredient_VariousInputs_ReturnsCorrectResult(
            decimal originalQuantity, decimal multiplier, decimal expected)
        {
            // Act
            var result = FormCalculator.ScaleIngredient(originalQuantity, multiplier);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ScaleIngredient_NegativeQuantity_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => FormCalculator.ScaleIngredient(-1m, 1.5m));
        }

        [Test]
        public void ScaleIngredient_ZeroMultiplier_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => FormCalculator.ScaleIngredient(100m, 0m));
        }

        [Test]
        public void ScaleIngredient_NegativeMultiplier_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => FormCalculator.ScaleIngredient(100m, -1m));
        }

        #endregion

        #region ScaleIngredients Tests

        [Test]
        public void ScaleIngredients_MeasurableUnits_RoundsToPrecision()
        {
            // Arrange
            var ingredients = new List<TestIngredient>
            {
                new("Mąka", 200m, "Gram")
            };
            var original = BakingForm.Rectangular(20, 30);  // 600 cm²
            var newForm = BakingForm.Rectangular(25, 35);   // 875 cm²
            // Multiplier: 875/600 ≈ 1.4583

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("Mąka"));
            Assert.That(result[0].OriginalQuantity, Is.EqualTo(200m));
            Assert.That(result[0].Unit, Is.EqualTo("Gram"));
            // 200 * 1.4583... ≈ 291.67 → rounded to 1 decimal = 291.7
            Assert.That(result[0].Quantity, Is.EqualTo(291.7m));
        }

        [Test]
        public void ScaleIngredients_CountableUnit_RoundsUp()
        {
            // Arrange
            var ingredients = new List<TestIngredient>
            {
                new("Jajka", 3m, "Sztuka")
            };
            var original = BakingForm.Rectangular(20, 30);  // 600 cm²
            var newForm = BakingForm.Rectangular(25, 35);   // 875 cm²
            // Multiplier: 875/600 ≈ 1.4583
            // 3 * 1.4583 = 4.375 → ceiling = 5

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Quantity, Is.EqualTo(5m));
        }

        [Test]
        public void ScaleIngredients_OpakowanieUnit_RoundsUp()
        {
            // Arrange
            var ingredients = new List<TestIngredient>
            {
                new("Drożdże", 1m, "Opakowanie")
            };
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(25, 35);

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm);

            // Assert
            Assert.That(result[0].Quantity, Is.EqualTo(2m)); // 1 * 1.4583 = 1.4583 → ceiling = 2
        }

        [Test]
        public void ScaleIngredients_ZabekUnit_RoundsUp()
        {
            // Arrange
            var ingredients = new List<TestIngredient>
            {
                new("Czosnek", 2m, "Zabek")
            };
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(25, 35);

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm);

            // Assert
            Assert.That(result[0].Quantity, Is.EqualTo(3m)); // 2 * 1.4583 = 2.916 → ceiling = 3
        }

        [Test]
        public void ScaleIngredients_MultipleIngredients_ScalesAllCorrectly()
        {
            // Arrange
            var ingredients = new List<TestIngredient>
            {
                new("Mąka", 200m, "Gram"),
                new("Jajka", 3m, "Sztuka"),
                new("Mleko", 250m, "Mililitr")
            };
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(20, 30); // Same size, multiplier = 1

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm);

            // Assert
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0].Quantity, Is.EqualTo(200m));
            Assert.That(result[1].Quantity, Is.EqualTo(3m));
            Assert.That(result[2].Quantity, Is.EqualTo(250m));
        }

        [Test]
        public void ScaleIngredients_EmptyList_ReturnsEmptyList()
        {
            // Arrange
            var ingredients = new List<TestIngredient>();
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(25, 35);

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ScaleIngredients_CustomPrecision_RoundsToSpecifiedDecimals()
        {
            // Arrange
            var ingredients = new List<TestIngredient>
            {
                new("Mąka", 200m, "Gram")
            };
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(25, 35);

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm, precision: 2);

            // Assert
            // 200 * 1.4583... ≈ 291.666... → rounded to 2 decimals = 291.67
            Assert.That(result[0].Quantity, Is.EqualTo(291.67m));
        }

        [Test]
        public void ScaleIngredients_UnknownUnit_TreatsAsMeasurable()
        {
            // Arrange
            var ingredients = new List<TestIngredient>
            {
                new("Składnik", 10m, "NieznanaJednostka")
            };
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(25, 35);

            // Act
            var result = FormCalculator.ScaleIngredients(ingredients, original, newForm);

            // Assert
            // Unknown unit defaults to Brak which is not countable, so it rounds to precision
            Assert.That(result[0].Quantity, Is.EqualTo(14.6m)); // 10 * 1.4583 ≈ 14.58 → 14.6
        }

        [Test]
        public void ScaleIngredients_NullIngredients_ThrowsArgumentNullException()
        {
            // Arrange
            var original = BakingForm.Rectangular(20, 30);
            var newForm = BakingForm.Rectangular(25, 35);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                FormCalculator.ScaleIngredients(null!, original, newForm));
        }

        [Test]
        public void ScaleIngredients_NullOriginalForm_ThrowsArgumentNullException()
        {
            // Arrange
            var ingredients = new List<TestIngredient>();
            var newForm = BakingForm.Rectangular(25, 35);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                FormCalculator.ScaleIngredients(ingredients, null!, newForm));
        }

        [Test]
        public void ScaleIngredients_NullNewForm_ThrowsArgumentNullException()
        {
            // Arrange
            var ingredients = new List<TestIngredient>();
            var original = BakingForm.Rectangular(20, 30);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                FormCalculator.ScaleIngredients(ingredients, original, null!));
        }

        #endregion

        #region CalculateRectangularArea Tests

        [TestCase(20, 30, 600)]
        [TestCase(10, 10, 100)]
        [TestCase(25.5, 35.5, 905.25)]
        public void CalculateRectangularArea_ValidDimensions_ReturnsCorrectArea(
            double width, double length, double expected)
        {
            // Act
            var result = FormCalculator.CalculateRectangularArea(width, length);

            // Assert
            Assert.That(result, Is.EqualTo(expected).Within(0.001));
        }

        [Test]
        public void CalculateRectangularArea_ZeroWidth_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FormCalculator.CalculateRectangularArea(0, 30));
        }

        [Test]
        public void CalculateRectangularArea_ZeroLength_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FormCalculator.CalculateRectangularArea(20, 0));
        }

        [Test]
        public void CalculateRectangularArea_NegativeWidth_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FormCalculator.CalculateRectangularArea(-20, 30));
        }

        #endregion

        #region CalculateCircularArea Tests

        [Test]
        public void CalculateCircularArea_ValidDiameter_ReturnsCorrectArea()
        {
            // Arrange
            double diameter = 20;
            double expectedArea = Math.PI * 10 * 10; // π * r²

            // Act
            var result = FormCalculator.CalculateCircularArea(diameter);

            // Assert
            Assert.That(result, Is.EqualTo(expectedArea).Within(0.001));
        }

        [TestCase(24, 452.389)] // π * 12² ≈ 452.389
        [TestCase(26, 530.929)] // π * 13² ≈ 530.929
        public void CalculateCircularArea_VariousDiameters_ReturnsCorrectArea(
            double diameter, double expected)
        {
            // Act
            var result = FormCalculator.CalculateCircularArea(diameter);

            // Assert
            Assert.That(result, Is.EqualTo(expected).Within(0.001));
        }

        [Test]
        public void CalculateCircularArea_ZeroDiameter_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FormCalculator.CalculateCircularArea(0));
        }

        [Test]
        public void CalculateCircularArea_NegativeDiameter_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FormCalculator.CalculateCircularArea(-20));
        }

        #endregion

        #region Test Helpers

        /// <summary>
        /// Simple test implementation of IIngredient for testing purposes.
        /// </summary>
        private record TestIngredient(string Name, decimal Quantity, string Unit) : IIngredient
        {
            public decimal Quantity { get; set; } = Quantity;
            public string Unit { get; set; } = Unit;
        }

        #endregion
    }
}

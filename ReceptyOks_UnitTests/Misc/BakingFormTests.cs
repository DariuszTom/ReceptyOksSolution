
namespace ReceptyOks.Shared.Misc.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="BakingForm"/> record.
    /// </summary>
    [TestFixture]
    public class BakingFormTests
    {
        #region Rectangular Factory Tests

        [Test]
        public void Rectangular_ValidDimensions_CreatesCorrectForm()
        {
            // Act
            var form = BakingForm.Rectangular(20, 30);

            // Assert
            Assert.That(form.Shape, Is.EqualTo(FormShape.Rectangular));
            Assert.That(form.Width, Is.EqualTo(20));
            Assert.That(form.Length, Is.EqualTo(30));
        }

        [Test]
        public void Rectangular_ZeroWidth_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => BakingForm.Rectangular(0, 30));
        }

        [Test]
        public void Rectangular_ZeroLength_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => BakingForm.Rectangular(20, 0));
        }

        [Test]
        public void Rectangular_NegativeWidth_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => BakingForm.Rectangular(-20, 30));
        }

        [Test]
        public void Rectangular_NegativeLength_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => BakingForm.Rectangular(20, -30));
        }

        #endregion

        #region Circular Factory Tests

        [Test]
        public void Circular_ValidDiameter_CreatesCorrectForm()
        {
            // Act
            var form = BakingForm.Circular(24);

            // Assert
            Assert.That(form.Shape, Is.EqualTo(FormShape.Circular));
            Assert.That(form.Diameter, Is.EqualTo(24));
        }

        [Test]
        public void Circular_ZeroDiameter_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => BakingForm.Circular(0));
        }

        [Test]
        public void Circular_NegativeDiameter_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => BakingForm.Circular(-24));
        }

        #endregion

        #region CalculateArea Tests

        [TestCase(20, 30, 600)]
        [TestCase(10, 10, 100)]
        [TestCase(25, 40, 1000)]
        public void CalculateArea_RectangularForm_ReturnsCorrectArea(
            double width, double length, double expected)
        {
            // Arrange
            var form = BakingForm.Rectangular(width, length);

            // Act
            var result = form.CalculateArea();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void CalculateArea_CircularForm_ReturnsCorrectArea()
        {
            // Arrange
            var form = BakingForm.Circular(20);
            var expected = Math.PI * 10 * 10; // π * r²

            // Act
            var result = form.CalculateArea();

            // Assert
            Assert.That(result, Is.EqualTo(expected).Within(0.001));
        }

        [TestCase(24, 452.389)] // π * 12² ≈ 452.389
        [TestCase(26, 530.929)] // π * 13² ≈ 530.929
        [TestCase(28, 615.752)] // π * 14² ≈ 615.752
        public void CalculateArea_CircularFormVariousSizes_ReturnsCorrectArea(
            double diameter, double expected)
        {
            // Arrange
            var form = BakingForm.Circular(diameter);

            // Act
            var result = form.CalculateArea();

            // Assert
            Assert.That(result, Is.EqualTo(expected).Within(0.001));
        }

        #endregion

        #region Record Equality Tests

        [Test]
        public void Equality_SameRectangularForms_AreEqual()
        {
            // Arrange
            var form1 = BakingForm.Rectangular(20, 30);
            var form2 = BakingForm.Rectangular(20, 30);

            // Assert
            Assert.That(form1, Is.EqualTo(form2));
        }

        [Test]
        public void Equality_DifferentRectangularForms_AreNotEqual()
        {
            // Arrange
            var form1 = BakingForm.Rectangular(20, 30);
            var form2 = BakingForm.Rectangular(25, 35);

            // Assert
            Assert.That(form1, Is.Not.EqualTo(form2));
        }

        [Test]
        public void Equality_SameCircularForms_AreEqual()
        {
            // Arrange
            var form1 = BakingForm.Circular(24);
            var form2 = BakingForm.Circular(24);

            // Assert
            Assert.That(form1, Is.EqualTo(form2));
        }

        #endregion
    }
}

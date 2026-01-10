using System.Globalization;


namespace ReceptyOks.Converters.UnitTests;

/// <summary>
/// Unit tests for IsNullConverter.
/// </summary>
[TestFixture]
public class IsNullConverterTests
{
    /// <summary>
    /// Tests the Convert method for various input values to ensure correct boolean is returned
    /// when checking for nullness of the input value.
    /// </summary>
    /// <param name="input">The input to be checked for null.</param>
    /// <param name="expected">Expected result: true if input is null, false otherwise.</param>
    [TestCase(null, true, TestName = "Convert_InputIsNull_ReturnsTrue")]
    [TestCase("", false, TestName = "Convert_InputIsEmptyString_ReturnsFalse")]
    [TestCase("nonempty", false, TestName = "Convert_InputIsNonEmptyString_ReturnsFalse")]
    [TestCase(0, false, TestName = "Convert_InputIsZeroInt_ReturnsFalse")]
    [TestCase(123, false, TestName = "Convert_InputIsPositiveInt_ReturnsFalse")]
    [TestCase(-5, false, TestName = "Convert_InputIsNegativeInt_ReturnsFalse")]
    [TestCase(0.0, false, TestName = "Convert_InputIsZeroDouble_ReturnsFalse")]
    [TestCase(double.NaN, false, TestName = "Convert_InputIsNaN_ReturnsFalse")]
    [TestCase(true, false, TestName = "Convert_InputIsTrueBool_ReturnsFalse")]
    [TestCase(false, false, TestName = "Convert_InputIsFalseBool_ReturnsFalse")]
    [TestCaseSource(nameof(GetReferenceTypeCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new IsNullConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Provides additional reference type edge cases for Convert method.
    /// </summary>
    private static object[] GetReferenceTypeCases =
    {
            new object[] { new object(), false },
            new object[] { new int[] { }, false },
            new object[] { new int[] { 1, 2, 3 }, false },
            new object[] { " ", false },
            new object[] { "\0", false }
        };
}

/// <summary>
/// Unit tests for StringIsNotNullOrEmptyConverter.Convert method.
/// </summary>
[TestFixture]
public class StringIsNotNullOrEmptyConverterTests
{
    /// <summary>
    /// Provides a very long string for boundary testing.
    /// </summary>
    private static object[] GetLongStringTestCases =
    {
            new object[] { new string('a', 10000), true },
        };
}

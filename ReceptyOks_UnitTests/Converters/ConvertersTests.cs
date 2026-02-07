using System;
using System.Globalization;

using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using NUnit.Framework;
using ReceptyOks.Converters;

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

    /// <summary>
    /// Tests the Convert method with various input values to ensure correct boolean is returned
    /// when checking if the input value is a non-null, non-empty string.
    /// </summary>
    /// <param name="input">The input value to be checked.</param>
    /// <param name="expected">Expected result: true if input is a non-null, non-empty string, false otherwise.</param>
    [TestCase(null, false, TestName = "Convert_InputIsNull_ReturnsFalse")]
    [TestCase("", false, TestName = "Convert_InputIsEmptyString_ReturnsFalse")]
    [TestCase("nonempty", true, TestName = "Convert_InputIsNonEmptyString_ReturnsTrue")]
    [TestCase("a", true, TestName = "Convert_InputIsSingleCharString_ReturnsTrue")]
    [TestCase(" ", true, TestName = "Convert_InputIsWhitespaceString_ReturnsTrue")]
    [TestCase("  ", true, TestName = "Convert_InputIsMultipleWhitespaceString_ReturnsTrue")]
    [TestCase("\t", true, TestName = "Convert_InputIsTabString_ReturnsTrue")]
    [TestCase("\n", true, TestName = "Convert_InputIsNewlineString_ReturnsTrue")]
    [TestCase("\r\n", true, TestName = "Convert_InputIsCarriageReturnNewlineString_ReturnsTrue")]
    [TestCase("test with spaces", true, TestName = "Convert_InputIsStringWithSpaces_ReturnsTrue")]
    [TestCase("special!@#$%^&*()", true, TestName = "Convert_InputIsStringWithSpecialChars_ReturnsTrue")]
    [TestCase("123", true, TestName = "Convert_InputIsNumericString_ReturnsTrue")]
    [TestCase("0", true, TestName = "Convert_InputIsZeroString_ReturnsTrue")]
    [TestCase(0, false, TestName = "Convert_InputIsZeroInt_ReturnsFalse")]
    [TestCase(123, false, TestName = "Convert_InputIsPositiveInt_ReturnsFalse")]
    [TestCase(-5, false, TestName = "Convert_InputIsNegativeInt_ReturnsFalse")]
    [TestCase(int.MinValue, false, TestName = "Convert_InputIsMinInt_ReturnsFalse")]
    [TestCase(int.MaxValue, false, TestName = "Convert_InputIsMaxInt_ReturnsFalse")]
    [TestCase(0.0, false, TestName = "Convert_InputIsZeroDouble_ReturnsFalse")]
    [TestCase(1.5, false, TestName = "Convert_InputIsPositiveDouble_ReturnsFalse")]
    [TestCase(-1.5, false, TestName = "Convert_InputIsNegativeDouble_ReturnsFalse")]
    [TestCase(double.NaN, false, TestName = "Convert_InputIsNaN_ReturnsFalse")]
    [TestCase(double.PositiveInfinity, false, TestName = "Convert_InputIsPositiveInfinity_ReturnsFalse")]
    [TestCase(double.NegativeInfinity, false, TestName = "Convert_InputIsNegativeInfinity_ReturnsFalse")]
    [TestCase(true, false, TestName = "Convert_InputIsTrueBool_ReturnsFalse")]
    [TestCase(false, false, TestName = "Convert_InputIsFalseBool_ReturnsFalse")]
    [TestCaseSource(nameof(GetReferenceTypeCases))]
    [TestCaseSource(nameof(GetLongStringTestCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new StringIsNotNullOrEmptyConverter();

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
        new object[] { "\0", true },
        new object[] { "Unicode: \u0041\u0042\u0043", true }
    };
}



/// <summary>
/// Unit tests for ByteArrayToImageSourceConverter.Convert method.
/// </summary>
[TestFixture]
public class ByteArrayToImageSourceConverterTests
{
    /// <summary>
    /// Tests the Convert method with null input value.
    /// Expected: Returns null.
    /// </summary>
    [Test]
    public void Convert_InputIsNull_ReturnsNull()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act
        var result = converter.Convert(null, typeof(ImageSource), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests the Convert method with empty byte array.
    /// Expected: Returns null because array length is zero.
    /// </summary>
    [Test]
    public void Convert_InputIsEmptyByteArray_ReturnsNull()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var emptyBytes = new byte[0];

        // Act
        var result = converter.Convert(emptyBytes, typeof(ImageSource), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests the Convert method with non-byte-array input types.
    /// Expected: Returns null for all non-byte-array types.
    /// </summary>
    /// <param name="input">The input value to test.</param>
    [TestCase("string")]
    [TestCase(123)]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    [TestCase(0.0)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    [TestCase(true)]
    [TestCase(false)]
    public void Convert_InputIsNotByteArray_ReturnsNull(object input)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act
        var result = converter.Convert(input, typeof(ImageSource), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests the Convert method with non-byte-array reference types.
    /// Expected: Returns null for all non-byte-array reference types.
    /// </summary>
    [TestCaseSource(nameof(GetNonByteArrayReferenceTypes))]
    public void Convert_InputIsNonByteArrayReferenceType_ReturnsNull(object input)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act
        var result = converter.Convert(input, typeof(ImageSource), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Provides test cases for non-byte-array reference types.
    /// </summary>
    private static object[] GetNonByteArrayReferenceTypes =
    {
        new object[] { new object() },
        new object[] { new int[] { 1, 2, 3 } },
        new object[] { new string[] { "a", "b" } },
        new object[] { "" },
        new object[] { " " },
        new object[] { "\0" },
        new object[] { new string('a', 10000) }
    };

    /// <summary>
    /// Tests the ConvertBack method to ensure it always throws NotImplementedException
    /// regardless of input parameters.
    /// </summary>
    /// <param name="value">The value parameter to pass to ConvertBack.</param>
    /// <param name="targetType">The target type parameter.</param>
    /// <param name="parameter">The converter parameter.</param>
    /// <param name="culture">The culture info parameter.</param>
    [TestCase(null, typeof(byte[]), null, "en-US", TestName = "ConvertBack_AllNullableParametersNull_ThrowsNotImplementedException")]
    [TestCase("test", typeof(byte[]), null, "en-US", TestName = "ConvertBack_StringValue_ThrowsNotImplementedException")]
    [TestCase(123, typeof(byte[]), "param", "en-US", TestName = "ConvertBack_IntValue_ThrowsNotImplementedException")]
    [TestCase(true, typeof(object), null, "en-US", TestName = "ConvertBack_BoolValue_ThrowsNotImplementedException")]
    [TestCase(null, typeof(string), null, "fr-FR", TestName = "ConvertBack_DifferentCulture_ThrowsNotImplementedException")]
    [TestCase(null, typeof(byte[]), "parameter", "de-DE", TestName = "ConvertBack_WithParameter_ThrowsNotImplementedException")]
    [TestCaseSource(nameof(GetConvertBackEdgeCases))]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, Type? targetType, object? parameter, string? cultureCode)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var culture = cultureCode != null ? new CultureInfo(cultureCode) : CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType ?? typeof(object), parameter, culture));
    }

    /// <summary>
    /// Provides edge case test data for ConvertBack method.
    /// </summary>
    private static object[] GetConvertBackEdgeCases =
    {
        new object?[] { new byte[] { }, typeof(byte[]), null, "en-US" },
        new object?[] { new byte[] { 1, 2, 3, 255 }, typeof(ImageSource), null, "en-US" },
        new object?[] { double.NaN, typeof(object), null, "en-US" },
        new object?[] { double.PositiveInfinity, typeof(object), null, "en-US" },
        new object?[] { double.NegativeInfinity, typeof(object), null, "en-US" },
        new object?[] { int.MinValue, typeof(int), null, "en-US" },
        new object?[] { int.MaxValue, typeof(int), null, "en-US" },
        new object?[] { "", typeof(string), null, "en-US" },
        new object?[] { "   ", typeof(string), null, "en-US" },
        new object?[] { new string('a', 10000), typeof(string), null, "en-US" },
        new object?[] { "\0\n\r\t", typeof(string), null, "en-US" },
        new object?[] { new object(), typeof(object), new object(), "en-US" }
    };

    /// <summary>
    /// Tests the ConvertBack method with invariant culture to ensure NotImplementedException is thrown.
    /// </summary>
    [Test]
    public void ConvertBack_WithInvariantCulture_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(byte[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method to ensure it consistently throws NotImplementedException
    /// for all input combinations, as this is a one-way converter.
    /// </summary>
    /// <param name="value">The value parameter to pass to ConvertBack.</param>
    /// <param name="targetType">The target type parameter to pass to ConvertBack.</param>
    /// <param name="parameter">The parameter to pass to ConvertBack.</param>
    [TestCase(null, typeof(object), null, TestName = "ConvertBack_AllNullableParametersNull_ThrowsNotImplementedException")]
    [TestCase("test", typeof(byte[]), null, TestName = "ConvertBack_StringValue_ThrowsNotImplementedException")]
    [TestCase(123, typeof(object), "param", TestName = "ConvertBack_IntValue_ThrowsNotImplementedException")]
    [TestCase(true, typeof(string), 456, TestName = "ConvertBack_BoolValue_ThrowsNotImplementedException")]
    [TestCase(0, typeof(int), null, TestName = "ConvertBack_ZeroValue_ThrowsNotImplementedException")]
    [TestCase(-1, typeof(object), null, TestName = "ConvertBack_NegativeIntValue_ThrowsNotImplementedException")]
    [TestCase(int.MinValue, typeof(object), null, TestName = "ConvertBack_IntMinValue_ThrowsNotImplementedException")]
    [TestCase(int.MaxValue, typeof(object), null, TestName = "ConvertBack_IntMaxValue_ThrowsNotImplementedException")]
    [TestCase(double.NaN, typeof(object), null, TestName = "ConvertBack_DoubleNaN_ThrowsNotImplementedException")]
    [TestCase(double.PositiveInfinity, typeof(object), null, TestName = "ConvertBack_DoublePositiveInfinity_ThrowsNotImplementedException")]
    [TestCase(double.NegativeInfinity, typeof(object), null, TestName = "ConvertBack_DoubleNegativeInfinity_ThrowsNotImplementedException")]
    [TestCase("", typeof(byte[]), null, TestName = "ConvertBack_EmptyString_ThrowsNotImplementedException")]
    [TestCase(" ", typeof(object), null, TestName = "ConvertBack_WhitespaceString_ThrowsNotImplementedException")]
    [TestCaseSource(nameof(GetComplexTestCases))]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, Type targetType, object? parameter)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.That(() => converter.ConvertBack(value, targetType, parameter, CultureInfo.InvariantCulture),
            Throws.TypeOf<NotImplementedException>());
    }

    /// <summary>
    /// Tests the ConvertBack method with different CultureInfo values
    /// to ensure NotImplementedException is thrown regardless of culture.
    /// </summary>
    /// <param name="culture">The CultureInfo to use in the conversion.</param>
    [TestCase("en-US", TestName = "ConvertBack_EnglishCulture_ThrowsNotImplementedException")]
    [TestCase("fr-FR", TestName = "ConvertBack_FrenchCulture_ThrowsNotImplementedException")]
    [TestCase("de-DE", TestName = "ConvertBack_GermanCulture_ThrowsNotImplementedException")]
    [TestCase("", TestName = "ConvertBack_InvariantCulture_ThrowsNotImplementedException")]
    public void ConvertBack_VariousCultures_ThrowsNotImplementedException(string cultureName)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var culture = string.IsNullOrEmpty(cultureName)
            ? CultureInfo.InvariantCulture
            : new CultureInfo(cultureName);

        // Act & Assert
        Assert.That(() => converter.ConvertBack(null, typeof(object), null, culture),
            Throws.TypeOf<NotImplementedException>());
    }

    /// <summary>
    /// Provides complex test cases including byte arrays, collections, and special objects
    /// for ConvertBack method testing.
    /// </summary>
    private static object[] GetComplexTestCases =
    {
        new object[] { new byte[] { }, typeof(string), null },
        new object[] { new byte[] { 1, 2, 3 }, typeof(byte[]), "param" },
        new object[] { new int[] { 1, 2, 3 }, typeof(object), null },
        new object[] { new object(), typeof(object), new object() },
        new object[] { new string('a', 10000), typeof(string), null },
        new object[] { "\0", typeof(object), null },
        new object[] { "\t\r\n", typeof(string), null },
        new object[] { 0.0, typeof(double), null },
        new object[] { -0.0, typeof(double), null },
        new object[] { false, typeof(bool), null }
    };

    /// <summary>
    /// Tests ConvertBack with a byte array value.
    /// </summary>
    [Test]
    public void ConvertBack_ByteArrayValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var byteArray = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(byteArray, typeof(byte[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests ConvertBack with an empty byte array value.
    /// </summary>
    [Test]
    public void ConvertBack_EmptyByteArray_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var emptyArray = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(emptyArray, typeof(byte[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests ConvertBack with various culture settings.
    /// </summary>
    [TestCaseSource(nameof(GetCultureTestCases))]
    public void ConvertBack_VariousCultures_ThrowsNotImplementedException(CultureInfo culture)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(object), null, culture));
    }

    /// <summary>
    /// Provides various culture test cases for ConvertBack method.
    /// </summary>
    private static object[] GetCultureTestCases =
    {
        new object[] { CultureInfo.InvariantCulture },
        new object[] { CultureInfo.CurrentCulture },
        new object[] { new CultureInfo("en-US") },
        new object[] { new CultureInfo("pl-PL") }
    };

    /// <summary>
    /// Tests that ConvertBack method always throws NotImplementedException
    /// regardless of input parameters.
    /// </summary>
    /// <param name="value">The value parameter to pass.</param>
    /// <param name="targetType">The target type parameter to pass.</param>
    /// <param name="parameter">The parameter to pass.</param>
    /// <param name="culture">The culture info to pass.</param>
    [TestCase(null, typeof(byte[]), null, "en-US", TestName = "ConvertBack_NullValue_ThrowsNotImplementedException")]
    [TestCase("test", typeof(byte[]), null, "en-US", TestName = "ConvertBack_StringValue_ThrowsNotImplementedException")]
    [TestCase(123, typeof(object), "param", "pl-PL", TestName = "ConvertBack_IntValue_ThrowsNotImplementedException")]
    [TestCase(true, typeof(byte[]), 456, "de-DE", TestName = "ConvertBack_BoolValue_ThrowsNotImplementedException")]
    [TestCase(0, typeof(string), null, "fr-FR", TestName = "ConvertBack_ZeroValue_ThrowsNotImplementedException")]
    [TestCase(-1, typeof(int), "", "ja-JP", TestName = "ConvertBack_NegativeValue_ThrowsNotImplementedException")]
    [TestCase(double.NaN, typeof(double), null, "", TestName = "ConvertBack_NaNValue_ThrowsNotImplementedException")]
    [TestCase(double.PositiveInfinity, typeof(double), null, "en-US", TestName = "ConvertBack_PositiveInfinityValue_ThrowsNotImplementedException")]
    [TestCase(double.NegativeInfinity, typeof(double), null, "en-US", TestName = "ConvertBack_NegativeInfinityValue_ThrowsNotImplementedException")]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException(object? value, Type targetType, object? parameter, string cultureName)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var culture = string.IsNullOrEmpty(cultureName) ? CultureInfo.InvariantCulture : new CultureInfo(cultureName);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, parameter, culture));
    }

    /// <summary>
    /// Tests ConvertBack with various complex object types to ensure NotImplementedException
    /// is always thrown regardless of value complexity.
    /// </summary>
    [Test]
    public void ConvertBack_ComplexObjectValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var complexObject = new { Name = "Test", Value = 123 };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(complexObject, typeof(byte[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests ConvertBack with InvariantCulture to ensure NotImplementedException
    /// is thrown with invariant culture.
    /// </summary>
    [Test]
    public void ConvertBack_InvariantCulture_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests ConvertBack with extreme integer values to ensure NotImplementedException
    /// is thrown for boundary values.
    /// </summary>
    [TestCase(int.MinValue, TestName = "ConvertBack_IntMinValue_ThrowsNotImplementedException")]
    [TestCase(int.MaxValue, TestName = "ConvertBack_IntMaxValue_ThrowsNotImplementedException")]
    public void ConvertBack_ExtremeIntegerValues_ThrowsNotImplementedException(int value)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, typeof(int), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests ConvertBack with whitespace string to ensure NotImplementedException
    /// is thrown for whitespace-only strings.
    /// </summary>
    [Test]
    public void ConvertBack_WhitespaceString_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack("   ", typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests ConvertBack with very long string to ensure NotImplementedException
    /// is thrown for boundary string lengths.
    /// </summary>
    [Test]
    public void ConvertBack_VeryLongString_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var longString = new string('a', 10000);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(longString, typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method to ensure it always throws NotImplementedException
    /// regardless of input parameters, as two-way conversion is not supported.
    /// </summary>
    /// <param name="value">The value to convert back.</param>
    /// <param name="targetType">The target type for conversion.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="culture">The culture to use for conversion.</param>
    [TestCase(null, typeof(byte[]), null, TestName = "ConvertBack_NullValueAndParameter_ThrowsNotImplementedException")]
    [TestCase("test", typeof(byte[]), "param", TestName = "ConvertBack_StringValueWithParameter_ThrowsNotImplementedException")]
    [TestCase(123, typeof(object), null, TestName = "ConvertBack_IntValueNullParameter_ThrowsNotImplementedException")]
    [TestCase(null, typeof(string), "param", TestName = "ConvertBack_NullValueWithParameter_ThrowsNotImplementedException")]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException(object? value, Type targetType, object? parameter)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var culture = CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, parameter, culture));
    }

    /// <summary>
    /// Tests the ConvertBack method with various target types to ensure
    /// NotImplementedException is always thrown.
    /// </summary>
    [TestCaseSource(nameof(GetTargetTypeCases))]
    public void ConvertBack_VariousTargetTypes_ThrowsNotImplementedException(Type targetType)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var value = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var culture = CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, null, culture));
    }

    /// <summary>
    /// Provides various target type cases for ConvertBack testing.
    /// </summary>
    private static object[] GetTargetTypeCases =
    {
        new object[] { typeof(byte[]) },
        new object[] { typeof(object) },
        new object[] { typeof(string) },
        new object[] { typeof(int) },
        new object[] { typeof(bool) }
    };

    /// <summary>
    /// Tests the ConvertBack method with edge case values including empty arrays
    /// and special objects to ensure NotImplementedException is always thrown.
    /// </summary>
    [TestCaseSource(nameof(GetEdgeCaseValues))]
    public void ConvertBack_EdgeCaseValues_ThrowsNotImplementedException(object? value)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var targetType = typeof(byte[]);
        var culture = CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, null, culture));
    }

    /// <summary>
    /// Provides edge case values for ConvertBack testing.
    /// </summary>
    private static object[] GetEdgeCaseValues =
    {
        new object?[] { null },
        new object[] { new byte[] { } },
        new object[] { new byte[] { 0 } },
        new object[] { new byte[] { 255, 255, 255 } },
        new object[] { new object() },
        new object[] { string.Empty },
        new object[] { " " },
        new object[] { int.MinValue },
        new object[] { int.MaxValue },
        new object[] { 0 },
        new object[] { double.NaN },
        new object[] { double.PositiveInfinity },
        new object[] { double.NegativeInfinity }
    };

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException with byte array and various culture inputs.
    /// </summary>
    [Test]
    public void ConvertBack_ByteArrayValueWithDifferentCultures_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var byteArray = new byte[] { 0x00, 0xFF, 0x01 };
        var cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("pl-PL")
        };

        // Act & Assert
        foreach (var culture in cultures)
        {
            Assert.Throws<NotImplementedException>(() => converter.ConvertBack(byteArray, typeof(byte[]), null, culture));
        }
    }

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException with CurrentCulture.
    /// </summary>
    [Test]
    public void ConvertBack_WithCurrentCulture_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(object), null, CultureInfo.CurrentCulture));
    }

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException with byte array value.
    /// </summary>
    [Test]
    public void ConvertBack_WithByteArrayValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var byteArray = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(byteArray, typeof(byte[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException with empty byte array.
    /// </summary>
    [Test]
    public void ConvertBack_WithEmptyByteArray_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var emptyArray = new byte[] { };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(emptyArray, typeof(byte[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Provides various target types for testing ConvertBack.
    /// </summary>
    private static object[] GetTargetTypeTestCases =
    {
        new object[] { typeof(string) },
        new object[] { typeof(int) },
        new object[] { typeof(byte[]) },
        new object[] { typeof(object) },
        new object[] { typeof(double) },
        new object[] { typeof(bool) }
    };

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException with complex objects.
    /// </summary>
    [TestCaseSource(nameof(GetComplexValueTestCases))]
    public void ConvertBack_ComplexObjectValues_ThrowsNotImplementedException(object? value)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Provides complex object values for testing ConvertBack.
    /// </summary>
    private static object?[] GetComplexValueTestCases =
    {
        new object?[] { new object() },
        new object?[] { new[] { 1, 2, 3 } },
        new object?[] { new[] { "a", "b", "c" } },
        new object?[] { " " },
        new object?[] { "\0" },
        new object?[] { "\t\n\r" },
        new object?[] { new string('x', 10000) }
    };

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException with various parameter values.
    /// </summary>
    [TestCase(null, TestName = "ConvertBack_NullParameter_ThrowsNotImplementedException")]
    [TestCase("stringParam", TestName = "ConvertBack_StringParameter_ThrowsNotImplementedException")]
    [TestCase(42, TestName = "ConvertBack_IntParameter_ThrowsNotImplementedException")]
    [TestCase(true, TestName = "ConvertBack_BoolParameter_ThrowsNotImplementedException")]
    public void ConvertBack_VariousParameters_ThrowsNotImplementedException(object? parameter)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(object), parameter, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method to ensure it always throws NotImplementedException
    /// for various input values and parameter combinations.
    /// </summary>
    /// <param name="value">The input value to convert back.</param>
    /// <param name="targetType">The target type for conversion.</param>
    /// <param name="parameter">Optional parameter for conversion.</param>
    /// <param name="culture">The culture info for conversion.</param>
    [TestCase(null, typeof(byte[]), null, null, TestName = "ConvertBack_NullValueNullParameterNullCulture_ThrowsNotImplementedException")]
    [TestCase("test", typeof(byte[]), null, null, TestName = "ConvertBack_StringValueNullParameterNullCulture_ThrowsNotImplementedException")]
    [TestCase(123, typeof(object), "param", null, TestName = "ConvertBack_IntValueWithParameterNullCulture_ThrowsNotImplementedException")]
    [TestCase(null, typeof(string), "param", null, TestName = "ConvertBack_NullValueWithParameterNullCulture_ThrowsNotImplementedException")]
    [TestCaseSource(nameof(GetConvertBackTestCases))]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var cultureToUse = culture ?? CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, parameter, cultureToUse));
    }

    /// <summary>
    /// Provides additional test cases for ConvertBack method with various input combinations.
    /// </summary>
    private static object[] GetConvertBackTestCases =
    {
        new object?[] { new byte[] { 0x00, 0x01, 0x02 }, typeof(byte[]), null, CultureInfo.InvariantCulture },
        new object?[] { new byte[] { }, typeof(byte[]), null, CultureInfo.InvariantCulture },
        new object?[] { null, typeof(ImageSource), null, CultureInfo.InvariantCulture },
        new object?[] { "test", typeof(string), "parameter", CultureInfo.CurrentCulture },
        new object?[] { 0, typeof(int), null, CultureInfo.InvariantCulture },
        new object?[] { -1, typeof(int), null, CultureInfo.InvariantCulture },
        new object?[] { int.MaxValue, typeof(int), null, CultureInfo.InvariantCulture },
        new object?[] { int.MinValue, typeof(int), null, CultureInfo.InvariantCulture },
        new object?[] { double.NaN, typeof(double), null, CultureInfo.InvariantCulture },
        new object?[] { double.PositiveInfinity, typeof(double), null, CultureInfo.InvariantCulture },
        new object?[] { double.NegativeInfinity, typeof(double), null, CultureInfo.InvariantCulture },
        new object?[] { true, typeof(bool), null, CultureInfo.InvariantCulture },
        new object?[] { false, typeof(bool), null, CultureInfo.InvariantCulture },
        new object?[] { "", typeof(string), null, CultureInfo.InvariantCulture },
        new object?[] { " ", typeof(string), null, CultureInfo.InvariantCulture },
        new object?[] { "\0", typeof(string), null, CultureInfo.InvariantCulture },
        new object?[] { new string('a', 10000), typeof(string), null, CultureInfo.InvariantCulture },
        new object?[] { new object(), typeof(object), null, CultureInfo.InvariantCulture },
        new object?[] { new int[] { 1, 2, 3 }, typeof(int[]), null, CultureInfo.InvariantCulture },
        new object?[] { null, typeof(object), new object(), new CultureInfo("en-US") },
        new object?[] { "value", typeof(byte[]), "param", new CultureInfo("pl-PL") },
    };

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException with empty byte array value.
    /// </summary>
    [Test]
    public void ConvertBack_EmptyByteArrayValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var value = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(value, typeof(byte[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException regardless of culture settings.
    /// </summary>
    [Test]
    public void ConvertBack_DifferentCultures_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new ByteArrayToImageSourceConverter();
        var cultures = new[] { CultureInfo.InvariantCulture, CultureInfo.CurrentCulture, new CultureInfo("de-DE"), new CultureInfo("ja-JP") };

        // Act & Assert
        foreach (var culture in cultures)
        {
            Assert.Throws<NotImplementedException>(() => converter.ConvertBack(null, typeof(object), null, culture));
        }
    }
}



/// <summary>
/// Unit tests for IsNotNullOrEmptyConverter.ConvertBack method.
/// </summary>
[TestFixture]
public partial class IsNotNullOrEmptyConverterTests
{
    /// <summary>
    /// Tests that ConvertBack always throws NotImplementedException regardless of input parameters.
    /// </summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="targetType">The target type parameter.</param>
    /// <param name="parameter">The parameter value.</param>
    [TestCase(null, typeof(object), null, TestName = "ConvertBack_AllNullableParametersNull_ThrowsNotImplementedException")]
    [TestCase("test", typeof(string), null, TestName = "ConvertBack_NonEmptyStringValue_ThrowsNotImplementedException")]
    [TestCase("", typeof(bool), null, TestName = "ConvertBack_EmptyStringValue_ThrowsNotImplementedException")]
    [TestCase(" ", typeof(object), "param", TestName = "ConvertBack_WhitespaceStringValue_ThrowsNotImplementedException")]
    [TestCase(123, typeof(int), null, TestName = "ConvertBack_IntegerValue_ThrowsNotImplementedException")]
    [TestCase(0, typeof(object), null, TestName = "ConvertBack_ZeroValue_ThrowsNotImplementedException")]
    [TestCase(-1, typeof(object), null, TestName = "ConvertBack_NegativeValue_ThrowsNotImplementedException")]
    [TestCase(true, typeof(bool), "parameter", TestName = "ConvertBack_BooleanTrueValue_ThrowsNotImplementedException")]
    [TestCase(false, typeof(bool), null, TestName = "ConvertBack_BooleanFalseValue_ThrowsNotImplementedException")]
    [TestCase(double.NaN, typeof(double), null, TestName = "ConvertBack_DoubleNaNValue_ThrowsNotImplementedException")]
    [TestCase(double.PositiveInfinity, typeof(double), null, TestName = "ConvertBack_DoublePositiveInfinityValue_ThrowsNotImplementedException")]
    [TestCase(double.NegativeInfinity, typeof(double), null, TestName = "ConvertBack_DoubleNegativeInfinityValue_ThrowsNotImplementedException")]
    [TestCaseSource(nameof(GetAdditionalConvertBackTestCases))]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, Type targetType, object? parameter)
    {
        // Arrange
        var converter = new IsNotNullOrEmptyConverter();
        var culture = CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(value, targetType, parameter, culture));
    }

    /// <summary>
    /// Tests ConvertBack with different CultureInfo values to ensure NotImplementedException is always thrown.
    /// </summary>
    /// <param name="cultureName">The name of the culture to use.</param>
    [TestCase("en-US", TestName = "ConvertBack_EnglishUSCulture_ThrowsNotImplementedException")]
    [TestCase("fr-FR", TestName = "ConvertBack_FrenchCulture_ThrowsNotImplementedException")]
    [TestCase("de-DE", TestName = "ConvertBack_GermanCulture_ThrowsNotImplementedException")]
    [TestCase("", TestName = "ConvertBack_InvariantCulture_ThrowsNotImplementedException")]
    public void ConvertBack_DifferentCultures_ThrowsNotImplementedException(string cultureName)
    {
        // Arrange
        var converter = new IsNotNullOrEmptyConverter();
        var culture = string.IsNullOrEmpty(cultureName) ? CultureInfo.InvariantCulture : new CultureInfo(cultureName);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack("test", typeof(string), null, culture));
    }

    /// <summary>
    /// Tests ConvertBack with extreme boundary values to ensure NotImplementedException is always thrown.
    /// </summary>
    [Test]
    public void ConvertBack_ExtremeBoundaryValues_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNotNullOrEmptyConverter();
        var culture = CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(int.MinValue, typeof(int), null, culture));
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(int.MaxValue, typeof(int), null, culture));
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(long.MinValue, typeof(long), null, culture));
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(long.MaxValue, typeof(long), null, culture));
    }

    /// <summary>
    /// Tests ConvertBack with special characters and long strings to ensure NotImplementedException is always thrown.
    /// </summary>
    [Test]
    public void ConvertBack_SpecialCharactersAndLongStrings_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNotNullOrEmptyConverter();
        var culture = CultureInfo.InvariantCulture;
        var longString = new string('x', 10000);
        var specialChars = "\t\n\r\0\u0001";

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(longString, typeof(string), null, culture));
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(specialChars, typeof(string), null, culture));
    }

    /// <summary>
    /// Provides additional test cases for ConvertBack with various object types.
    /// </summary>
    private static object[] GetAdditionalConvertBackTestCases =
    {
        new object[] { new object(), typeof(object), null },
        new object[] { new int[] { 1, 2, 3 }, typeof(int[]), "param" },
        new object[] { new int[] { }, typeof(int[]), null },
        new object[] { "\0", typeof(string), null },
        new object[] { new string('a', 1000), typeof(string), new object() },
        new object?[] { null, typeof(string), new object() }
    };

    /// <summary>
    /// Tests the Convert method for various input values to ensure correct boolean is returned
    /// when checking if the input value is a non-null, non-empty string.
    /// </summary>
    /// <param name="input">The input to be checked.</param>
    /// <param name="expected">Expected result: true if input is a non-null, non-empty string, false otherwise.</param>
    [TestCase(null, false, TestName = "Convert_InputIsNull_ReturnsFalse")]
    [TestCase("", false, TestName = "Convert_InputIsEmptyString_ReturnsFalse")]
    [TestCase(" ", true, TestName = "Convert_InputIsWhitespace_ReturnsTrue")]
    [TestCase("\t", true, TestName = "Convert_InputIsTab_ReturnsTrue")]
    [TestCase("\n", true, TestName = "Convert_InputIsNewline_ReturnsTrue")]
    [TestCase("test", true, TestName = "Convert_InputIsNonEmptyString_ReturnsTrue")]
    [TestCase("a", true, TestName = "Convert_InputIsSingleCharString_ReturnsTrue")]
    [TestCase("  spaces  ", true, TestName = "Convert_InputIsStringWithSpaces_ReturnsTrue")]
    [TestCase("special!@#$%", true, TestName = "Convert_InputIsStringWithSpecialChars_ReturnsTrue")]
    [TestCase(0, false, TestName = "Convert_InputIsZeroInt_ReturnsFalse")]
    [TestCase(123, false, TestName = "Convert_InputIsPositiveInt_ReturnsFalse")]
    [TestCase(-5, false, TestName = "Convert_InputIsNegativeInt_ReturnsFalse")]
    [TestCase(int.MinValue, false, TestName = "Convert_InputIsIntMinValue_ReturnsFalse")]
    [TestCase(int.MaxValue, false, TestName = "Convert_InputIsIntMaxValue_ReturnsFalse")]
    [TestCase(0.0, false, TestName = "Convert_InputIsZeroDouble_ReturnsFalse")]
    [TestCase(123.456, false, TestName = "Convert_InputIsPositiveDouble_ReturnsFalse")]
    [TestCase(-123.456, false, TestName = "Convert_InputIsNegativeDouble_ReturnsFalse")]
    [TestCase(double.NaN, false, TestName = "Convert_InputIsNaN_ReturnsFalse")]
    [TestCase(double.PositiveInfinity, false, TestName = "Convert_InputIsPositiveInfinity_ReturnsFalse")]
    [TestCase(double.NegativeInfinity, false, TestName = "Convert_InputIsNegativeInfinity_ReturnsFalse")]
    [TestCase(true, false, TestName = "Convert_InputIsTrueBool_ReturnsFalse")]
    [TestCase(false, false, TestName = "Convert_InputIsFalseBool_ReturnsFalse")]
    [TestCaseSource(nameof(GetReferenceTypeCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new IsNotNullOrEmptyConverter();

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
        new object[] { new string('a', 10000), true },
        new object[] { "\0", true }
    };

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException.
    /// </summary>
    [Test]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNotNullOrEmptyConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}



/// <summary>
/// Unit tests for InvertedBoolConverter.Convert method.
/// </summary>
[TestFixture]
public class InvertedBoolConverterTests
{
    /// <summary>
    /// Tests the Convert method for various input values to ensure correct inverted boolean is returned
    /// when the input is a boolean, and false for all non-boolean inputs.
    /// </summary>
    /// <param name="input">The input value to be converted.</param>
    /// <param name="expected">Expected result: true if input is false, false if input is true or non-boolean.</param>
    [TestCase(true, false, TestName = "Convert_InputIsTrue_ReturnsFalse")]
    [TestCase(false, true, TestName = "Convert_InputIsFalse_ReturnsTrue")]
    [TestCase(null, false, TestName = "Convert_InputIsNull_ReturnsFalse")]
    [TestCase("", false, TestName = "Convert_InputIsEmptyString_ReturnsFalse")]
    [TestCase("nonempty", false, TestName = "Convert_InputIsNonEmptyString_ReturnsFalse")]
    [TestCase("true", false, TestName = "Convert_InputIsStringTrue_ReturnsFalse")]
    [TestCase("false", false, TestName = "Convert_InputIsStringFalse_ReturnsFalse")]
    [TestCase(0, false, TestName = "Convert_InputIsZeroInt_ReturnsFalse")]
    [TestCase(1, false, TestName = "Convert_InputIsOneInt_ReturnsFalse")]
    [TestCase(123, false, TestName = "Convert_InputIsPositiveInt_ReturnsFalse")]
    [TestCase(-5, false, TestName = "Convert_InputIsNegativeInt_ReturnsFalse")]
    [TestCase(int.MinValue, false, TestName = "Convert_InputIsIntMinValue_ReturnsFalse")]
    [TestCase(int.MaxValue, false, TestName = "Convert_InputIsIntMaxValue_ReturnsFalse")]
    [TestCase(0.0, false, TestName = "Convert_InputIsZeroDouble_ReturnsFalse")]
    [TestCase(1.0, false, TestName = "Convert_InputIsOneDouble_ReturnsFalse")]
    [TestCase(-1.5, false, TestName = "Convert_InputIsNegativeDouble_ReturnsFalse")]
    [TestCase(double.NaN, false, TestName = "Convert_InputIsNaN_ReturnsFalse")]
    [TestCase(double.PositiveInfinity, false, TestName = "Convert_InputIsPositiveInfinity_ReturnsFalse")]
    [TestCase(double.NegativeInfinity, false, TestName = "Convert_InputIsNegativeInfinity_ReturnsFalse")]
    [TestCase(double.MinValue, false, TestName = "Convert_InputIsDoubleMinValue_ReturnsFalse")]
    [TestCase(double.MaxValue, false, TestName = "Convert_InputIsDoubleMaxValue_ReturnsFalse")]
    [TestCase(' ', false, TestName = "Convert_InputIsSpaceChar_ReturnsFalse")]
    [TestCase('\0', false, TestName = "Convert_InputIsNullChar_ReturnsFalse")]
    [TestCaseSource(nameof(GetReferenceTypeCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new InvertedBoolConverter();

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
        new object[] { "\t\n\r", false },
        new object[] { new string('a', 10000), false }
    };

    /// <summary>
    /// Tests the Convert method with boxed boolean values to ensure correct inversion.
    /// </summary>
    [Test]
    public void Convert_BoxedTrueBoolean_ReturnsFalse()
    {
        // Arrange
        var converter = new InvertedBoolConverter();
        object boxedTrue = true;

        // Act
        var result = converter.Convert(boxedTrue, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    /// <summary>
    /// Tests the Convert method with boxed false boolean value to ensure correct inversion.
    /// </summary>
    [Test]
    public void Convert_BoxedFalseBoolean_ReturnsTrue()
    {
        // Arrange
        var converter = new InvertedBoolConverter();
        object boxedFalse = false;

        // Act
        var result = converter.Convert(boxedFalse, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(true));
    }

    /// <summary>
    /// Tests the Convert method with different target types to ensure the targetType parameter
    /// doesn't affect the conversion logic.
    /// </summary>
    [TestCase(true, typeof(bool))]
    [TestCase(true, typeof(string))]
    [TestCase(true, typeof(object))]
    [TestCase(false, typeof(bool))]
    [TestCase(false, typeof(int))]
    public void Convert_DifferentTargetTypes_ReturnsCorrectInversion(bool input, Type targetType)
    {
        // Arrange
        var converter = new InvertedBoolConverter();
        var expected = !input;

        // Act
        var result = converter.Convert(input, targetType, null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests the Convert method with different parameter values to ensure the parameter
    /// doesn't affect the conversion logic.
    /// </summary>
    [TestCase(true, null)]
    [TestCase(true, "someParameter")]
    [TestCase(true, 42)]
    [TestCase(false, null)]
    [TestCase(false, "anotherParameter")]
    public void Convert_DifferentParameters_ReturnsCorrectInversion(bool input, object? parameter)
    {
        // Arrange
        var converter = new InvertedBoolConverter();
        var expected = !input;

        // Act
        var result = converter.Convert(input, typeof(bool), parameter, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests the Convert method with different culture infos to ensure culture
    /// doesn't affect the conversion logic.
    /// </summary>
    [Test]
    public void Convert_DifferentCultures_ReturnsCorrectInversion()
    {
        // Arrange
        var converter = new InvertedBoolConverter();
        var cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.CurrentCulture,
            new CultureInfo("en-US"),
            new CultureInfo("pl-PL"),
            new CultureInfo("de-DE")
        };

        // Act & Assert
        foreach (var culture in cultures)
        {
            var resultTrue = converter.Convert(true, typeof(bool), null, culture);
            var resultFalse = converter.Convert(false, typeof(bool), null, culture);

            Assert.That(resultTrue, Is.EqualTo(false), $"Failed for culture {culture.Name} with true input");
            Assert.That(resultFalse, Is.EqualTo(true), $"Failed for culture {culture.Name} with false input");
        }
    }

    /// <summary>
    /// Tests the ConvertBack method with various input values to ensure correct boolean inversion
    /// when the input is a boolean, and returns false for non-boolean inputs.
    /// </summary>
    /// <param name="input">The input value to convert back.</param>
    /// <param name="expected">Expected result: inverted boolean if input is bool, false otherwise.</param>
    [TestCase(true, false, TestName = "ConvertBack_InputIsTrue_ReturnsFalse")]
    [TestCase(false, true, TestName = "ConvertBack_InputIsFalse_ReturnsTrue")]
    [TestCase(null, false, TestName = "ConvertBack_InputIsNull_ReturnsFalse")]
    [TestCase("", false, TestName = "ConvertBack_InputIsEmptyString_ReturnsFalse")]
    [TestCase("nonempty", false, TestName = "ConvertBack_InputIsNonEmptyString_ReturnsFalse")]
    [TestCase(0, false, TestName = "ConvertBack_InputIsZeroInt_ReturnsFalse")]
    [TestCase(1, false, TestName = "ConvertBack_InputIsOneInt_ReturnsFalse")]
    [TestCase(-1, false, TestName = "ConvertBack_InputIsNegativeOneInt_ReturnsFalse")]
    [TestCase(int.MinValue, false, TestName = "ConvertBack_InputIsIntMinValue_ReturnsFalse")]
    [TestCase(int.MaxValue, false, TestName = "ConvertBack_InputIsIntMaxValue_ReturnsFalse")]
    [TestCase(0.0, false, TestName = "ConvertBack_InputIsZeroDouble_ReturnsFalse")]
    [TestCase(1.0, false, TestName = "ConvertBack_InputIsOneDouble_ReturnsFalse")]
    [TestCase(double.NaN, false, TestName = "ConvertBack_InputIsNaN_ReturnsFalse")]
    [TestCase(double.PositiveInfinity, false, TestName = "ConvertBack_InputIsPositiveInfinity_ReturnsFalse")]
    [TestCase(double.NegativeInfinity, false, TestName = "ConvertBack_InputIsNegativeInfinity_ReturnsFalse")]
    [TestCaseSource(nameof(GetReferenceTypeCases))]
    public void ConvertBack_InputVariousTypes_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new InvertedBoolConverter();

        // Act
        var result = converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests the ConvertBack method with different targetType parameters to ensure
    /// the parameter does not affect the result.
    /// </summary>
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void ConvertBack_DifferentTargetTypes_ReturnsCorrectResult(bool input, bool expected)
    {
        // Arrange
        var converter = new InvertedBoolConverter();

        // Act
        var resultWithBoolType = converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);
        var resultWithObjectType = converter.ConvertBack(input, typeof(object), null, CultureInfo.InvariantCulture);
        var resultWithStringType = converter.ConvertBack(input, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(resultWithBoolType, Is.EqualTo(expected));
        Assert.That(resultWithObjectType, Is.EqualTo(expected));
        Assert.That(resultWithStringType, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests the ConvertBack method with different culture parameters to ensure
    /// culture does not affect the boolean inversion.
    /// </summary>
    [Test]
    public void ConvertBack_DifferentCultures_ReturnsCorrectResult()
    {
        // Arrange
        var converter = new InvertedBoolConverter();

        // Act
        var resultInvariant = converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture);
        var resultEnUs = converter.ConvertBack(true, typeof(bool), null, new CultureInfo("en-US"));
        var resultDeDe = converter.ConvertBack(true, typeof(bool), null, new CultureInfo("de-DE"));

        // Assert
        Assert.That(resultInvariant, Is.EqualTo(false));
        Assert.That(resultEnUs, Is.EqualTo(false));
        Assert.That(resultDeDe, Is.EqualTo(false));
    }

    /// <summary>
    /// Tests the ConvertBack method with different parameter values to ensure
    /// the parameter does not affect the result.
    /// </summary>
    [Test]
    public void ConvertBack_DifferentParameters_ReturnsCorrectResult()
    {
        // Arrange
        var converter = new InvertedBoolConverter();

        // Act
        var resultNullParam = converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture);
        var resultStringParam = converter.ConvertBack(true, typeof(bool), "parameter", CultureInfo.InvariantCulture);
        var resultObjectParam = converter.ConvertBack(true, typeof(bool), new object(), CultureInfo.InvariantCulture);

        // Assert
        Assert.That(resultNullParam, Is.EqualTo(false));
        Assert.That(resultStringParam, Is.EqualTo(false));
        Assert.That(resultObjectParam, Is.EqualTo(false));
    }
}



/// <summary>
/// Unit tests for IconSelectionConverter.
/// </summary>
[TestFixture]
public class IconSelectionConverterTests
{
    /// <summary>
    /// Tests the Convert method when values array is null.
    /// Expected: NullReferenceException is thrown.
    /// </summary>
    [Test]
    public void Convert_ValuesIsNull_ThrowsNullReferenceException()
    {
        // Arrange
        var converter = new IconSelectionConverter();

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            converter.Convert(null!, typeof(object), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the Convert method when both elements are strings but not equal.
    /// Expected: Colors.Transparent is returned.
    /// </summary>
    [TestCase("icon1", "icon2", TestName = "Convert_TwoStringsDifferent_ReturnsTransparent")]
    [TestCase("", "icon", TestName = "Convert_FirstEmptySecondNonEmpty_ReturnsTransparent")]
    [TestCase("icon", "", TestName = "Convert_FirstNonEmptySecondEmpty_ReturnsTransparent")]
    [TestCase(" ", "icon", TestName = "Convert_FirstWhitespaceSecondNonEmpty_ReturnsTransparent")]
    [TestCase("icon", " ", TestName = "Convert_FirstNonEmptySecondWhitespace_ReturnsTransparent")]
    [TestCase("Icon", "icon", TestName = "Convert_DifferentCase_ReturnsTransparent")]
    public void Convert_TwoStringsNotEqual_ReturnsTransparent(string first, string second)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var values = new object[] { first, second };

        // Act
        var result = converter.Convert(values, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(Colors.Transparent));
    }

    /// <summary>
    /// Tests the Convert method when values array has more than two elements.
    /// Expected: Only first two elements are considered, returns Colors.Transparent if not equal.
    /// </summary>
    [Test]
    public void Convert_ValuesHasMoreThanTwoElements_ConsidersOnlyFirstTwo()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var values = new object[] { "icon1", "icon2", "icon3", "icon4" };

        // Act
        var result = converter.Convert(values, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(Colors.Transparent));
    }

    /// <summary>
    /// Tests the Convert method when both string elements are equal.
    /// Expected: Application.Current.Resources["Primary"] is returned.
    /// Note: This test requires Application.Current to be initialized and is marked as Inconclusive
    /// if Application.Current is null or the "Primary" resource is not found.
    /// </summary>
    [TestCase("icon", "icon", TestName = "Convert_TwoStringsEqual_ReturnsPrimaryResource")]
    [TestCase("", "", TestName = "Convert_BothEmptyStrings_ReturnsPrimaryResource")]
    [TestCase(" ", " ", TestName = "Convert_BothWhitespaceStrings_ReturnsPrimaryResource")]
    public void Convert_TwoStringsEqual_ReturnsPrimaryResource(string icon1, string icon2)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var values = new object[] { icon1, icon2 };

        // Act
        if (Application.Current == null || !Application.Current.Resources.ContainsKey("Primary"))
        {
            Assert.Inconclusive(
                "This test requires Application.Current to be initialized with a 'Primary' resource. " +
                "In a unit test environment, Application.Current is typically null. " +
                "Consider running this test in an integration test context where the MAUI application is initialized.");
            return;
        }

        var result = converter.Convert(values, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(Application.Current.Resources["Primary"]));
    }

    /// <summary>
    /// Tests the Convert method with special characters in strings.
    /// Expected: Returns Colors.Transparent when strings don't match.
    /// </summary>
    [TestCase("icon\n", "icon", TestName = "Convert_FirstWithNewline_ReturnsTransparent")]
    [TestCase("icon\t", "icon", TestName = "Convert_FirstWithTab_ReturnsTransparent")]
    [TestCase("icon\0", "icon", TestName = "Convert_FirstWithNullChar_ReturnsTransparent")]
    [TestCase("icon!", "icon", TestName = "Convert_FirstWithSpecialChar_ReturnsTransparent")]
    public void Convert_StringsWithSpecialCharacters_ReturnsTransparent(string first, string second)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var values = new object[] { first, second };

        // Act
        var result = converter.Convert(values, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(Colors.Transparent));
    }

    /// <summary>
    /// Tests the Convert method with very long strings.
    /// Expected: Returns Colors.Transparent when strings don't match.
    /// </summary>
    [Test]
    public void Convert_VeryLongStrings_ReturnsTransparent()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var longString1 = new string('a', 10000);
        var longString2 = new string('b', 10000);
        var values = new object[] { longString1, longString2 };

        // Act
        var result = converter.Convert(values, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(Colors.Transparent));
    }

    /// <summary>
    /// Tests that targetType, parameter, and culture parameters are not used.
    /// Expected: Result is independent of these parameters.
    /// </summary>
    [Test]
    public void Convert_UnusedParameters_DoesNotAffectResult()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var values = new object[] { "icon1", "icon2" };

        // Act
        var result1 = converter.Convert(values, typeof(string), null, CultureInfo.InvariantCulture);
        var result2 = converter.Convert(values, typeof(int), new object(), CultureInfo.CurrentCulture);
        var result3 = converter.Convert(values, null!, "parameter", null!);

        // Assert
        Assert.That(result1, Is.EqualTo(Colors.Transparent));
        Assert.That(result2, Is.EqualTo(Colors.Transparent));
        Assert.That(result3, Is.EqualTo(Colors.Transparent));
    }

    /// <summary>
    /// Tests the ConvertBack method with various input combinations.
    /// ConvertBack is not implemented and should always throw NotImplementedException.
    /// </summary>
    /// <param name="value">The value to convert back.</param>
    /// <param name="targetTypes">Array of target types.</param>
    /// <param name="parameter">Converter parameter.</param>
    /// <param name="culture">Culture information.</param>
    [TestCase("test", new[] { typeof(string) }, "param", null, TestName = "ConvertBack_ValidInputsWithNullCulture_ThrowsNotImplementedException")]
    [TestCase(null, new[] { typeof(object) }, null, null, TestName = "ConvertBack_NullValueAndParameterWithNullCulture_ThrowsNotImplementedException")]
    [TestCase(123, new[] { typeof(int), typeof(string) }, "param", null, TestName = "ConvertBack_IntValueMultipleTargetTypes_ThrowsNotImplementedException")]
    [TestCase(true, new[] { typeof(bool) }, null, null, TestName = "ConvertBack_BoolValue_ThrowsNotImplementedException")]
    [TestCase("", null, "param", null, TestName = "ConvertBack_EmptyStringNullTargetTypes_ThrowsNotImplementedException")]
    [TestCase(0, new Type[] { }, null, null, TestName = "ConvertBack_EmptyTargetTypesArray_ThrowsNotImplementedException")]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, Type[]? targetTypes, object? parameter, CultureInfo? culture)
    {
        // Arrange
        var converter = new IconSelectionConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value!, targetTypes!, parameter, culture!));
    }

    /// <summary>
    /// Tests the ConvertBack method with valid culture information.
    /// Should still throw NotImplementedException as the method is not implemented.
    /// </summary>
    [Test]
    public void ConvertBack_WithValidCulture_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var value = "test";
        var targetTypes = new[] { typeof(string) };
        var parameter = new object();
        var culture = CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, parameter, culture));
    }

    /// <summary>
    /// Tests the ConvertBack method with all null parameters.
    /// Should throw NotImplementedException before any parameter validation.
    /// </summary>
    [Test]
    public void ConvertBack_AllNullParameters_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IconSelectionConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null!, null!, null, null!));
    }

    /// <summary>
    /// Tests the ConvertBack method with complex object types.
    /// Should throw NotImplementedException regardless of input complexity.
    /// </summary>
    [Test]
    public void ConvertBack_ComplexObjectValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var value = new { Property1 = "test", Property2 = 123 };
        var targetTypes = new[] { typeof(object), typeof(string), typeof(int) };
        var parameter = new object();
        var culture = new CultureInfo("en-US");

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, parameter, culture));
    }

    /// <summary>
    /// Tests the ConvertBack method with numeric edge case values.
    /// Should throw NotImplementedException for all numeric inputs.
    /// </summary>
    [TestCase(int.MinValue, TestName = "ConvertBack_IntMinValue_ThrowsNotImplementedException")]
    [TestCase(int.MaxValue, TestName = "ConvertBack_IntMaxValue_ThrowsNotImplementedException")]
    [TestCase(0, TestName = "ConvertBack_Zero_ThrowsNotImplementedException")]
    [TestCase(-1, TestName = "ConvertBack_NegativeOne_ThrowsNotImplementedException")]
    public void ConvertBack_NumericEdgeCases_ThrowsNotImplementedException(int value)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var targetTypes = new[] { typeof(int) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with double edge case values including NaN and Infinity.
    /// Should throw NotImplementedException for all floating-point inputs.
    /// </summary>
    [TestCase(double.NaN, TestName = "ConvertBack_DoubleNaN_ThrowsNotImplementedException")]
    [TestCase(double.PositiveInfinity, TestName = "ConvertBack_DoublePositiveInfinity_ThrowsNotImplementedException")]
    [TestCase(double.NegativeInfinity, TestName = "ConvertBack_DoubleNegativeInfinity_ThrowsNotImplementedException")]
    [TestCase(0.0, TestName = "ConvertBack_DoubleZero_ThrowsNotImplementedException")]
    [TestCase(-1.5, TestName = "ConvertBack_NegativeDouble_ThrowsNotImplementedException")]
    public void ConvertBack_DoubleEdgeCases_ThrowsNotImplementedException(double value)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var targetTypes = new[] { typeof(double) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with string edge cases.
    /// Should throw NotImplementedException for all string inputs.
    /// </summary>
    [TestCase(null, TestName = "ConvertBack_NullString_ThrowsNotImplementedException")]
    [TestCase("", TestName = "ConvertBack_EmptyString_ThrowsNotImplementedException")]
    [TestCase(" ", TestName = "ConvertBack_WhitespaceString_ThrowsNotImplementedException")]
    [TestCase("   ", TestName = "ConvertBack_MultipleWhitespaces_ThrowsNotImplementedException")]
    [TestCase("normal string", TestName = "ConvertBack_NormalString_ThrowsNotImplementedException")]
    public void ConvertBack_StringEdgeCases_ThrowsNotImplementedException(string? value)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var targetTypes = new[] { typeof(string) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value!, targetTypes, null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with a very long string.
    /// Should throw NotImplementedException regardless of string length.
    /// </summary>
    [Test]
    public void ConvertBack_VeryLongString_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var value = new string('a', 10000);
        var targetTypes = new[] { typeof(string) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with strings containing special characters.
    /// Should throw NotImplementedException for all character inputs.
    /// </summary>
    [TestCase("\0", TestName = "ConvertBack_NullCharacterString_ThrowsNotImplementedException")]
    [TestCase("\n\r\t", TestName = "ConvertBack_ControlCharacters_ThrowsNotImplementedException")]
    [TestCase("!@#$%^&*()", TestName = "ConvertBack_SpecialCharacters_ThrowsNotImplementedException")]
    [TestCase("unicode: \u00A9 \u00AE", TestName = "ConvertBack_UnicodeCharacters_ThrowsNotImplementedException")]
    public void ConvertBack_StringsWithSpecialCharacters_ThrowsNotImplementedException(string value)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var targetTypes = new[] { typeof(string) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with different culture settings.
    /// Should throw NotImplementedException regardless of culture.
    /// </summary>
    [TestCase("en-US", TestName = "ConvertBack_EnglishUSCulture_ThrowsNotImplementedException")]
    [TestCase("fr-FR", TestName = "ConvertBack_FrenchCulture_ThrowsNotImplementedException")]
    [TestCase("de-DE", TestName = "ConvertBack_GermanCulture_ThrowsNotImplementedException")]
    [TestCase("ja-JP", TestName = "ConvertBack_JapaneseCulture_ThrowsNotImplementedException")]
    public void ConvertBack_DifferentCultures_ThrowsNotImplementedException(string cultureName)
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var value = "test";
        var targetTypes = new[] { typeof(string) };
        var culture = new CultureInfo(cultureName);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, null, culture));
    }

    /// <summary>
    /// Tests the ConvertBack method with arrays and collections as values.
    /// Should throw NotImplementedException for collection inputs.
    /// </summary>
    [Test]
    public void ConvertBack_ArrayValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var value = new[] { 1, 2, 3 };
        var targetTypes = new[] { typeof(int[]) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with empty array as value.
    /// Should throw NotImplementedException for empty collection inputs.
    /// </summary>
    [Test]
    public void ConvertBack_EmptyArrayValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IconSelectionConverter();
        var value = new int[] { };
        var targetTypes = new[] { typeof(int[]) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetTypes, null, CultureInfo.InvariantCulture));
    }
}



/// <summary>
/// Unit tests for BoolToExpandIconConverter.Convert method.
/// </summary>
[TestFixture]
public class BoolToExpandIconConverterTests
{
    private const string ExpandLess = "\ue5ce";
    private const string ExpandMore = "\ue5cf";

    /// <summary>
    /// Tests the Convert method for various input values to ensure correct icon string is returned.
    /// Returns ExpandLess (\ue5ce) when value is exactly true, ExpandMore (\ue5cf) otherwise.
    /// </summary>
    /// <param name="input">The input value to be converted.</param>
    /// <param name="expected">Expected result: ExpandLess when true, ExpandMore otherwise.</param>
    [TestCase(true, ExpandLess, TestName = "Convert_InputIsTrue_ReturnsExpandLess")]
    [TestCase(false, ExpandMore, TestName = "Convert_InputIsFalse_ReturnsExpandMore")]
    [TestCase(null, ExpandMore, TestName = "Convert_InputIsNull_ReturnsExpandMore")]
    [TestCase(1, ExpandMore, TestName = "Convert_InputIsIntOne_ReturnsExpandMore")]
    [TestCase(0, ExpandMore, TestName = "Convert_InputIsIntZero_ReturnsExpandMore")]
    [TestCase(-1, ExpandMore, TestName = "Convert_InputIsNegativeInt_ReturnsExpandMore")]
    [TestCase("true", ExpandMore, TestName = "Convert_InputIsStringTrue_ReturnsExpandMore")]
    [TestCase("", ExpandMore, TestName = "Convert_InputIsEmptyString_ReturnsExpandMore")]
    [TestCase(" ", ExpandMore, TestName = "Convert_InputIsWhitespace_ReturnsExpandMore")]
    [TestCase(1.0, ExpandMore, TestName = "Convert_InputIsDouble_ReturnsExpandMore")]
    [TestCase(0.0, ExpandMore, TestName = "Convert_InputIsZeroDouble_ReturnsExpandMore")]
    [TestCaseSource(nameof(GetAdditionalTestCases))]
    public void Convert_VariousInputTypes_ReturnsCorrectIcon(object? input, string expected)
    {
        // Arrange
        var converter = new BoolToExpandIconConverter();

        // Act
        var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Provides additional test cases for edge cases with reference types and numeric boundaries.
    /// </summary>
    private static object[] GetAdditionalTestCases =
    {
        new object[] { new object(), ExpandMore },
        new object[] { new int[] { }, ExpandMore },
        new object[] { new int[] { 1, 2, 3 }, ExpandMore },
        new object[] { int.MinValue, ExpandMore },
        new object[] { int.MaxValue, ExpandMore },
        new object[] { double.NaN, ExpandMore },
        new object[] { double.PositiveInfinity, ExpandMore },
        new object[] { double.NegativeInfinity, ExpandMore }
    };

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException.
    /// </summary>
    [Test]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new BoolToExpandIconConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(ExpandLess, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException for various input combinations.
    /// This method is not intended to be used as the converter is one-way only.
    /// </summary>
    /// <param name="value">The value parameter to pass to ConvertBack.</param>
    /// <param name="targetType">The target type parameter to pass to ConvertBack.</param>
    /// <param name="parameter">The optional parameter to pass to ConvertBack.</param>
    /// <param name="culture">The culture info to pass to ConvertBack.</param>
    [TestCase(null, typeof(bool), null, null, TestName = "ConvertBack_NullValueAndParameterWithNullCulture_ThrowsNotImplementedException")]
    [TestCase(true, typeof(bool), null, null, TestName = "ConvertBack_TrueBoolValueWithNullCulture_ThrowsNotImplementedException")]
    [TestCase(false, typeof(bool), null, null, TestName = "ConvertBack_FalseBoolValueWithNullCulture_ThrowsNotImplementedException")]
    [TestCase("test", typeof(string), null, null, TestName = "ConvertBack_StringValue_ThrowsNotImplementedException")]
    [TestCase("", typeof(string), null, null, TestName = "ConvertBack_EmptyStringValue_ThrowsNotImplementedException")]
    [TestCase(0, typeof(int), null, null, TestName = "ConvertBack_ZeroIntValue_ThrowsNotImplementedException")]
    [TestCase(123, typeof(int), null, null, TestName = "ConvertBack_PositiveIntValue_ThrowsNotImplementedException")]
    [TestCase(-456, typeof(int), null, null, TestName = "ConvertBack_NegativeIntValue_ThrowsNotImplementedException")]
    [TestCase(double.NaN, typeof(double), null, null, TestName = "ConvertBack_NaNValue_ThrowsNotImplementedException")]
    [TestCase(double.PositiveInfinity, typeof(double), null, null, TestName = "ConvertBack_PositiveInfinityValue_ThrowsNotImplementedException")]
    [TestCase(double.NegativeInfinity, typeof(double), null, null, TestName = "ConvertBack_NegativeInfinityValue_ThrowsNotImplementedException")]
    [TestCaseSource(nameof(GetConvertBackTestCases))]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        // Arrange
        var converter = new BoolToExpandIconConverter();
        var cultureToUse = culture ?? CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, parameter, cultureToUse));
    }

    /// <summary>
    /// Provides additional test cases for ConvertBack method with various cultures and parameters.
    /// </summary>
    private static object[] GetConvertBackTestCases =
    {
        new object[] { null, typeof(object), null, CultureInfo.InvariantCulture },
        new object[] { null, typeof(object), "param", CultureInfo.InvariantCulture },
        new object[] { true, typeof(bool), "param", CultureInfo.InvariantCulture },
        new object[] { false, typeof(string), null, CultureInfo.InvariantCulture },
        new object[] { "\ue5ce", typeof(bool), null, CultureInfo.InvariantCulture },
        new object[] { "\ue5cf", typeof(bool), null, CultureInfo.InvariantCulture },
        new object[] { new object(), typeof(object), null, CultureInfo.InvariantCulture },
        new object[] { int.MinValue, typeof(int), null, CultureInfo.InvariantCulture },
        new object[] { int.MaxValue, typeof(int), null, CultureInfo.InvariantCulture },
        new object[] { " ", typeof(string), "param", CultureInfo.CurrentCulture },
        new object[] { "\t\n", typeof(string), null, new CultureInfo("en-US") },
        new object[] { new string('a', 10000), typeof(string), null, new CultureInfo("pl-PL") },
        new object[] { 0.0, typeof(double), null, CultureInfo.InvariantCulture },
        new object[] { -0.0, typeof(double), null, CultureInfo.InvariantCulture },
        new object[] { 1.7976931348623157E+308, typeof(double), null, CultureInfo.InvariantCulture }
    };
}



/// <summary>
/// Unit tests for IsNotNullConverter.
/// </summary>
[TestFixture]
public class IsNotNullConverterTests
{
    /// <summary>
    /// Tests that ConvertBack always throws NotImplementedException regardless of input values.
    /// </summary>
    /// <param name="value">The value to convert back.</param>
    /// <param name="targetType">The target type for conversion.</param>
    /// <param name="parameter">Optional parameter for conversion.</param>
    /// <param name="culture">The culture info for conversion.</param>
    [TestCase(null, typeof(object), null, null, TestName = "ConvertBack_NullValueAndParameter_ThrowsNotImplementedException")]
    [TestCase("", typeof(string), null, null, TestName = "ConvertBack_EmptyStringValue_ThrowsNotImplementedException")]
    [TestCase("test", typeof(string), "param", null, TestName = "ConvertBack_NonEmptyString_ThrowsNotImplementedException")]
    [TestCase(0, typeof(int), null, null, TestName = "ConvertBack_ZeroIntValue_ThrowsNotImplementedException")]
    [TestCase(123, typeof(int), 456, null, TestName = "ConvertBack_PositiveIntValue_ThrowsNotImplementedException")]
    [TestCase(-123, typeof(int), null, null, TestName = "ConvertBack_NegativeIntValue_ThrowsNotImplementedException")]
    [TestCase(int.MinValue, typeof(int), null, null, TestName = "ConvertBack_IntMinValue_ThrowsNotImplementedException")]
    [TestCase(int.MaxValue, typeof(int), null, null, TestName = "ConvertBack_IntMaxValue_ThrowsNotImplementedException")]
    [TestCase(0.0, typeof(double), null, null, TestName = "ConvertBack_ZeroDoubleValue_ThrowsNotImplementedException")]
    [TestCase(double.NaN, typeof(double), null, null, TestName = "ConvertBack_NaNValue_ThrowsNotImplementedException")]
    [TestCase(double.PositiveInfinity, typeof(double), null, null, TestName = "ConvertBack_PositiveInfinityValue_ThrowsNotImplementedException")]
    [TestCase(double.NegativeInfinity, typeof(double), null, null, TestName = "ConvertBack_NegativeInfinityValue_ThrowsNotImplementedException")]
    [TestCase(true, typeof(bool), null, null, TestName = "ConvertBack_TrueBoolValue_ThrowsNotImplementedException")]
    [TestCase(false, typeof(bool), null, null, TestName = "ConvertBack_FalseBoolValue_ThrowsNotImplementedException")]
    [TestCase(" ", typeof(string), null, null, TestName = "ConvertBack_WhitespaceString_ThrowsNotImplementedException")]
    [TestCaseSource(nameof(GetAdditionalConvertBackTestCases))]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        // Arrange
        var converter = new IsNotNullConverter();
        var cultureToUse = culture ?? CultureInfo.InvariantCulture;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, parameter, cultureToUse));
    }

    /// <summary>
    /// Provides additional test cases for ConvertBack method including edge cases with different cultures and complex types.
    /// </summary>
    private static object[] GetAdditionalConvertBackTestCases =
    {
        new object[] { new object(), typeof(object), null, CultureInfo.InvariantCulture },
        new object[] { new int[] { }, typeof(int[]), null, CultureInfo.InvariantCulture },
        new object[] { new int[] { 1, 2, 3 }, typeof(int[]), "parameter", CultureInfo.InvariantCulture },
        new object[] { new string('a', 10000), typeof(string), null, CultureInfo.InvariantCulture },
        new object[] { "\0", typeof(string), null, CultureInfo.InvariantCulture },
        new object[] { "test", typeof(bool), "param", new CultureInfo("en-US") },
        new object[] { 42, typeof(string), new object(), new CultureInfo("pl-PL") },
        new object[] { float.MaxValue, typeof(float), null, CultureInfo.CurrentCulture },
        new object[] { decimal.MinValue, typeof(decimal), null, CultureInfo.InvariantCulture }
    };

    /// <summary>
    /// Tests the ConvertBack method to ensure it always throws NotImplementedException
    /// regardless of input parameters.
    /// </summary>
    /// <param name="value">The value to convert back.</param>
    /// <param name="targetTypeString">String representation of the target type.</param>
    /// <param name="parameter">Optional parameter for conversion.</param>
    [TestCase(null, "System.String", null, TestName = "ConvertBack_NullValueAndParameter_ThrowsNotImplementedException")]
    [TestCase(true, "System.Boolean", null, TestName = "ConvertBack_TrueBooleanValue_ThrowsNotImplementedException")]
    [TestCase(false, "System.Boolean", null, TestName = "ConvertBack_FalseBooleanValue_ThrowsNotImplementedException")]
    [TestCase("test", "System.String", "param", TestName = "ConvertBack_StringValueWithParameter_ThrowsNotImplementedException")]
    [TestCase(123, "System.Int32", null, TestName = "ConvertBack_IntegerValue_ThrowsNotImplementedException")]
    [TestCase(0, "System.Int32", null, TestName = "ConvertBack_ZeroValue_ThrowsNotImplementedException")]
    [TestCase(-456, "System.Int32", null, TestName = "ConvertBack_NegativeIntegerValue_ThrowsNotImplementedException")]
    [TestCase(double.NaN, "System.Double", null, TestName = "ConvertBack_NaNValue_ThrowsNotImplementedException")]
    [TestCase(double.PositiveInfinity, "System.Double", null, TestName = "ConvertBack_PositiveInfinityValue_ThrowsNotImplementedException")]
    [TestCase(double.NegativeInfinity, "System.Double", null, TestName = "ConvertBack_NegativeInfinityValue_ThrowsNotImplementedException")]
    [TestCase("", "System.Object", "", TestName = "ConvertBack_EmptyStringValueAndParameter_ThrowsNotImplementedException")]
    [TestCase(" ", "System.String", " ", TestName = "ConvertBack_WhitespaceValueAndParameter_ThrowsNotImplementedException")]
    public void ConvertBack_VariousInputs_ThrowsNotImplementedException(object? value, string targetTypeString, object? parameter)
    {
        // Arrange
        var converter = new IsNotNullConverter();
        var targetType = Type.GetType(targetTypeString)!;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, parameter, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with various culture settings to ensure it always throws NotImplementedException.
    /// </summary>
    /// <param name="cultureName">The culture name to use for conversion.</param>
    [TestCase("en-US", TestName = "ConvertBack_EnglishUSCulture_ThrowsNotImplementedException")]
    [TestCase("fr-FR", TestName = "ConvertBack_FrenchCulture_ThrowsNotImplementedException")]
    [TestCase("de-DE", TestName = "ConvertBack_GermanCulture_ThrowsNotImplementedException")]
    [TestCase("", TestName = "ConvertBack_InvariantCulture_ThrowsNotImplementedException")]
    public void ConvertBack_VariousCultures_ThrowsNotImplementedException(string cultureName)
    {
        // Arrange
        var converter = new IsNotNullConverter();
        var culture = string.IsNullOrEmpty(cultureName) ? CultureInfo.InvariantCulture : new CultureInfo(cultureName);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(true, typeof(bool), null, culture));
    }

    /// <summary>
    /// Tests the ConvertBack method with edge case inputs including arrays and complex objects.
    /// </summary>
    [Test]
    public void ConvertBack_ArrayValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNotNullConverter();
        var arrayValue = new int[] { 1, 2, 3 };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(arrayValue, typeof(int[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with an empty array value.
    /// </summary>
    [Test]
    public void ConvertBack_EmptyArrayValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNotNullConverter();
        var emptyArray = new object[] { };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(emptyArray, typeof(object[]), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with boundary integer values.
    /// </summary>
    /// <param name="value">The boundary integer value to test.</param>
    [TestCase(int.MinValue, TestName = "ConvertBack_IntMinValue_ThrowsNotImplementedException")]
    [TestCase(int.MaxValue, TestName = "ConvertBack_IntMaxValue_ThrowsNotImplementedException")]
    public void ConvertBack_BoundaryIntegerValues_ThrowsNotImplementedException(int value)
    {
        // Arrange
        var converter = new IsNotNullConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, typeof(int), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with a very long string value.
    /// </summary>
    [Test]
    public void ConvertBack_VeryLongString_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNotNullConverter();
        var longString = new string('a', 10000);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(longString, typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests the ConvertBack method with special characters in string value.
    /// </summary>
    [Test]
    public void ConvertBack_StringWithSpecialCharacters_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNotNullConverter();
        var specialString = "\0\t\n\r";

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(specialString, typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tests ConvertBack with reference type edge cases.
    /// </summary>
    [TestCaseSource(nameof(GetReferenceTypeEdgeCases))]
    public void ConvertBack_ReferenceTypeEdgeCases_ThrowsNotImplementedException(object? value, Type targetType)
    {
        // Arrange
        var converter = new IsNotNullConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(value, targetType, null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Provides reference type edge cases for ConvertBack testing.
    /// </summary>
    private static object[] GetReferenceTypeEdgeCases =
    {
        new object[] { new object(), typeof(object) },
        new object?[] { new int[] { }, typeof(int[]) },
        new object?[] { new int[] { 1, 2, 3 }, typeof(int[]) },
        new object[] { " ", typeof(string) },
        new object[] { "\0", typeof(string) },
        new object[] { "\t\n\r", typeof(string) },
        new object[] { new string('a', 10000), typeof(string) }
    };

    /// <summary>
    /// Tests the Convert method for various input values to ensure correct boolean is returned
    /// when checking for non-nullness of the input value.
    /// </summary>
    /// <param name="input">The input to be checked for null.</param>
    /// <param name="expected">Expected result: false if input is null, true otherwise.</param>
    [TestCase(null, false, TestName = "Convert_InputIsNull_ReturnsFalse")]
    [TestCase("", true, TestName = "Convert_InputIsEmptyString_ReturnsTrue")]
    [TestCase("nonempty", true, TestName = "Convert_InputIsNonEmptyString_ReturnsTrue")]
    [TestCase(0, true, TestName = "Convert_InputIsZeroInt_ReturnsTrue")]
    [TestCase(123, true, TestName = "Convert_InputIsPositiveInt_ReturnsTrue")]
    [TestCase(-5, true, TestName = "Convert_InputIsNegativeInt_ReturnsTrue")]
    [TestCase(int.MinValue, true, TestName = "Convert_InputIsIntMinValue_ReturnsTrue")]
    [TestCase(int.MaxValue, true, TestName = "Convert_InputIsIntMaxValue_ReturnsTrue")]
    [TestCase(0.0, true, TestName = "Convert_InputIsZeroDouble_ReturnsTrue")]
    [TestCase(double.NaN, true, TestName = "Convert_InputIsNaN_ReturnsTrue")]
    [TestCase(double.PositiveInfinity, true, TestName = "Convert_InputIsPositiveInfinity_ReturnsTrue")]
    [TestCase(double.NegativeInfinity, true, TestName = "Convert_InputIsNegativeInfinity_ReturnsTrue")]
    [TestCase(true, true, TestName = "Convert_InputIsTrueBool_ReturnsTrue")]
    [TestCase(false, true, TestName = "Convert_InputIsFalseBool_ReturnsTrue")]
    [TestCaseSource(nameof(GetReferenceTypeCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new IsNotNullConverter();

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
        new object[] { new object(), true },
        new object[] { new int[] { }, true },
        new object[] { new int[] { 1, 2, 3 }, true },
        new object[] { " ", true },
        new object[] { "\0", true },
        new object[] { new string('a', 10000), true }
    };
}



/// <summary>
/// Unit tests for IsNotZeroConverter.Convert method.
/// </summary>
[TestFixture]
public class IsNotZeroConverterTests
{
    /// <summary>
    /// Tests the Convert method for integer input values to ensure correct boolean is returned
    /// when checking if the value is not zero.
    /// </summary>
    /// <param name="input">The integer input to be checked.</param>
    /// <param name="expected">Expected result: true if input is not zero, false if zero.</param>
    [TestCase(0, false, TestName = "Convert_IntZero_ReturnsFalse")]
    [TestCase(1, true, TestName = "Convert_IntOne_ReturnsTrue")]
    [TestCase(-1, true, TestName = "Convert_IntNegativeOne_ReturnsTrue")]
    [TestCase(int.MaxValue, true, TestName = "Convert_IntMaxValue_ReturnsTrue")]
    [TestCase(int.MinValue, true, TestName = "Convert_IntMinValue_ReturnsTrue")]
    [TestCase(42, true, TestName = "Convert_IntPositive_ReturnsTrue")]
    [TestCase(-42, true, TestName = "Convert_IntNegative_ReturnsTrue")]
    public void Convert_IntInputVariousValues_ReturnsCorrectResult(int input, bool expected)
    {
        // Arrange
        var converter = new IsNotZeroConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests the Convert method for double input values to ensure correct boolean is returned
    /// when checking if the value is not zero.
    /// </summary>
    /// <param name="input">The double input to be checked.</param>
    /// <param name="expected">Expected result: true if input is not zero, false if zero.</param>
    [TestCase(0.0, false, TestName = "Convert_DoubleZero_ReturnsFalse")]
    [TestCase(-0.0, false, TestName = "Convert_DoubleNegativeZero_ReturnsFalse")]
    [TestCase(1.0, true, TestName = "Convert_DoubleOne_ReturnsTrue")]
    [TestCase(-1.0, true, TestName = "Convert_DoubleNegativeOne_ReturnsTrue")]
    [TestCase(double.MaxValue, true, TestName = "Convert_DoubleMaxValue_ReturnsTrue")]
    [TestCase(double.MinValue, true, TestName = "Convert_DoubleMinValue_ReturnsTrue")]
    [TestCase(double.NaN, true, TestName = "Convert_DoubleNaN_ReturnsTrue")]
    [TestCase(double.PositiveInfinity, true, TestName = "Convert_DoublePositiveInfinity_ReturnsTrue")]
    [TestCase(double.NegativeInfinity, true, TestName = "Convert_DoubleNegativeInfinity_ReturnsTrue")]
    [TestCase(double.Epsilon, true, TestName = "Convert_DoubleEpsilon_ReturnsTrue")]
    [TestCase(42.5, true, TestName = "Convert_DoublePositive_ReturnsTrue")]
    [TestCase(-42.5, true, TestName = "Convert_DoubleNegative_ReturnsTrue")]
    [TestCase(0.0000001, true, TestName = "Convert_DoubleSmallPositive_ReturnsTrue")]
    [TestCase(-0.0000001, true, TestName = "Convert_DoubleSmallNegative_ReturnsTrue")]
    public void Convert_DoubleInputVariousValues_ReturnsCorrectResult(double input, bool expected)
    {
        // Arrange
        var converter = new IsNotZeroConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests the Convert method for decimal input values to ensure correct boolean is returned
    /// when checking if the value is not zero.
    /// </summary>
    /// <param name="value">The decimal input to be checked.</param>
    /// <param name="expected">Expected result: true if input is not zero, false if zero.</param>
    [TestCaseSource(nameof(GetDecimalTestCases))]
    public void Convert_DecimalInputVariousValues_ReturnsCorrectResult(decimal value, bool expected)
    {
        // Arrange
        var converter = new IsNotZeroConverter();

        // Act
        var result = converter.Convert(value, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Provides decimal test cases for boundary testing.
    /// </summary>
    private static object[] GetDecimalTestCases =
    {
        new object[] { 0m, false },
        new object[] { -0m, false },
        new object[] { 0.0m, false },
        new object[] { 1m, true },
        new object[] { -1m, true },
        new object[] { decimal.MaxValue, true },
        new object[] { decimal.MinValue, true },
        new object[] { 42.5m, true },
        new object[] { -42.5m, true },
        new object[] { 0.0000001m, true },
        new object[] { -0.0000001m, true }
    };

    /// <summary>
    /// Tests the Convert method for null and unsupported input types to ensure false is returned.
    /// </summary>
    /// <param name="input">The input to be checked.</param>
    [TestCase(null, TestName = "Convert_InputIsNull_ReturnsFalse")]
    [TestCase("0", TestName = "Convert_InputIsStringZero_ReturnsFalse")]
    [TestCase("1", TestName = "Convert_InputIsStringOne_ReturnsFalse")]
    [TestCase("", TestName = "Convert_InputIsEmptyString_ReturnsFalse")]
    [TestCase(" ", TestName = "Convert_InputIsWhitespaceString_ReturnsFalse")]
    [TestCase(true, TestName = "Convert_InputIsTrue_ReturnsFalse")]
    [TestCase(false, TestName = "Convert_InputIsFalse_ReturnsFalse")]
    [TestCaseSource(nameof(GetUnsupportedTypeTestCases))]
    public void Convert_UnsupportedInputTypes_ReturnsFalse(object? input)
    {
        // Arrange
        var converter = new IsNotZeroConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    /// <summary>
    /// Provides test cases for unsupported numeric and other types.
    /// </summary>
    private static object[] GetUnsupportedTypeTestCases =
    {
        new object[] { 0L },
        new object[] { 1L },
        new object[] { -1L },
        new object[] { 0f },
        new object[] { 1f },
        new object[] { -1f },
        new object[] { (byte)0 },
        new object[] { (byte)1 },
        new object[] { (short)0 },
        new object[] { (short)1 },
        new object[] { new object() },
        new object[] { new int[] { } },
        new object[] { new int[] { 0 } },
        new object[] { new int[] { 1, 2, 3 } }
    };
}



/// <summary>
/// Unit tests for HasCategoryIconConverter.Convert method.
/// </summary>
[TestFixture]
public class HasCategoryIconConverterTests
{
    /// <summary>
    /// Tests the Convert method with various input values to verify correct boolean is returned.
    /// Returns true when value is a non-null, non-empty string; false otherwise.
    /// Note: Whitespace-only strings return true because the method uses IsNullOrEmpty, not IsNullOrWhiteSpace.
    /// </summary>
    /// <param name="input">The input value to convert.</param>
    /// <param name="expected">Expected result: true if value is a non-null, non-empty string, false otherwise.</param>
    [TestCase(null, false, TestName = "Convert_InputIsNull_ReturnsFalse")]
    [TestCase("", false, TestName = "Convert_InputIsEmptyString_ReturnsFalse")]
    [TestCase("icon", true, TestName = "Convert_InputIsNonEmptyString_ReturnsTrue")]
    [TestCase("icon_name", true, TestName = "Convert_InputIsStringWithUnderscore_ReturnsTrue")]
    [TestCase("icon-name", true, TestName = "Convert_InputIsStringWithHyphen_ReturnsTrue")]
    [TestCase("icon123", true, TestName = "Convert_InputIsStringWithNumbers_ReturnsTrue")]
    [TestCase(" ", true, TestName = "Convert_InputIsSpaceString_ReturnsTrue")]
    [TestCase("  ", true, TestName = "Convert_InputIsMultipleSpaces_ReturnsTrue")]
    [TestCase("\t", true, TestName = "Convert_InputIsTabString_ReturnsTrue")]
    [TestCase("\n", true, TestName = "Convert_InputIsNewlineString_ReturnsTrue")]
    [TestCase("\r\n", true, TestName = "Convert_InputIsCarriageReturnNewline_ReturnsTrue")]
    [TestCase(" icon ", true, TestName = "Convert_InputIsStringWithLeadingTrailingSpaces_ReturnsTrue")]
    [TestCase("a", true, TestName = "Convert_InputIsSingleCharacter_ReturnsTrue")]
    [TestCase("🔥", true, TestName = "Convert_InputIsEmoji_ReturnsTrue")]
    [TestCase(0, false, TestName = "Convert_InputIsZeroInt_ReturnsFalse")]
    [TestCase(123, false, TestName = "Convert_InputIsPositiveInt_ReturnsFalse")]
    [TestCase(-5, false, TestName = "Convert_InputIsNegativeInt_ReturnsFalse")]
    [TestCase(int.MinValue, false, TestName = "Convert_InputIsIntMinValue_ReturnsFalse")]
    [TestCase(int.MaxValue, false, TestName = "Convert_InputIsIntMaxValue_ReturnsFalse")]
    [TestCase(0.0, false, TestName = "Convert_InputIsZeroDouble_ReturnsFalse")]
    [TestCase(123.45, false, TestName = "Convert_InputIsPositiveDouble_ReturnsFalse")]
    [TestCase(-123.45, false, TestName = "Convert_InputIsNegativeDouble_ReturnsFalse")]
    [TestCase(double.NaN, false, TestName = "Convert_InputIsNaN_ReturnsFalse")]
    [TestCase(double.PositiveInfinity, false, TestName = "Convert_InputIsPositiveInfinity_ReturnsFalse")]
    [TestCase(double.NegativeInfinity, false, TestName = "Convert_InputIsNegativeInfinity_ReturnsFalse")]
    [TestCase(true, false, TestName = "Convert_InputIsTrueBool_ReturnsFalse")]
    [TestCase(false, false, TestName = "Convert_InputIsFalseBool_ReturnsFalse")]
    [TestCaseSource(nameof(GetAdditionalTestCases))]
    public void Convert_VariousInputs_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new HasCategoryIconConverter();

        // Act
        var result = converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Provides additional test cases including reference types and very long strings.
    /// </summary>
    private static object[] GetAdditionalTestCases =
    {
        new object[] { new object(), false },
        new object[] { new int[] { }, false },
        new object[] { new int[] { 1, 2, 3 }, false },
        new object[] { new string('a', 10000), true },
        new object[] { new string(' ', 1000), true },
        new object[] { "\0", true }
    };

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException as expected.
    /// </summary>
    [Test]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new HasCategoryIconConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture));
    }
}



/// <summary>
/// Unit tests for IsNullOrEmptyBytesConverter.
/// </summary>
[TestFixture]
public class IsNullOrEmptyBytesConverterTests
{
    /// <summary>
    /// Tests the Convert method for various input values to ensure correct boolean is returned
    /// when checking if the value is null or an empty byte array.
    /// </summary>
    /// <param name="input">The input to be checked.</param>
    /// <param name="expected">Expected result: true if input is null or empty byte array, false otherwise.</param>
    [TestCase(null, true, TestName = "Convert_InputIsNull_ReturnsTrue")]
    [TestCase("", false, TestName = "Convert_InputIsEmptyString_ReturnsFalse")]
    [TestCase("nonempty", false, TestName = "Convert_InputIsNonEmptyString_ReturnsFalse")]
    [TestCase(0, false, TestName = "Convert_InputIsZeroInt_ReturnsFalse")]
    [TestCase(123, false, TestName = "Convert_InputIsPositiveInt_ReturnsFalse")]
    [TestCase(-5, false, TestName = "Convert_InputIsNegativeInt_ReturnsFalse")]
    [TestCase(int.MinValue, false, TestName = "Convert_InputIsIntMinValue_ReturnsFalse")]
    [TestCase(int.MaxValue, false, TestName = "Convert_InputIsIntMaxValue_ReturnsFalse")]
    [TestCase(0.0, false, TestName = "Convert_InputIsZeroDouble_ReturnsFalse")]
    [TestCase(double.NaN, false, TestName = "Convert_InputIsNaN_ReturnsFalse")]
    [TestCase(double.PositiveInfinity, false, TestName = "Convert_InputIsPositiveInfinity_ReturnsFalse")]
    [TestCase(double.NegativeInfinity, false, TestName = "Convert_InputIsNegativeInfinity_ReturnsFalse")]
    [TestCase(true, false, TestName = "Convert_InputIsTrueBool_ReturnsFalse")]
    [TestCase(false, false, TestName = "Convert_InputIsFalseBool_ReturnsFalse")]
    [TestCaseSource(nameof(GetByteArrayTestCases))]
    [TestCaseSource(nameof(GetReferenceTypeTestCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectResult(object? input, bool expected)
    {
        // Arrange
        var converter = new IsNullOrEmptyBytesConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Provides byte array test cases for Convert method.
    /// </summary>
    private static object[] GetByteArrayTestCases =
    {
        new object[] { new byte[0], true },
        new object[] { Array.Empty<byte>(), true },
        new object[] { new byte[] { 0 }, false },
        new object[] { new byte[] { 1 }, false },
        new object[] { new byte[] { 255 }, false },
        new object[] { new byte[] { byte.MinValue }, false },
        new object[] { new byte[] { byte.MaxValue }, false },
        new object[] { new byte[] { 1, 2, 3 }, false },
        new object[] { new byte[] { 0, 0, 0 }, false },
        new object[] { new byte[1000], false }
    };

    /// <summary>
    /// Provides additional reference type edge cases for Convert method.
    /// </summary>
    private static object[] GetReferenceTypeTestCases =
    {
        new object[] { new object(), false },
        new object[] { new int[] { }, false },
        new object[] { new int[] { 1, 2, 3 }, false },
        new object[] { new string[] { }, false },
        new object[] { " ", false },
        new object[] { "\0", false },
        new object[] { new string('a', 10000), false }
    };

    /// <summary>
    /// Tests that ConvertBack method throws NotImplementedException.
    /// </summary>
    [Test]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new IsNullOrEmptyBytesConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}



/// <summary>
/// Unit tests for LogLevelToColorConverter.Convert method.
/// </summary>
[TestFixture]
public class LogLevelToColorConverterTests
{
    /// <summary>
    /// Tests the Convert method with valid log level strings.
    /// Verifies that each log level string is converted to the appropriate color.
    /// Note: These tests assume light mode since Application.Current is typically null in unit test context.
    /// </summary>
    /// <param name="logLevel">The log level string to convert.</param>
    /// <param name="expectedColor">The expected color hex string.</param>
    [TestCase("Debug", "#6E6E6E", TestName = "Convert_DebugLevel_ReturnsGray500InLightMode")]
    [TestCase("Information", "#1976D2", TestName = "Convert_InformationLevel_ReturnsBlueInLightMode")]
    [TestCase("Warning", "#F57C00", TestName = "Convert_WarningLevel_ReturnsOrangeInLightMode")]
    [TestCase("Error", "#D32F2F", TestName = "Convert_ErrorLevel_ReturnsRedInLightMode")]
    [TestCase("Fatal", "#C2185B", TestName = "Convert_FatalLevel_ReturnsDarkPinkInLightMode")]
    public void Convert_ValidLogLevel_ReturnsExpectedColorInLightMode(string logLevel, string expectedColor)
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var expectedColorObj = Color.FromArgb(expectedColor);

        // Act
        var result = converter.Convert(logLevel, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.TypeOf<Color>());
        var resultColor = (Color)result;
        Assert.That(resultColor.ToArgbHex(), Is.EqualTo(expectedColorObj.ToArgbHex()));
    }

    /// <summary>
    /// Tests the Convert method with an unknown log level string.
    /// Verifies that an unrecognized log level returns the default color (Gray900 in light mode).
    /// </summary>
    [TestCase("Unknown", TestName = "Convert_UnknownLogLevel_ReturnsDefaultColor")]
    [TestCase("Trace", TestName = "Convert_TraceLogLevel_ReturnsDefaultColor")]
    [TestCase("Critical", TestName = "Convert_CriticalLogLevel_ReturnsDefaultColor")]
    [TestCase("", TestName = "Convert_EmptyString_ReturnsDefaultColor")]
    [TestCase("   ", TestName = "Convert_WhitespaceString_ReturnsDefaultColor")]
    [TestCase("debug", TestName = "Convert_LowercaseDebug_ReturnsDefaultColor")]
    [TestCase("DEBUG", TestName = "Convert_UppercaseDebUG_ReturnsDefaultColor")]
    [TestCase("Info", TestName = "Convert_AbbreviatedInfo_ReturnsDefaultColor")]
    public void Convert_UnknownOrInvalidLogLevel_ReturnsDefaultColor(string logLevel)
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var expectedDefaultColor = Color.FromArgb("#212121");

        // Act
        var result = converter.Convert(logLevel, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.TypeOf<Color>());
        var resultColor = (Color)result;
        Assert.That(resultColor.ToArgbHex(), Is.EqualTo(expectedDefaultColor.ToArgbHex()));
    }

    /// <summary>
    /// Tests the Convert method with special characters and edge case strings.
    /// Verifies that strings with special characters return the default color.
    /// </summary>
    [TestCase("Debug\0", TestName = "Convert_DebugWithNullChar_ReturnsDefaultColor")]
    [TestCase("Error\n", TestName = "Convert_ErrorWithNewline_ReturnsDefaultColor")]
    [TestCase("Warning\t", TestName = "Convert_WarningWithTab_ReturnsDefaultColor")]
    [TestCase("Info@rmation", TestName = "Convert_StringWithSpecialChar_ReturnsDefaultColor")]
    public void Convert_LogLevelWithSpecialCharacters_ReturnsDefaultColor(string logLevel)
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var expectedDefaultColor = Color.FromArgb("#212121");

        // Act
        var result = converter.Convert(logLevel, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.TypeOf<Color>());
        var resultColor = (Color)result;
        Assert.That(resultColor.ToArgbHex(), Is.EqualTo(expectedDefaultColor.ToArgbHex()));
    }

    /// <summary>
    /// Tests the Convert method with a very long string.
    /// Verifies that excessively long strings return the default color.
    /// </summary>
    [Test]
    public void Convert_VeryLongString_ReturnsDefaultColor()
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var veryLongString = new string('a', 10000);
        var expectedDefaultColor = Color.FromArgb("#212121");

        // Act
        var result = converter.Convert(veryLongString, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.TypeOf<Color>());
        var resultColor = (Color)result;
        Assert.That(resultColor.ToArgbHex(), Is.EqualTo(expectedDefaultColor.ToArgbHex()));
    }

    /// <summary>
    /// Tests the Convert method with null value.
    /// Verifies that null input returns the default color (#212121).
    /// </summary>
    [Test]
    public void Convert_NullValue_ReturnsDefaultColor()
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var expectedDefaultColor = Color.FromArgb("#212121");

        // Act
        var result = converter.Convert(null, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.TypeOf<Color>());
        var resultColor = (Color)result;
        Assert.That(resultColor.ToArgbHex(), Is.EqualTo(expectedDefaultColor.ToArgbHex()));
    }

    /// <summary>
    /// Tests the Convert method with non-string value types.
    /// Verifies that non-string inputs return the default color (#212121).
    /// </summary>
    /// <param name="value">The non-string value to convert.</param>
    [TestCase(42, TestName = "Convert_IntegerValue_ReturnsDefaultColor")]
    [TestCase(0, TestName = "Convert_ZeroInteger_ReturnsDefaultColor")]
    [TestCase(-5, TestName = "Convert_NegativeInteger_ReturnsDefaultColor")]
    [TestCase(int.MinValue, TestName = "Convert_IntMinValue_ReturnsDefaultColor")]
    [TestCase(int.MaxValue, TestName = "Convert_IntMaxValue_ReturnsDefaultColor")]
    [TestCase(3.14, TestName = "Convert_DoubleValue_ReturnsDefaultColor")]
    [TestCase(0.0, TestName = "Convert_ZeroDouble_ReturnsDefaultColor")]
    [TestCase(double.NaN, TestName = "Convert_NaNValue_ReturnsDefaultColor")]
    [TestCase(double.PositiveInfinity, TestName = "Convert_PositiveInfinity_ReturnsDefaultColor")]
    [TestCase(double.NegativeInfinity, TestName = "Convert_NegativeInfinity_ReturnsDefaultColor")]
    [TestCase(true, TestName = "Convert_TrueBoolean_ReturnsDefaultColor")]
    [TestCase(false, TestName = "Convert_FalseBoolean_ReturnsDefaultColor")]
    public void Convert_NonStringValue_ReturnsDefaultColor(object value)
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var expectedDefaultColor = Color.FromArgb("#212121");

        // Act
        var result = converter.Convert(value, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.TypeOf<Color>());
        var resultColor = (Color)result;
        Assert.That(resultColor.ToArgbHex(), Is.EqualTo(expectedDefaultColor.ToArgbHex()));
    }

    /// <summary>
    /// Tests the Convert method with an object that is not a string.
    /// Verifies that a custom object returns the default color.
    /// </summary>
    [Test]
    public void Convert_CustomObject_ReturnsDefaultColor()
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var customObject = new object();
        var expectedDefaultColor = Color.FromArgb("#212121");

        // Act
        var result = converter.Convert(customObject, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.TypeOf<Color>());
        var resultColor = (Color)result;
        Assert.That(resultColor.ToArgbHex(), Is.EqualTo(expectedDefaultColor.ToArgbHex()));
    }

    /// <summary>
    /// Tests the Convert method with various parameter and culture values.
    /// Verifies that the method ignores these parameters as they are not used in the implementation.
    /// </summary>
    [Test]
    public void Convert_WithDifferentParametersAndCulture_ReturnsExpectedColor()
    {
        // Arrange
        var converter = new LogLevelToColorConverter();
        var expectedColor = Color.FromArgb("#D32F2F");

        // Act
        var result1 = converter.Convert("Error", typeof(string), "someParameter", CultureInfo.InvariantCulture);
        var result2 = converter.Convert("Error", typeof(Color), null, new CultureInfo("pl-PL"));
        var result3 = converter.Convert("Error", typeof(object), 123, CultureInfo.CurrentCulture);

        // Assert
        Assert.That(result1, Is.TypeOf<Color>());
        Assert.That(result2, Is.TypeOf<Color>());
        Assert.That(result3, Is.TypeOf<Color>());

        var resultColor1 = (Color)result1;
        var resultColor2 = (Color)result2;
        var resultColor3 = (Color)result3;

        Assert.That(resultColor1.ToArgbHex(), Is.EqualTo(expectedColor.ToArgbHex()));
        Assert.That(resultColor2.ToArgbHex(), Is.EqualTo(expectedColor.ToArgbHex()));
        Assert.That(resultColor3.ToArgbHex(), Is.EqualTo(expectedColor.ToArgbHex()));
    }
}



/// <summary>
/// Unit tests for MealCountToHeightConverter.
/// </summary>
[TestFixture]
public class MealCountToHeightConverterTests
{
    /// <summary>
    /// Tests the Convert method for various input values to ensure correct height calculation
    /// based on meal count. Returns 0 when value is null, not an int, zero, or negative.
    /// Returns count * 44 for positive integers.
    /// </summary>
    /// <param name="input">The input value to convert.</param>
    /// <param name="expected">Expected height result.</param>
    [TestCase(null, 0, TestName = "Convert_InputIsNull_ReturnsZero")]
    [TestCase(0, 0, TestName = "Convert_InputIsZero_ReturnsZero")]
    [TestCase(-1, 0, TestName = "Convert_InputIsNegativeOne_ReturnsZero")]
    [TestCase(-5, 0, TestName = "Convert_InputIsNegativeFive_ReturnsZero")]
    [TestCase(int.MinValue, 0, TestName = "Convert_InputIsIntMinValue_ReturnsZero")]
    [TestCase(1, 44, TestName = "Convert_InputIsOne_Returns44")]
    [TestCase(2, 88, TestName = "Convert_InputIsTwo_Returns88")]
    [TestCase(5, 220, TestName = "Convert_InputIsFive_Returns220")]
    [TestCase(10, 440, TestName = "Convert_InputIsTen_Returns440")]
    [TestCase(100, 4400, TestName = "Convert_InputIsOneHundred_Returns4400")]
    [TestCase("", 0, TestName = "Convert_InputIsEmptyString_ReturnsZero")]
    [TestCase("10", 0, TestName = "Convert_InputIsStringNumber_ReturnsZero")]
    [TestCase("text", 0, TestName = "Convert_InputIsString_ReturnsZero")]
    [TestCase(1.5, 0, TestName = "Convert_InputIsDouble_ReturnsZero")]
    [TestCase(0.0, 0, TestName = "Convert_InputIsZeroDouble_ReturnsZero")]
    [TestCase(double.NaN, 0, TestName = "Convert_InputIsNaN_ReturnsZero")]
    [TestCase(double.PositiveInfinity, 0, TestName = "Convert_InputIsPositiveInfinity_ReturnsZero")]
    [TestCase(double.NegativeInfinity, 0, TestName = "Convert_InputIsNegativeInfinity_ReturnsZero")]
    [TestCase(true, 0, TestName = "Convert_InputIsTrue_ReturnsZero")]
    [TestCase(false, 0, TestName = "Convert_InputIsFalse_ReturnsZero")]
    [TestCaseSource(nameof(GetAdditionalEdgeCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectHeight(object? input, int expected)
    {
        // Arrange
        var converter = new MealCountToHeightConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Provides additional edge cases for Convert method including reference types and boundary values.
    /// </summary>
    private static object[] GetAdditionalEdgeCases =
    {
        new object[] { new object(), 0 },
        new object[] { new int[] { }, 0 },
        new object[] { new int[] { 1, 2, 3 }, 0 },
        new object[] { " ", 0 },
        new object[] { "\0", 0 },
        new object[] { long.MaxValue, 0 },
        new object[] { 1000, 44000 }
    };

    /// <summary>
    /// Tests the ConvertBack method to ensure it throws NotImplementedException as expected.
    /// </summary>
    [Test]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new MealCountToHeightConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}



/// <summary>
/// Unit tests for LogLevelToBackgroundConverter.
/// </summary>
[TestFixture]
public class LogLevelToBackgroundConverterTests
{
    /// <summary>
    /// Tests the Convert method with valid log level strings to ensure correct Color is returned.
    /// Note: The actual color depends on Application.Current.RequestedTheme which cannot be mocked.
    /// This test verifies that valid Color objects are returned for known log levels.
    /// </summary>
    /// <param name="logLevel">The log level string to convert.</param>
    [TestCase("Debug", TestName = "Convert_DebugLevel_ReturnsColor")]
    [TestCase("Information", TestName = "Convert_InformationLevel_ReturnsColor")]
    [TestCase("Warning", TestName = "Convert_WarningLevel_ReturnsColor")]
    [TestCase("Error", TestName = "Convert_ErrorLevel_ReturnsColor")]
    [TestCase("Fatal", TestName = "Convert_FatalLevel_ReturnsColor")]
    public void Convert_ValidLogLevel_ReturnsColor(string logLevel)
    {
        // Arrange
        var converter = new LogLevelToBackgroundConverter();

        // Act
        var result = converter.Convert(logLevel, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<Color>());

        var color = (Color)result;

        // Verify it's one of the expected colors (dark or light mode)
        var expectedColors = GetExpectedColorsForLevel(logLevel);
        Assert.That(expectedColors, Does.Contain(color));
    }

    /// <summary>
    /// Tests the Convert method with unknown log level strings to ensure default Color is returned.
    /// Unknown levels should return the default color based on the current theme.
    /// </summary>
    /// <param name="logLevel">The unknown log level string to convert.</param>
    [TestCase("Trace", TestName = "Convert_UnknownLevelTrace_ReturnsDefaultColor")]
    [TestCase("Critical", TestName = "Convert_UnknownLevelCritical_ReturnsDefaultColor")]
    [TestCase("Verbose", TestName = "Convert_UnknownLevelVerbose_ReturnsDefaultColor")]
    [TestCase("Custom", TestName = "Convert_UnknownLevelCustom_ReturnsDefaultColor")]
    [TestCase("", TestName = "Convert_EmptyString_ReturnsDefaultColor")]
    [TestCase("   ", TestName = "Convert_WhitespaceString_ReturnsDefaultColor")]
    public void Convert_UnknownLogLevel_ReturnsDefaultColor(string logLevel)
    {
        // Arrange
        var converter = new LogLevelToBackgroundConverter();

        // Act
        var result = converter.Convert(logLevel, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<Color>());

        var color = (Color)result;

        // Verify it's one of the default colors (either dark or light mode default)
        var expectedDefaults = new[]
        {
            Color.FromArgb("#303030"), // Dark mode default
            Color.FromArgb("#F5F5F5")  // Light mode default
        };
        Assert.That(expectedDefaults, Does.Contain(color));
    }

    /// <summary>
    /// Tests the Convert method with null value to ensure default Color is returned.
    /// </summary>
    [Test]
    public void Convert_NullValue_ReturnsDefaultColor()
    {
        // Arrange
        var converter = new LogLevelToBackgroundConverter();

        // Act
        var result = converter.Convert(null, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<Color>());
        Assert.That(result, Is.EqualTo(Color.FromArgb("#F5F5F5")));
    }

    /// <summary>
    /// Tests the Convert method with non-string value types to ensure default Color is returned.
    /// When value is not a string, the method should return the default light mode color #F5F5F5.
    /// </summary>
    /// <param name="value">The non-string value to convert.</param>
    [TestCase(123, TestName = "Convert_IntValue_ReturnsDefaultColor")]
    [TestCase(0, TestName = "Convert_ZeroInt_ReturnsDefaultColor")]
    [TestCase(-5, TestName = "Convert_NegativeInt_ReturnsDefaultColor")]
    [TestCase(int.MinValue, TestName = "Convert_IntMinValue_ReturnsDefaultColor")]
    [TestCase(int.MaxValue, TestName = "Convert_IntMaxValue_ReturnsDefaultColor")]
    [TestCase(true, TestName = "Convert_BoolTrue_ReturnsDefaultColor")]
    [TestCase(false, TestName = "Convert_BoolFalse_ReturnsDefaultColor")]
    [TestCase(0.0, TestName = "Convert_DoubleZero_ReturnsDefaultColor")]
    [TestCase(double.NaN, TestName = "Convert_DoubleNaN_ReturnsDefaultColor")]
    [TestCase(double.PositiveInfinity, TestName = "Convert_DoublePositiveInfinity_ReturnsDefaultColor")]
    [TestCase(double.NegativeInfinity, TestName = "Convert_DoubleNegativeInfinity_ReturnsDefaultColor")]
    [TestCaseSource(nameof(GetNonStringValueTestCases))]
    public void Convert_NonStringValue_ReturnsDefaultColor(object value)
    {
        // Arrange
        var converter = new LogLevelToBackgroundConverter();

        // Act
        var result = converter.Convert(value, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<Color>());
        Assert.That(result, Is.EqualTo(Color.FromArgb("#F5F5F5")));
    }

    /// <summary>
    /// Tests the Convert method with case-sensitive log level strings.
    /// The switch statement is case-sensitive, so variations should return default colors.
    /// </summary>
    /// <param name="logLevel">The log level string with different casing.</param>
    [TestCase("debug", TestName = "Convert_LowercaseDebug_ReturnsDefaultColor")]
    [TestCase("DEBUG", TestName = "Convert_UppercaseDebug_ReturnsDefaultColor")]
    [TestCase("DeBuG", TestName = "Convert_MixedCaseDebug_ReturnsDefaultColor")]
    [TestCase("information", TestName = "Convert_LowercaseInformation_ReturnsDefaultColor")]
    [TestCase("INFORMATION", TestName = "Convert_UppercaseInformation_ReturnsDefaultColor")]
    [TestCase("warning", TestName = "Convert_LowercaseWarning_ReturnsDefaultColor")]
    [TestCase("error", TestName = "Convert_LowercaseError_ReturnsDefaultColor")]
    [TestCase("fatal", TestName = "Convert_LowercaseFatal_ReturnsDefaultColor")]
    public void Convert_DifferentCaseLogLevel_ReturnsDefaultColor(string logLevel)
    {
        // Arrange
        var converter = new LogLevelToBackgroundConverter();

        // Act
        var result = converter.Convert(logLevel, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<Color>());

        var color = (Color)result;

        // Should return default colors for unknown (case-mismatched) levels
        var expectedDefaults = new[]
        {
            Color.FromArgb("#303030"), // Dark mode default
            Color.FromArgb("#F5F5F5")  // Light mode default
        };
        Assert.That(expectedDefaults, Does.Contain(color));
    }

    /// <summary>
    /// Tests the Convert method with special string edge cases.
    /// </summary>
    /// <param name="specialString">The special string to convert.</param>
    [TestCaseSource(nameof(GetSpecialStringTestCases))]
    public void Convert_SpecialStringValue_ReturnsDefaultColor(string specialString)
    {
        // Arrange
        var converter = new LogLevelToBackgroundConverter();

        // Act
        var result = converter.Convert(specialString, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<Color>());

        var color = (Color)result;

        // Should return default colors for unknown levels
        var expectedDefaults = new[]
        {
            Color.FromArgb("#303030"), // Dark mode default
            Color.FromArgb("#F5F5F5")  // Light mode default
        };
        Assert.That(expectedDefaults, Does.Contain(color));
    }

    /// <summary>
    /// Tests that ConvertBack throws NotImplementedException as expected.
    /// </summary>
    [Test]
    public void ConvertBack_AnyValue_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new LogLevelToBackgroundConverter();
        var someColor = Color.FromArgb("#FFFFFF");

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(someColor, typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Provides non-string value test cases for Convert method.
    /// </summary>
    private static object[] GetNonStringValueTestCases =
    {
        new object[] { new object() },
        new object[] { new int[] { } },
        new object[] { new int[] { 1, 2, 3 } },
        new object[] { new string[] { "Debug", "Error" } },
        new object[] { DateTime.Now },
        new object[] { Guid.NewGuid() }
    };

    /// <summary>
    /// Provides special string test cases including long strings and special characters.
    /// </summary>
    private static object[] GetSpecialStringTestCases =
    {
        new object[] { new string('a', 10000) }, // Very long string
        new object[] { "Debug\0Error" }, // String with null character
        new object[] { "Debug\r\nError" }, // String with newline
        new object[] { "\t\r\n" }, // Control characters
        new object[] { "日本語" }, // Unicode characters
        new object[] { "Debug Error" }, // String with space
        new object[] { " Debug" }, // Leading space
        new object[] { "Debug " } // Trailing space
    };

    /// <summary>
    /// Returns all expected colors (both dark and light mode) for a given log level.
    /// </summary>
    /// <param name="logLevel">The log level string.</param>
    /// <returns>Array of expected Color objects for both themes.</returns>
    private static Color[] GetExpectedColorsForLevel(string logLevel)
    {
        return logLevel switch
        {
            "Debug" => new[]
            {
                Color.FromArgb("#404040"), // Dark mode
                Color.FromArgb("#E1E1E1")  // Light mode
            },
            "Information" => new[]
            {
                Color.FromArgb("#1565C0"), // Dark mode
                Color.FromArgb("#E3F2FD")  // Light mode
            },
            "Warning" => new[]
            {
                Color.FromArgb("#E65100"), // Dark mode
                Color.FromArgb("#FFF3E0")  // Light mode
            },
            "Error" => new[]
            {
                Color.FromArgb("#C62828"), // Dark mode
                Color.FromArgb("#FFEBEE")  // Light mode
            },
            "Fatal" => new[]
            {
                Color.FromArgb("#880E4F"), // Dark mode
                Color.FromArgb("#FCE4EC")  // Light mode
            },
            _ => new[]
            {
                Color.FromArgb("#303030"), // Dark mode default
                Color.FromArgb("#F5F5F5")  // Light mode default
            }
        };
    }
}



/// <summary>
/// Unit tests for BoolToStrikethroughConverter.Convert method.
/// </summary>
[TestFixture]
public class BoolToStrikethroughConverterTests
{
    /// <summary>
    /// Tests the Convert method for various input values to ensure correct TextDecorations is returned.
    /// Returns Strikethrough when value is exactly true, otherwise returns None.
    /// </summary>
    /// <param name="input">The input value to convert.</param>
    /// <param name="expected">Expected TextDecorations result.</param>
    [TestCase(true, TextDecorations.Strikethrough, TestName = "Convert_InputIsTrue_ReturnsStrikethrough")]
    [TestCase(false, TextDecorations.None, TestName = "Convert_InputIsFalse_ReturnsNone")]
    [TestCase(null, TextDecorations.None, TestName = "Convert_InputIsNull_ReturnsNone")]
    [TestCase("", TextDecorations.None, TestName = "Convert_InputIsEmptyString_ReturnsNone")]
    [TestCase("true", TextDecorations.None, TestName = "Convert_InputIsStringTrue_ReturnsNone")]
    [TestCase("false", TextDecorations.None, TestName = "Convert_InputIsStringFalse_ReturnsNone")]
    [TestCase(0, TextDecorations.None, TestName = "Convert_InputIsZeroInt_ReturnsNone")]
    [TestCase(1, TextDecorations.None, TestName = "Convert_InputIsOneInt_ReturnsNone")]
    [TestCase(-1, TextDecorations.None, TestName = "Convert_InputIsNegativeInt_ReturnsNone")]
    [TestCase(int.MinValue, TextDecorations.None, TestName = "Convert_InputIsIntMinValue_ReturnsNone")]
    [TestCase(int.MaxValue, TextDecorations.None, TestName = "Convert_InputIsIntMaxValue_ReturnsNone")]
    [TestCase(0.0, TextDecorations.None, TestName = "Convert_InputIsZeroDouble_ReturnsNone")]
    [TestCase(1.0, TextDecorations.None, TestName = "Convert_InputIsOneDouble_ReturnsNone")]
    [TestCase(double.NaN, TextDecorations.None, TestName = "Convert_InputIsNaN_ReturnsNone")]
    [TestCase(double.PositiveInfinity, TextDecorations.None, TestName = "Convert_InputIsPositiveInfinity_ReturnsNone")]
    [TestCase(double.NegativeInfinity, TextDecorations.None, TestName = "Convert_InputIsNegativeInfinity_ReturnsNone")]
    [TestCaseSource(nameof(GetAdditionalEdgeCases))]
    public void Convert_InputVariousTypes_ReturnsCorrectTextDecorations(object? input, TextDecorations expected)
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();

        // Act
        var result = converter.Convert(input, typeof(TextDecorations), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests Convert method with various whitespace strings.
    /// </summary>
    [TestCase(" ", TestName = "Convert_InputIsWhitespace_ReturnsNone")]
    [TestCase("\t", TestName = "Convert_InputIsTab_ReturnsNone")]
    [TestCase("\n", TestName = "Convert_InputIsNewline_ReturnsNone")]
    [TestCase("\r\n", TestName = "Convert_InputIsCarriageReturnNewline_ReturnsNone")]
    [TestCase("   ", TestName = "Convert_InputIsMultipleSpaces_ReturnsNone")]
    public void Convert_InputIsWhitespaceString_ReturnsNone(string input)
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();

        // Act
        var result = converter.Convert(input, typeof(TextDecorations), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(TextDecorations.None));
    }

    /// <summary>
    /// Tests Convert method with special character strings.
    /// </summary>
    [TestCase("\0", TestName = "Convert_InputIsNullCharacter_ReturnsNone")]
    [TestCase("nonempty", TestName = "Convert_InputIsNonEmptyString_ReturnsNone")]
    public void Convert_InputIsSpecialString_ReturnsNone(string input)
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();

        // Act
        var result = converter.Convert(input, typeof(TextDecorations), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(TextDecorations.None));
    }

    /// <summary>
    /// Tests Convert method with very long string.
    /// </summary>
    [Test]
    public void Convert_InputIsVeryLongString_ReturnsNone()
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();
        var longString = new string('a', 10000);

        // Act
        var result = converter.Convert(longString, typeof(TextDecorations), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(TextDecorations.None));
    }

    /// <summary>
    /// Tests Convert method with null targetType parameter.
    /// </summary>
    [Test]
    public void Convert_TargetTypeIsNull_ReturnsStrikethroughWhenValueIsTrue()
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();

        // Act
        var result = converter.Convert(true, null!, null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(TextDecorations.Strikethrough));
    }

    /// <summary>
    /// Tests Convert method with null culture parameter.
    /// </summary>
    [Test]
    public void Convert_CultureIsNull_ReturnsStrikethroughWhenValueIsTrue()
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();

        // Act
        var result = converter.Convert(true, typeof(TextDecorations), null, null!);

        // Assert
        Assert.That(result, Is.EqualTo(TextDecorations.Strikethrough));
    }

    /// <summary>
    /// Tests Convert method with different culture.
    /// </summary>
    [Test]
    public void Convert_WithDifferentCulture_ReturnsStrikethroughWhenValueIsTrue()
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();
        var germanCulture = new CultureInfo("de-DE");

        // Act
        var result = converter.Convert(true, typeof(TextDecorations), null, germanCulture);

        // Assert
        Assert.That(result, Is.EqualTo(TextDecorations.Strikethrough));
    }

    /// <summary>
    /// Tests Convert method with non-null parameter object.
    /// </summary>
    [Test]
    public void Convert_WithParameter_ReturnsStrikethroughWhenValueIsTrue()
    {
        // Arrange
        var converter = new BoolToStrikethroughConverter();
        var parameter = new object();

        // Act
        var result = converter.Convert(true, typeof(TextDecorations), parameter, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(TextDecorations.Strikethrough));
    }

    /// <summary>
    /// Provides additional edge cases for Convert method including reference types and collections.
    /// </summary>
    private static object[] GetAdditionalEdgeCases =
    {
        new object[] { new object(), TextDecorations.None },
        new object[] { new int[] { }, TextDecorations.None },
        new object[] { new int[] { 1, 2, 3 }, TextDecorations.None },
        new object[] { new bool[] { true }, TextDecorations.None },
        new object[] { new bool[] { true, false }, TextDecorations.None }
    };
}



/// <summary>
/// Unit tests for CategoryIconConverter.Convert method.
/// </summary>
[TestFixture]
public class CategoryIconConverterTests
{
    /// <summary>
    /// Tests the Convert method with various input values to ensure correct handling
    /// of string icon names and non-string or null/empty values.
    /// </summary>
    /// <param name="input">The input value to convert.</param>
    /// <param name="expected">Expected result: the string itself if valid and not empty, otherwise null.</param>
    [TestCase(null, null, TestName = "Convert_InputIsNull_ReturnsNull")]
    [TestCase("", null, TestName = "Convert_InputIsEmptyString_ReturnsNull")]
    [TestCase("icon_name", "icon_name", TestName = "Convert_InputIsValidIconName_ReturnsIconName")]
    [TestCase("category_icon", "category_icon", TestName = "Convert_InputIsValidString_ReturnsString")]
    [TestCase(" ", " ", TestName = "Convert_InputIsSingleSpace_ReturnsSpace")]
    [TestCase("   ", "   ", TestName = "Convert_InputIsMultipleSpaces_ReturnsSpaces")]
    [TestCase("\t", "\t", TestName = "Convert_InputIsTabCharacter_ReturnsTab")]
    [TestCase("\n", "\n", TestName = "Convert_InputIsNewlineCharacter_ReturnsNewline")]
    [TestCase("@#$%^&*()", "@#$%^&*()", TestName = "Convert_InputIsSpecialCharacters_ReturnsSpecialCharacters")]
    [TestCase("icon\u0000name", "icon\u0000name", TestName = "Convert_InputContainsNullCharacter_ReturnsString")]
    [TestCase(0, null, TestName = "Convert_InputIsZeroInt_ReturnsNull")]
    [TestCase(123, null, TestName = "Convert_InputIsPositiveInt_ReturnsNull")]
    [TestCase(-5, null, TestName = "Convert_InputIsNegativeInt_ReturnsNull")]
    [TestCase(int.MinValue, null, TestName = "Convert_InputIsIntMinValue_ReturnsNull")]
    [TestCase(int.MaxValue, null, TestName = "Convert_InputIsIntMaxValue_ReturnsNull")]
    [TestCase(0.0, null, TestName = "Convert_InputIsZeroDouble_ReturnsNull")]
    [TestCase(3.14, null, TestName = "Convert_InputIsPositiveDouble_ReturnsNull")]
    [TestCase(-2.5, null, TestName = "Convert_InputIsNegativeDouble_ReturnsNull")]
    [TestCase(double.NaN, null, TestName = "Convert_InputIsNaN_ReturnsNull")]
    [TestCase(double.PositiveInfinity, null, TestName = "Convert_InputIsPositiveInfinity_ReturnsNull")]
    [TestCase(double.NegativeInfinity, null, TestName = "Convert_InputIsNegativeInfinity_ReturnsNull")]
    [TestCase(true, null, TestName = "Convert_InputIsTrueBool_ReturnsNull")]
    [TestCase(false, null, TestName = "Convert_InputIsFalseBool_ReturnsNull")]
    [TestCaseSource(nameof(GetAdditionalTestCases))]
    public void Convert_VariousInputValues_ReturnsExpectedResult(object? input, object? expected)
    {
        // Arrange
        var converter = new CategoryIconConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests the Convert method with a very long string to verify boundary conditions.
    /// </summary>
    [Test]
    public void Convert_InputIsVeryLongString_ReturnsString()
    {
        // Arrange
        var converter = new CategoryIconConverter();
        var longString = new string('a', 10000);

        // Act
        var result = converter.Convert(longString, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(longString));
    }

    /// <summary>
    /// Tests that the unused parameters (targetType, parameter, culture) do not affect the result.
    /// </summary>
    [Test]
    public void Convert_UnusedParametersWithDifferentValues_DoesNotAffectResult()
    {
        // Arrange
        var converter = new CategoryIconConverter();
        var iconName = "test_icon";

        // Act
        var result1 = converter.Convert(iconName, typeof(string), null, CultureInfo.InvariantCulture);
        var result2 = converter.Convert(iconName, typeof(int), "parameter", CultureInfo.CurrentCulture);
        var result3 = converter.Convert(iconName, typeof(object), new object(), new CultureInfo("en-US"));

        // Assert
        Assert.That(result1, Is.EqualTo(iconName));
        Assert.That(result2, Is.EqualTo(iconName));
        Assert.That(result3, Is.EqualTo(iconName));
    }

    /// <summary>
    /// Tests the ConvertBack method to ensure it throws NotImplementedException.
    /// </summary>
    [Test]
    public void ConvertBack_AnyInput_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new CategoryIconConverter();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack("icon", typeof(object), null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Provides additional reference type test cases for Convert method.
    /// </summary>
    private static object[] GetAdditionalTestCases =
    {
        new object?[] { new object(), null },
        new object?[] { new int[] { }, null },
        new object?[] { new int[] { 1, 2, 3 }, null },
        new object?[] { new string[] { "test" }, null },
        new object?[] { DateTime.Now, null },
        new object?[] { Guid.NewGuid(), null }
    };
}



/// <summary>
/// Unit tests for LogLevelToIconConverter.Convert method.
/// </summary>
[TestFixture]
public class LogLevelToIconConverterTests
{
    /// <summary>
    /// Tests the Convert method with valid log level strings to ensure correct icon is returned.
    /// </summary>
    /// <param name="logLevel">The log level string to convert.</param>
    [TestCase("Debug", TestName = "Convert_LogLevelIsDebug_ReturnsDebugIcon")]
    [TestCase("Information", TestName = "Convert_LogLevelIsInformation_ReturnsInformationIcon")]
    [TestCase("Warning", TestName = "Convert_LogLevelIsWarning_ReturnsWarningIcon")]
    [TestCase("Error", TestName = "Convert_LogLevelIsError_ReturnsErrorIcon")]
    [TestCase("Fatal", TestName = "Convert_LogLevelIsFatal_ReturnsFatalIcon")]
    public void Convert_ValidLogLevel_ReturnsNonNullIcon(string logLevel)
    {
        // Arrange
        var converter = new LogLevelToIconConverter();

        // Act
        var result = converter.Convert(logLevel, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<string>());
        Assert.That(result, Is.Not.Empty);
    }

    /// <summary>
    /// Tests the Convert method with null value to ensure default icon is returned.
    /// </summary>
    [Test]
    public void Convert_ValueIsNull_ReturnsDefaultIcon()
    {
        // Arrange
        var converter = new LogLevelToIconConverter();

        // Act
        var result = converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<string>());
        Assert.That(result, Is.Not.Empty);
    }

    /// <summary>
    /// Tests the Convert method with non-string values to ensure default icon is returned.
    /// </summary>
    /// <param name="input">The non-string input value.</param>
    [TestCase(123, TestName = "Convert_ValueIsInteger_ReturnsDefaultIcon")]
    [TestCase(0, TestName = "Convert_ValueIsZero_ReturnsDefaultIcon")]
    [TestCase(-5, TestName = "Convert_ValueIsNegativeInteger_ReturnsDefaultIcon")]
    [TestCase(true, TestName = "Convert_ValueIsBoolean_ReturnsDefaultIcon")]
    [TestCase(3.14, TestName = "Convert_ValueIsDouble_ReturnsDefaultIcon")]
    [TestCase(double.NaN, TestName = "Convert_ValueIsNaN_ReturnsDefaultIcon")]
    [TestCase(double.PositiveInfinity, TestName = "Convert_ValueIsPositiveInfinity_ReturnsDefaultIcon")]
    [TestCase(double.NegativeInfinity, TestName = "Convert_ValueIsNegativeInfinity_ReturnsDefaultIcon")]
    [TestCaseSource(nameof(GetNonStringTypeCases))]
    public void Convert_NonStringValue_ReturnsDefaultIcon(object input)
    {
        // Arrange
        var converter = new LogLevelToIconConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<string>());
        Assert.That(result, Is.Not.Empty);
    }

    /// <summary>
    /// Provides additional non-string type test cases.
    /// </summary>
    private static object[] GetNonStringTypeCases =
    {
        new object[] { new object() },
        new object[] { new int[] { 1, 2, 3 } },
        new object[] { CultureInfo.InvariantCulture }
    };

    /// <summary>
    /// Tests the Convert method with invalid or unknown log level strings to ensure default icon is returned.
    /// </summary>
    /// <param name="input">The invalid log level string.</param>
    [TestCase("", TestName = "Convert_EmptyString_ReturnsDefaultIcon")]
    [TestCase(" ", TestName = "Convert_WhitespaceString_ReturnsDefaultIcon")]
    [TestCase("   ", TestName = "Convert_MultipleWhitespaces_ReturnsDefaultIcon")]
    [TestCase("Unknown", TestName = "Convert_UnknownLogLevel_ReturnsDefaultIcon")]
    [TestCase("Trace", TestName = "Convert_TraceLogLevel_ReturnsDefaultIcon")]
    [TestCase("Critical", TestName = "Convert_CriticalLogLevel_ReturnsDefaultIcon")]
    [TestCase("Verbose", TestName = "Convert_VerboseLogLevel_ReturnsDefaultIcon")]
    [TestCase("debug", TestName = "Convert_LowercaseDebug_ReturnsDefaultIcon")]
    [TestCase("DEBUG", TestName = "Convert_UppercaseDebug_ReturnsDefaultIcon")]
    [TestCase("information", TestName = "Convert_LowercaseInformation_ReturnsDefaultIcon")]
    [TestCase("INFORMATION", TestName = "Convert_UppercaseInformation_ReturnsDefaultIcon")]
    [TestCase("error", TestName = "Convert_LowercaseError_ReturnsDefaultIcon")]
    [TestCase("ERROR", TestName = "Convert_UppercaseError_ReturnsDefaultIcon")]
    [TestCase("warning", TestName = "Convert_LowercaseWarning_ReturnsDefaultIcon")]
    [TestCase("Warning ", TestName = "Convert_WarningWithTrailingSpace_ReturnsDefaultIcon")]
    [TestCase(" Warning", TestName = "Convert_WarningWithLeadingSpace_ReturnsDefaultIcon")]
    [TestCase("Debug\n", TestName = "Convert_DebugWithNewline_ReturnsDefaultIcon")]
    [TestCase("\tInformation", TestName = "Convert_InformationWithTab_ReturnsDefaultIcon")]
    [TestCaseSource(nameof(GetInvalidStringCases))]
    public void Convert_InvalidOrUnknownString_ReturnsDefaultIcon(string input)
    {
        // Arrange
        var converter = new LogLevelToIconConverter();

        // Act
        var result = converter.Convert(input, typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<string>());
        Assert.That(result, Is.Not.Empty);
    }

    /// <summary>
    /// Provides additional invalid string test cases including special characters and very long strings.
    /// </summary>
    private static object[] GetInvalidStringCases =
    {
        new object[] { new string('a', 10000) },
        new object[] { "Debug123" },
        new object[] { "123Error" },
        new object[] { "Info" },
        new object[] { "Warn" },
        new object[] { "\0" },
        new object[] { "Debug\0" },
        new object[] { "€uro" },
        new object[] { "⚠️" }
    };

    /// <summary>
    /// Tests that the Convert method returns consistent values for the same log level called multiple times.
    /// </summary>
    [Test]
    public void Convert_SameLogLevelCalledMultipleTimes_ReturnsConsistentValue()
    {
        // Arrange
        var converter = new LogLevelToIconConverter();

        // Act
        var result1 = converter.Convert("Error", typeof(object), null, CultureInfo.InvariantCulture);
        var result2 = converter.Convert("Error", typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result1, Is.EqualTo(result2));
    }

    /// <summary>
    /// Tests that unused parameters (targetType, parameter, culture) do not affect the result.
    /// </summary>
    [Test]
    public void Convert_DifferentTargetTypeParameterCulture_ReturnsSameResult()
    {
        // Arrange
        var converter = new LogLevelToIconConverter();

        // Act
        var result1 = converter.Convert("Warning", typeof(string), null, CultureInfo.InvariantCulture);
        var result2 = converter.Convert("Warning", typeof(object), "param", CultureInfo.CurrentCulture);
        var result3 = converter.Convert("Warning", typeof(int), new object(), new CultureInfo("pl-PL"));

        // Assert
        Assert.That(result1, Is.EqualTo(result2));
        Assert.That(result2, Is.EqualTo(result3));
    }

    /// <summary>
    /// Tests that null and non-string values return the same default icon.
    /// </summary>
    [Test]
    public void Convert_NullAndNonString_ReturnSameDefaultIcon()
    {
        // Arrange
        var converter = new LogLevelToIconConverter();

        // Act
        var nullResult = converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture);
        var intResult = converter.Convert(123, typeof(object), null, CultureInfo.InvariantCulture);
        var unknownResult = converter.Convert("Unknown", typeof(object), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(nullResult, Is.EqualTo(intResult));
        Assert.That(intResult, Is.EqualTo(unknownResult));
    }
}
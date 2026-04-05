namespace ReceptyOks.Shared.Misc.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="ExtensionMethods"/> class.
    /// </summary>
    [TestFixture]
    public class ExtensionMethodsTests
    {
        /// <summary>
        /// Tests that GetStartOfWeek returns the correct Monday for various days of the week.
        /// </summary>
        /// <param name="year">The year of the test date.</param>
        /// <param name="month">The month of the test date.</param>
        /// <param name="day">The day of the test date.</param>
        /// <param name="expectedYear">The year of the expected Monday.</param>
        /// <param name="expectedMonth">The month of the expected Monday.</param>
        /// <param name="expectedDay">The day of the expected Monday.</param>
        [TestCase(2024, 1, 1, 2024, 1, 1)]  // Monday -> Same Monday
        [TestCase(2024, 1, 2, 2024, 1, 1)]  // Tuesday -> Previous Monday
        [TestCase(2024, 1, 3, 2024, 1, 1)]  // Wednesday -> Previous Monday
        [TestCase(2024, 1, 4, 2024, 1, 1)]  // Thursday -> Previous Monday
        [TestCase(2024, 1, 5, 2024, 1, 1)]  // Friday -> Previous Monday
        [TestCase(2024, 1, 6, 2024, 1, 1)]  // Saturday -> Previous Monday
        [TestCase(2024, 1, 7, 2024, 1, 1)]  // Sunday -> Previous Monday
        public void GetStartOfWeek_VariousDaysOfWeek_ReturnsCorrectMonday(int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
        {
            // Arrange
            var date = new DateTime(year, month, day, 14, 30, 45); // Include time component
            var expected = new DateTime(expectedYear, expectedMonth, expectedDay, 0, 0, 0); // Midnight

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result.TimeOfDay, Is.EqualTo(TimeSpan.Zero), "Result should have time stripped to midnight");
        }

        /// <summary>
        /// Tests that GetStartOfWeek correctly handles dates at the end of a month.
        /// </summary>
        [Test]
        public void GetStartOfWeek_EndOfMonth_ReturnsCorrectMonday()
        {
            // Arrange
            var date = new DateTime(2024, 1, 31, 23, 59, 59); // Wednesday, Jan 31
            var expected = new DateTime(2024, 1, 29); // Monday, Jan 29

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests that GetStartOfWeek correctly handles dates at the start of a year when the week spans two years.
        /// </summary>
        [Test]
        public void GetStartOfWeek_StartOfYear_ReturnsCorrectMondayFromPreviousYear()
        {
            // Arrange
            var date = new DateTime(2024, 1, 3); // Wednesday, Jan 3, 2024
            var expected = new DateTime(2024, 1, 1); // Monday, Jan 1, 2024

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests that GetStartOfWeek correctly handles a Sunday at the start of a year.
        /// </summary>
        [Test]
        public void GetStartOfWeek_SundayAtStartOfYear_ReturnsCorrectMondayFromPreviousYear()
        {
            // Arrange
            var date = new DateTime(2023, 1, 1); // Sunday, Jan 1, 2023
            var expected = new DateTime(2022, 12, 26); // Monday, Dec 26, 2022

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests that GetStartOfWeek correctly handles leap year dates.
        /// </summary>
        [Test]
        public void GetStartOfWeek_LeapYearDate_ReturnsCorrectMonday()
        {
            // Arrange
            var date = new DateTime(2024, 2, 29); // Thursday, Feb 29, 2024 (leap year)
            var expected = new DateTime(2024, 2, 26); // Monday, Feb 26, 2024

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests that GetStartOfWeek returns midnight when input has a time component.
        /// </summary>
        [Test]
        public void GetStartOfWeek_DateWithTimeComponent_ReturnsDateAtMidnight()
        {
            // Arrange
            var date = new DateTime(2024, 6, 15, 18, 45, 30, 500); // Saturday with specific time
            var expected = new DateTime(2024, 6, 10); // Monday at midnight

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result.Hour, Is.EqualTo(0));
            Assert.That(result.Minute, Is.EqualTo(0));
            Assert.That(result.Second, Is.EqualTo(0));
            Assert.That(result.Millisecond, Is.EqualTo(0));
        }

        /// <summary>
        /// Tests that GetStartOfWeek handles DateTime.MaxValue correctly.
        /// </summary>
        [Test]
        public void GetStartOfWeek_MaxValue_ReturnsCorrectMonday()
        {
            // Arrange
            var date = DateTime.MaxValue; // Friday, December 31, 9999
            var expected = new DateTime(9999, 12, 27); // Monday, December 27, 9999

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests that GetStartOfWeek handles DateTime.MinValue when it's already a Monday.
        /// </summary>
        [Test]
        public void GetStartOfWeek_MinValue_ReturnsMinValueDate()
        {
            // Arrange
            var date = DateTime.MinValue; // Monday, January 1, 0001

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(1, 1, 1)));
            Assert.That(result.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        }

        /// <summary>
        /// Tests that GetStartOfWeek handles dates near DateTime.MinValue that are not Monday.
        /// This tests potential edge cases where subtracting days might approach the minimum value.
        /// </summary>
        [Test]
        public void GetStartOfWeek_NearMinValue_ReturnsCorrectMonday()
        {
            // Arrange
            var date = new DateTime(1, 1, 7); // Sunday, January 7, 0001
            var expected = new DateTime(1, 1, 1); // Monday, January 1, 0001

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests that GetStartOfWeek correctly handles dates at the end of a year.
        /// </summary>
        [Test]
        public void GetStartOfWeek_EndOfYear_ReturnsCorrectMonday()
        {
            // Arrange
            var date = new DateTime(2024, 12, 31); // Tuesday, December 31, 2024
            var expected = new DateTime(2024, 12, 30); // Monday, December 30, 2024

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests that GetStartOfWeek correctly handles dates in different months and years with various day offsets.
        /// </summary>
        /// <param name="year">The year of the test date.</param>
        /// <param name="month">The month of the test date.</param>
        /// <param name="day">The day of the test date.</param>
        /// <param name="expectedYear">The year of the expected Monday.</param>
        /// <param name="expectedMonth">The month of the expected Monday.</param>
        /// <param name="expectedDay">The day of the expected Monday.</param>
        [TestCase(2024, 3, 10, 2024, 3, 4)]   // Sunday in March
        [TestCase(2024, 7, 4, 2024, 7, 1)]    // Thursday (Independence Day)
        [TestCase(2024, 12, 25, 2024, 12, 23)] // Wednesday (Christmas)
        [TestCase(2023, 2, 28, 2023, 2, 27)]  // Tuesday (non-leap year Feb end)
        [TestCase(2024, 2, 28, 2024, 2, 26)]  // Wednesday (leap year Feb 28)
        public void GetStartOfWeek_VariousDatesAcrossYears_ReturnsCorrectMonday(int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
        {
            // Arrange
            var date = new DateTime(year, month, day);
            var expected = new DateTime(expectedYear, expectedMonth, expectedDay);

            // Act
            var result = date.GetStartOfWeek();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        }

        /// <summary>
        /// Tests that GetPolishDayName returns the correct Polish name for valid DayOfWeek values.
        /// </summary>
        /// <param name="dayOfWeek">The day of the week to test.</param>
        /// <param name="expectedPolishName">The expected Polish name for the day.</param>
        [TestCase(DayOfWeek.Monday, "Poniedziałek")]
        [TestCase(DayOfWeek.Tuesday, "Wtorek")]
        [TestCase(DayOfWeek.Wednesday, "Środa")]
        [TestCase(DayOfWeek.Thursday, "Czwartek")]
        [TestCase(DayOfWeek.Friday, "Piątek")]
        [TestCase(DayOfWeek.Saturday, "Sobota")]
        [TestCase(DayOfWeek.Sunday, "Niedziela")]
        public void GetPolishDayName_ValidDayOfWeek_ReturnsCorrectPolishName(DayOfWeek dayOfWeek, string expectedPolishName)
        {
            // Act
            var result = dayOfWeek.GetPolishDayName();

            // Assert
            Assert.That(result, Is.EqualTo(expectedPolishName));
        }

        /// <summary>
        /// Tests that GetPolishDayName returns an empty string for invalid DayOfWeek values outside the defined enum range.
        /// </summary>
        /// <param name="invalidValue">The invalid integer value cast to DayOfWeek.</param>
        [TestCase(-1)]
        [TestCase(7)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void GetPolishDayName_InvalidDayOfWeekValue_ReturnsEmptyString(int invalidValue)
        {
            // Arrange
            var invalidDayOfWeek = (DayOfWeek)invalidValue;

            // Act
            var result = invalidDayOfWeek.GetPolishDayName();

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes returns an empty byte array when input is null.
        /// </summary>
        [Test]
        public void DecodeBase64OrHexToBytes_NullInput_ReturnsEmptyArray()
        {
            // Arrange
            string? input = null;

            // Act
            byte[] result = input!.DecodeBase64OrHexToBytes();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes correctly decodes valid Base64 strings.
        /// </summary>
        /// <param name="base64Input">The Base64 encoded input string.</param>
        /// <param name="expectedBytes">The expected decoded byte array.</param>
        [TestCase("SGVsbG8=", new byte[] { 72, 101, 108, 108, 111 })] // "Hello"
        [TestCase("V29ybGQ=", new byte[] { 87, 111, 114, 108, 100 })] // "World"
        [TestCase("", new byte[] { })] // Empty string
        [TestCase("AQIDBA==", new byte[] { 1, 2, 3, 4 })] // Binary data
        public void DecodeBase64OrHexToBytes_ValidBase64Input_ReturnsDecodedBytes(string base64Input, byte[] expectedBytes)
        {
            // Act
            byte[] result = base64Input.DecodeBase64OrHexToBytes();

            // Assert
            Assert.That(result, Is.EqualTo(expectedBytes));
        }

        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes correctly decodes valid hex strings without prefix.
        /// </summary>
        /// <param name="hexInput">The hex encoded input string.</param>
        /// <param name="expectedBytes">The expected decoded byte array.</param>
        [TestCase("48656c6c6f", new byte[] { 72, 101, 108, 108, 111 })] // "Hello" lowercase
        [TestCase("48656C6C6F", new byte[] { 72, 101, 108, 108, 111 })] // "Hello" uppercase
        [TestCase("FF", new byte[] { 255 })] // Single byte
        [TestCase("00", new byte[] { 0 })] // Zero byte
        [TestCase("0102030405", new byte[] { 1, 2, 3, 4, 5 })] // Multiple bytes
        public void DecodeBase64OrHexToBytes_ValidHexInputWithoutPrefix_ReturnsDecodedBytes(string hexInput, byte[] expectedBytes)
        {
            // Act
            byte[] result = hexInput.DecodeBase64OrHexToBytes();

            // Assert
            Assert.That(result, Is.EqualTo(expectedBytes));
        }

        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes throws FormatException for invalid hex strings with odd length.
        /// </summary>
        [TestCase("F")]
        [TestCase("ABC")]
        [TestCase("12345")]
        public void DecodeBase64OrHexToBytes_OddLengthHexInput_ThrowsFormatException(string invalidHexInput)
        {
            // Act & Assert
            Assert.Throws<FormatException>(() => invalidHexInput.DecodeBase64OrHexToBytes());
        }


        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes correctly handles hex strings with mixed case.
        /// </summary>
        [Test]
        public void DecodeBase64OrHexToBytes_MixedCaseHex_ReturnsDecodedBytes()
        {
            // Arrange
            string hexInput = "0xAbCdEf12";
            byte[] expectedBytes = new byte[] { 0xAB, 0xCD, 0xEF, 0x12 };

            // Act
            byte[] result = hexInput.DecodeBase64OrHexToBytes();

            // Assert
            Assert.That(result, Is.EqualTo(expectedBytes));
        }

        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes handles the edge case of "0x" prefix only.
        /// </summary>
        [Test]
        public void DecodeBase64OrHexToBytes_OnlyPrefixInput_ReturnsEmptyArray()
        {
            // Arrange
            string input = "0x";

            // Act
            byte[] result = input.DecodeBase64OrHexToBytes();

            // Assert
            Assert.That(result, Is.Empty);
        }

        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes prioritizes Base64 decoding over hex when string is valid for both.
        /// </summary>
        [Test]
        public void DecodeBase64OrHexToBytes_ValidBase64AndHex_PrioritizesBase64()
        {
            // Arrange
            // "ABCD" is valid Base64 (decodes to {0, 16, 131}) and also valid hex (would decode to {0xAB, 0xCD})
            string input = "ABCD";
            byte[] expectedBase64Bytes = Convert.FromBase64String(input);

            // Act
            byte[] result = input.DecodeBase64OrHexToBytes();

            // Assert
            Assert.That(result, Is.EqualTo(expectedBase64Bytes));
            Assert.That(result, Is.Not.EqualTo(Convert.FromHexString(input))); // Verify it's not using hex decoding
        }


        /// <summary>
        /// Tests that DecodeBase64OrHexToBytes handles long valid Base64 strings.
        /// </summary>
        [Test]
        public void DecodeBase64OrHexToBytes_LongBase64String_ReturnsDecodedBytes()
        {
            // Arrange
            string base64Input = "VGhpcyBpcyBhIGxvbmcgc3RyaW5nIHRvIHRlc3QgQmFzZTY0IGRlY29kaW5nIHdpdGggbXVsdGlwbGUgd29yZHM=";
            byte[] expectedBytes = Convert.FromBase64String(base64Input);

            // Act
            byte[] result = base64Input.DecodeBase64OrHexToBytes();

            // Assert
            Assert.That(result, Is.EqualTo(expectedBytes));
        }
    }
}
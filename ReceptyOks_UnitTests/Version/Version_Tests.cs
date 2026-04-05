using ReceptyOks.Services;

namespace ReceptyOks_UnitTests.Version
{
    [TestFixture]
    public class Version_Tests
    {
        [TestCase("1.2.3", "1.2.3")]
        [TestCase("v1.2.3", "1.2.3")]
        [TestCase("V1.2.3", "1.2.3")]
        [TestCase("1.2.3.alpha", "1.2.3")]
        [TestCase("v1.2.3.beta", "1.2.3")]
        [TestCase("1.2.3.", "1.2.3")]
        [TestCase("", "")]
        public void ConvertVersionToNumeric_ReturnsExpected(string input, string expected)
        {
            var result = VersionInfo.ConvertVersionToNumeric(input);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}

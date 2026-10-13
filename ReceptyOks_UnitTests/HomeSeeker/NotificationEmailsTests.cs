using HomeSeeker.Models;

namespace ReceptyOks_UnitTests.HomeSeeker;

public sealed class NotificationEmailsTests
{
    [Test]
    public void Parse_SingleAddress_ReturnsIt()
    {
        var result = NotificationEmails.Parse("a@example.com");

        Assert.That(result, Is.EqualTo(new[] { "a@example.com" }));
    }

    [Test]
    public void Parse_CommaAndSemicolonSeparated_TrimsAndSplits()
    {
        var result = NotificationEmails.Parse(" a@example.com , b@example.com;c@example.com ;; ");

        Assert.That(result, Is.EqualTo(new[] { "a@example.com", "b@example.com", "c@example.com" }));
    }

    [Test]
    public void Parse_DuplicatesIgnoringCase_AreRemoved()
    {
        var result = NotificationEmails.Parse("A@example.com, a@example.com");

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ;, ")]
    public void Parse_Empty_ReturnsEmptyList(string? value)
    {
        Assert.That(NotificationEmails.Parse(value), Is.Empty);
    }

    [Test]
    public void Normalize_ProducesCanonicalCommaSeparatedForm()
    {
        var result = NotificationEmails.Normalize("b@example.com ;a@example.com,  b@example.com");

        Assert.That(result, Is.EqualTo("b@example.com, a@example.com"));
    }
}

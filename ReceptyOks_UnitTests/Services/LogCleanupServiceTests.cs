using ReceptyOks.Shared.Configuration;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class LogCleanupServiceTests
{
    [Test]
    public void WhenMaxAgeIsDefaultThenValueIsSeven()
    {
        var options = new CleanupOptions();

        Assert.That(options.MaxAge.Days, Is.EqualTo(7));
    }

    [Test]
    public void WhenStartupDelayIsDefaultThenValueIsThirtySeconds()
    {
        var options = new CleanupOptions();

        Assert.That(options.StartupDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
    }

    [Test]
    public void WhenIntervalIsDefaultThenValueIsTwentyFourHours()
    {
        var options = new CleanupOptions();

        Assert.That(options.Interval, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void WhenCustomValuesProvidedThenOptionsReflectThem()
    {
        var options = new CleanupOptions
        {
            Interval = TimeSpan.FromMinutes(5),
            StartupDelay = TimeSpan.FromSeconds(10),
            MaxAge = TimeSpan.FromDays(14)
        };

        Assert.That(options.Interval, Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(options.StartupDelay, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(options.MaxAge, Is.EqualTo(TimeSpan.FromDays(14)));
    }
}

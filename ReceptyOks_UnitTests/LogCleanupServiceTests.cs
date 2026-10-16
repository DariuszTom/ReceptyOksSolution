using Microsoft.Extensions.Logging;
using Moq;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.Configuration;

namespace ReceptyOks_UnitTests;

[TestFixture]
public class LogCleanupServiceTests
{
    private Mock<ILocalDatabase> _db = null!;
    private Mock<ILogger<LogCleanupService>> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new Mock<ILocalDatabase>();
        _logger = new Mock<ILogger<LogCleanupService>>();
    }

    private LogCleanupService CreateService(TimeSpan? interval = null) => new(
        _db.Object,
        new CleanupOptions
        {
            StartupDelay = TimeSpan.FromMilliseconds(10),
            Interval = interval ?? TimeSpan.FromHours(1),
            MaxAge = TimeSpan.FromDays(3)
        },
        _logger.Object);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
            await Task.Delay(25);
    }

    [Test]
    public async Task RunsCleanupAfterStartupDelay_WithConfiguredMaxAgeDays()
    {
        _db.Setup(d => d.ClearOldLogsAsync(It.IsAny<int>())).ReturnsAsync(0);
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _db.Invocations.Count > 0);
        await service.StopAsync(CancellationToken.None);

        _db.Verify(d => d.ClearOldLogsAsync(3), Times.Once);
    }

    [Test]
    public async Task RunsCleanupRepeatedly_OnInterval()
    {
        _db.Setup(d => d.ClearOldLogsAsync(It.IsAny<int>())).ReturnsAsync(0);
        var service = CreateService(TimeSpan.FromMilliseconds(30));

        await service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _db.Invocations.Count >= 3);
        await service.StopAsync(CancellationToken.None);

        Assert.That(_db.Invocations.Count, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public async Task DatabaseFailure_IsLogged_AndDoesNotStopService()
    {
        _db.Setup(d => d.ClearOldLogsAsync(It.IsAny<int>())).ThrowsAsync(new InvalidOperationException("boom"));
        var service = CreateService(TimeSpan.FromMilliseconds(30));

        await service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _db.Invocations.Count >= 2);
        await service.StopAsync(CancellationToken.None);

        Assert.That(_db.Invocations.Count, Is.GreaterThanOrEqualTo(2));
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<InvalidOperationException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task StoppedBeforeStartupDelay_DoesNotCleanup()
    {
        var service = new LogCleanupService(
            _db.Object,
            new CleanupOptions { StartupDelay = TimeSpan.FromSeconds(30) },
            _logger.Object);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        _db.Verify(d => d.ClearOldLogsAsync(It.IsAny<int>()), Times.Never);
    }
}

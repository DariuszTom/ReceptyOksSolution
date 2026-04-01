using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using ReceptyOks.Interfaces;
using ReceptyOks.Services;
using ReceptyOks.Shared.Configuration;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class PeriodicSyncServiceTests
{
    private Mock<ISyncService> _mockSyncService = null!;
    private Mock<ILogger<PeriodicSyncService>> _mockLogger = null!;
    private PeriodicSyncOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _mockSyncService = new Mock<ISyncService>();
        _mockLogger = new Mock<ILogger<PeriodicSyncService>>();
        _options = new PeriodicSyncOptions
        {
            Interval = TimeSpan.FromMilliseconds(100),
            StartupDelay = TimeSpan.FromMilliseconds(50),
            SyncType = SyncType.Normal,
            ShowNotifications = false
        };
    }

    [Test]
    public async Task WhenStartedThenStartupDelayIsRespected()
    {
        // Arrange
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(new SyncResult { Success = true });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var startTime = DateTime.UtcNow;
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(30)); // Less than startup delay
        cts.Cancel();
        await task;

        // Assert
        _mockSyncService.Verify(x => x.SyncAsync(), Times.Never, 
            "Sync should not run before startup delay");
    }

    [Test]
    public async Task WhenSyncTypeIsNormalThenSyncAsyncIsCalled()
    {
        // Arrange
        var options = new PeriodicSyncOptions
        {
            Interval = TimeSpan.FromMilliseconds(100),
            StartupDelay = TimeSpan.FromMilliseconds(50),
            SyncType = SyncType.Normal,
            ShowNotifications = false
        };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(new SyncResult { Success = true });

        var service = CreateService(options);
        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200)); // Wait for startup + interval
        cts.Cancel();
        await task;

        // Assert
        _mockSyncService.Verify(x => x.SyncAsync(), Times.AtLeastOnce);
        _mockSyncService.Verify(x => x.FullSyncAsync(), Times.Never);
    }

    [Test]
    public async Task WhenSyncTypeIsForceThenFullSyncAsyncIsCalled()
    {
        // Arrange
        var options = new PeriodicSyncOptions
        {
            Interval = TimeSpan.FromMilliseconds(100),
            StartupDelay = TimeSpan.FromMilliseconds(50),
            SyncType = SyncType.Force,
            ShowNotifications = false
        };
        _mockSyncService.Setup(x => x.FullSyncAsync())
            .ReturnsAsync(new SyncResult { Success = true });

        var service = CreateService(options);
        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cts.Cancel();
        await task;

        // Assert
        _mockSyncService.Verify(x => x.FullSyncAsync(), Times.AtLeastOnce);
        _mockSyncService.Verify(x => x.SyncAsync(), Times.Never);
    }

    [Test]
    public async Task WhenSyncTypeIsManualThenNoSyncIsCalled()
    {
        // Arrange
        var options = new PeriodicSyncOptions
        {
            Interval = TimeSpan.FromMilliseconds(100),
            StartupDelay = TimeSpan.FromMilliseconds(50),
            SyncType = SyncType.Manual,
            ShowNotifications = false
        };

        var service = CreateService(options);
        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cts.Cancel();
        await task;

        // Assert
        _mockSyncService.Verify(x => x.SyncAsync(), Times.Never);
        _mockSyncService.Verify(x => x.FullSyncAsync(), Times.Never);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Manual")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Should log that sync type is Manual");
    }

    [Test]
    public async Task WhenSyncFailsThenErrorIsLogged()
    {
        // Arrange
        _mockSyncService.Setup(x => x.SyncAsync())
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cts.Cancel();
        await task;

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during periodic sync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Should log error when sync fails");
    }

    [Test]
    public async Task WhenCancelledThenServiceStopsGracefully()
    {
        // Arrange
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(new SyncResult { Success = true });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cts.Cancel();
        await task;
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task WhenMultipleIntervalsPassThenSyncIsCalledMultipleTimes()
    {
        // Arrange
        var options = new PeriodicSyncOptions
        {
            Interval = TimeSpan.FromMilliseconds(50),
            StartupDelay = TimeSpan.FromMilliseconds(10),
            SyncType = SyncType.Normal,
            ShowNotifications = false
        };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(new SyncResult { Success = true });

        var service = CreateService(options);
        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200)); // ~3 intervals
        cts.Cancel();
        await task;

        // Assert
        _mockSyncService.Verify(x => x.SyncAsync(), Times.AtLeast(2));
    }

    [Test]
    public void WhenOptionsHaveDefaultValuesThenTheyAreCorrect()
    {
        // Arrange & Act
        var defaultOptions = new PeriodicSyncOptions();

        // Assert
        Assert.That(defaultOptions.Interval, Is.EqualTo(TimeSpan.FromMinutes(30)));
        Assert.That(defaultOptions.StartupDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(defaultOptions.SyncType, Is.EqualTo(SyncType.Normal));
        Assert.That(defaultOptions.ShowNotifications, Is.False);
    }

    [Test]
    public async Task WhenSyncReturnsNullThenNoExceptionIsThrown()
    {
        // Arrange
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync((SyncResult)null!);

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act & Assert
        var task = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cts.Cancel();

        Assert.DoesNotThrowAsync(async () => await task);
    }

    private PeriodicSyncService CreateService(PeriodicSyncOptions? options = null)
    {
        return new PeriodicSyncService(
            _mockSyncService.Object, 
            options ?? _options, 
            _mockLogger.Object);
    }
}

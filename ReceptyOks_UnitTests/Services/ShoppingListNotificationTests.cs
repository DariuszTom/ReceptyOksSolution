using Microsoft.Extensions.Logging;
using Moq;
using ReceptyOks.Configuration;
using ReceptyOks.Misc;
using ReceptyOks.Services;
using ReceptyOks.Shared.Models;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class ShoppingListNotificationTests
{
    private Mock<IShoppingListService> _serviceMock = null!;
    private Mock<INotificationManagerService> _notificationManagerMock = null!;
    private Mock<IPreferences> _preferencesMock = null!;
    private AppNotification _appNotification = null!;
    private AppSettings _appSettings = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceMock = new Mock<IShoppingListService>();
        _notificationManagerMock = new Mock<INotificationManagerService>();
        _preferencesMock = new Mock<IPreferences>();
        _appNotification = new AppNotification(
            _notificationManagerMock.Object,
            Mock.Of<ILogger<AppNotification>>());
        _appSettings = new AppSettings
        {
            Notifications = new NotificationSettings
            {
                StartupDelaySeconds = 0,
                ShoppingListCheckIntervalMinutes = 60
            }
        };
    }

    [Test]
    public async Task WhenNewItemsExistThenNotificationIsSent()
    {
        var items = new List<ShoppingListItem>
        {
            new() { Name = "Mleko", CreatedAt = DateTime.UtcNow.AddSeconds(1) }
        };
        SetupServiceReturns(items);
        SetupLastCheckTime(DateTime.UtcNow.AddMinutes(-5));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        _notificationManagerMock.Verify(
            n => n.SendNotification("Lista zakupów", It.Is<string>(m => m.Contains("Mleko")), null),
            Times.Once);
    }

    [Test]
    public async Task WhenMultipleNewItemsExistThenNotificationContainsCount()
    {
        var items = new List<ShoppingListItem>
        {
            new() { Name = "Mleko", CreatedAt = DateTime.UtcNow.AddSeconds(1) },
            new() { Name = "Chleb", CreatedAt = DateTime.UtcNow.AddSeconds(1) }
        };
        SetupServiceReturns(items);
        SetupLastCheckTime(DateTime.UtcNow.AddMinutes(-5));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        _notificationManagerMock.Verify(
            n => n.SendNotification("Lista zakupów", It.Is<string>(m => m.Contains("2")), null),
            Times.Once);
    }

    [Test]
    public async Task WhenNoNewItemsExistThenNotificationIsNotSent()
    {
        var items = new List<ShoppingListItem>
        {
            new() { Name = "Mleko", CreatedAt = DateTime.UtcNow.AddMinutes(-10) }
        };
        SetupServiceReturns(items);
        SetupLastCheckTime(DateTime.UtcNow.AddMinutes(-5));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        _notificationManagerMock.Verify(
            n => n.SendNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()),
            Times.Never);
    }

    [Test]
    public async Task WhenFetchFailsThenNotificationIsNotSent()
    {
        _serviceMock
            .Setup(s => s.GetAllAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShoppingListResult<List<ShoppingListItem>>.Failure("Network error"));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        _notificationManagerMock.Verify(
            n => n.SendNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()),
            Times.Never);
    }

    [Test]
    public async Task WhenEmptyListReturnedThenNotificationIsNotSent()
    {
        SetupServiceReturns([]);
        SetupLastCheckTime(DateTime.UtcNow.AddMinutes(-5));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        _notificationManagerMock.Verify(
            n => n.SendNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()),
            Times.Never);
    }

    [Test]
    public async Task WhenCheckCompletesThenLastCheckTimeIsUpdated()
    {
        SetupServiceReturns([]);
        SetupLastCheckTime(DateTime.UtcNow.AddMinutes(-5));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        _preferencesMock.Verify(
            p => p.Set(_appSettings.Notifications.PreferenceKey, It.IsAny<long>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task WhenFetchFailsThenLastCheckTimeIsNotUpdated()
    {
        _serviceMock
            .Setup(s => s.GetAllAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShoppingListResult<List<ShoppingListItem>>.Failure("Network error"));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        _preferencesMock.Verify(
            p => p.Set(It.IsAny<string>(), It.IsAny<long>()),
            Times.Never);
    }

    [Test]
    public async Task WhenCancelledDuringStartupDelayThenStopsPromptly()
    {
        _appSettings.Notifications.StartupDelaySeconds = 3600;

        var sut = CreateSut();

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        _serviceMock.Verify(
            s => s.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task WhenServiceThrowsThenServiceDoesNotCrash()
    {
        _serviceMock
            .Setup(s => s.GetAllAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        Assert.Pass();
    }

    private ShoppingListNotification CreateSut() =>
        new(
            _serviceMock.Object,
            _appNotification,
            Mock.Of<ILogger<ShoppingListNotification>>(),
            _appSettings,
            _preferencesMock.Object);

    private void SetupServiceReturns(List<ShoppingListItem> items)
    {
        _serviceMock
            .Setup(s => s.GetAllAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShoppingListResult<List<ShoppingListItem>>.Success(items));
    }

    private void SetupLastCheckTime(DateTime lastCheck)
    {
        _preferencesMock
            .Setup(p => p.Get(_appSettings.Notifications.PreferenceKey, It.IsAny<long>()))
            .Returns(lastCheck.Ticks);
    }
}

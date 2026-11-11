using Microsoft.Extensions.Logging;
using Moq;
using ReceptyOks.Services;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class AppNotificationTests
{
    private Mock<INotificationManagerService> _notificationManagerMock = null!;
    private Mock<ILogger<AppNotification>> _loggerMock = null!;
    private AppNotification _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _notificationManagerMock = new Mock<INotificationManagerService>();
        _loggerMock = new Mock<ILogger<AppNotification>>();
        _sut = new AppNotification(_notificationManagerMock.Object, _loggerMock.Object);
    }

    [Test]
    public void SendNotification_WhenManagerSucceeds_ForwardsTitleAndMessage()
    {
        _sut.SendNotification("Title", "Message");

        _notificationManagerMock.Verify(
            manager => manager.SendNotification("Title", "Message", null),
            Times.Once);
    }

    [Test]
    public void SendNotification_WhenManagerThrows_LogsErrorAndDoesNotThrow()
    {
        var exception = new InvalidOperationException("Notification failure");
        _notificationManagerMock
            .Setup(manager => manager.SendNotification("Title", "Message", null))
            .Throws(exception);

        Assert.DoesNotThrow(() => _sut.SendNotification("Title", "Message"));

        AssertErrorLogged("Failed to send notification", exception);
    }

    [Test]
    public void SendNotification_WhenScheduledManagerSucceeds_ForwardsNotificationTime()
    {
        var notifyTime = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Local);

        _sut.SendNotification("Title", "Message", notifyTime);

        _notificationManagerMock.Verify(
            manager => manager.SendNotification("Title", "Message", notifyTime),
            Times.Once);
    }

    [Test]
    public void SendNotification_WhenScheduledManagerThrows_LogsErrorAndDoesNotThrow()
    {
        var notifyTime = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Local);
        var exception = new InvalidOperationException("Scheduling failure");
        _notificationManagerMock
            .Setup(manager => manager.SendNotification("Title", "Message", notifyTime))
            .Throws(exception);

        Assert.DoesNotThrow(() => _sut.SendNotification("Title", "Message", notifyTime));

        AssertErrorLogged("Failed to schedule notification", exception);
    }

    [Test]
    public async Task RequestPermissionAsync_OnNonAndroidPlatform_ReturnsTrue()
    {
        var result = await _sut.RequestPermissionAsync();

        Assert.That(result, Is.True);
    }

    private void AssertErrorLogged(string expectedMessage, Exception expectedException)
    {
        var logInvocation = _loggerMock.Invocations.Single(
            invocation => invocation.Method.Name == nameof(ILogger.Log));

        Assert.Multiple(() =>
        {
            Assert.That(logInvocation.Arguments[0], Is.EqualTo(LogLevel.Error));
            Assert.That(logInvocation.Arguments[2]?.ToString(), Is.EqualTo(expectedMessage));
            Assert.That(logInvocation.Arguments[3], Is.SameAs(expectedException));
        });
    }
}

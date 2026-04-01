namespace ReceptyOks.Services;

/// <summary>
/// Cross-platform abstraction for sending and receiving local OS notifications.
/// Each platform provides its own implementation.
/// </summary>
public interface INotificationManagerService
{
    /// <summary>
    /// Raised when a notification is received while the app is in the foreground.
    /// </summary>
    event EventHandler<NotificationEventArgs>? NotificationReceived;

    /// <summary>
    /// Sends a local notification immediately or at a scheduled time.
    /// </summary>
    void SendNotification(string title, string message, DateTime? notifyTime = null);

    /// <summary>
    /// Processes a notification received by the underlying platform.
    /// </summary>
    void ReceiveNotification(string title, string message);

}

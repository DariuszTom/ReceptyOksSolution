using Microsoft.Extensions.Logging;

namespace ReceptyOks.Services;

/// <summary>
/// Service for dispatching OS-level local notifications from background work.
/// Falls back to no-op if the platform notification service is unavailable.
/// </summary>
public sealed class AppNotification(
    INotificationManagerService notificationManager,
    ILogger<AppNotification> logger)
{
    /// <summary>
    /// Sends an OS-level notification (appears in status bar / notification drawer).
    /// </summary>
    public void SendNotification(string title, string message)
    {
        try
        {
            notificationManager.SendNotification(title, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification");
        }
    }

    /// <summary>
    /// Sends a scheduled OS-level notification at a specific time.
    /// </summary>
    public void SendNotification(string title, string message, DateTime notifyTime)
    {
        try
        {
            notificationManager.SendNotification(title, message, notifyTime);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule notification");
        }
    }

    /// <summary>
    /// Requests notification permission on platforms that require it (Android 13+).
    /// Should be called early in app lifecycle.
    /// </summary>
    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
#if ANDROID
            var status = await Permissions.RequestAsync<ReceptyOks.Platforms.Android.NotificationPermission>();
            return status == PermissionStatus.Granted;
#else
            await Task.CompletedTask;
            return true;
#endif
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to request notification permission");
            return false;
        }
    }
}

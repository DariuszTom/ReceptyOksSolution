using AsyncAwaitBestPractices;
using ReceptyOks.Services;

namespace ReceptyOks.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="INotificationManagerService"/> using
/// <see cref="SnackBarHelper"/> for in-app notifications, since Windows does
/// not support native local notifications in .NET MAUI out of the box.
/// </summary>
internal sealed class SnackbarNotificationService : INotificationManagerService
{
    private readonly WeakEventManager<NotificationEventArgs> _notificationEventManager = new();

    public event EventHandler<NotificationEventArgs>? NotificationReceived
    {
        add => _notificationEventManager.AddEventHandler(value);
        remove => _notificationEventManager.RemoveEventHandler(value);
    }

    public void SendNotification(string title, string message, DateTime? notifyTime = null)
    {
        if (notifyTime is not null)
        {
            var delay = notifyTime.Value - DateTime.Now;
            if (delay > TimeSpan.Zero)
            {
                ScheduleAsync(title, message, delay).SafeFireAndForget();
                return;
            }
        }

        ShowSnackbar(title, message);
    }

    public void ReceiveNotification(string title, string message)
    {
        _notificationEventManager.RaiseEvent(this, new NotificationEventArgs
        {
            Title = title,
            Message = message
        }, nameof(NotificationReceived));
    }

    private static async Task ScheduleAsync(string title, string message, TimeSpan delay)
    {
        await Task.Delay(delay).ConfigureAwait(false);
        ShowSnackbar(title, message);
    }

    private static void ShowSnackbar(string title, string message)
    {
        var text = string.IsNullOrWhiteSpace(title)
            ? message
            : $"{title}: {message}";

        MainThread.BeginInvokeOnMainThread(async () =>
            await SnackBarHelper.ShowInfoSnackbarAsync(text));
    }
}

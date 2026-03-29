using Android.Content;

namespace ReceptyOks.Platforms.Android;

/// <summary>
/// Receives scheduled alarm broadcasts and shows a notification.
/// </summary>
[BroadcastReceiver(Enabled = true, Label = "Shopping List Notification Broadcast Receiver")]
public class AlarmHandler : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Extras is null)
            return;

        string title = intent.GetStringExtra(NotificationManagerService.TitleKey) ?? string.Empty;
        string message = intent.GetStringExtra(NotificationManagerService.MessageKey) ?? string.Empty;

        var manager = NotificationManagerService.Instance ?? new NotificationManagerService();
        manager.Show(title, message);
    }
}

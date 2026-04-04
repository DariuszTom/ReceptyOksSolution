using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AsyncAwaitBestPractices;
using ReceptyOks.Services;

namespace ReceptyOks.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="INotificationManagerService"/> using the native notification system.
/// Notifications appear in the status bar even when the app is not visible.
/// </summary>
public class NotificationManagerService : INotificationManagerService
{
    private const string ChannelId = "receptyoks_shopping";
    private const string ChannelName = "Lista zakupów";
    private const string ChannelDescription = "Powiadomienia o nowych elementach na liście zakupów.";

    public const string TitleKey = "title";
    public const string MessageKey = "message";

    private bool _channelInitialized;
    private int _messageId = System.Environment.TickCount;
    private int _pendingIntentId = System.Environment.TickCount;

    private NotificationManagerCompat? _compatManager = null!;

    private readonly WeakEventManager<NotificationEventArgs> _notificationEventManager = new();

    public event EventHandler<NotificationEventArgs>? NotificationReceived
    {
        add => _notificationEventManager.AddEventHandler(value);
        remove => _notificationEventManager.RemoveEventHandler(value);
    }

    public static NotificationManagerService? Instance { get; private set; }

    public NotificationManagerService()
    {
        CreateNotificationChannel();
        _compatManager = NotificationManagerCompat.From(Platform.AppContext);
        Instance ??= this;
    }

    public void SendNotification(string title, string message, DateTime? notifyTime = null)
    {
        if (!_channelInitialized)
        {
            CreateNotificationChannel();
        }

        if (notifyTime is not null)
        {
            var intent = new Intent(Platform.AppContext, typeof(AlarmHandler));
            intent.PutExtra(TitleKey, title);
            intent.PutExtra(MessageKey, message);
            intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                ? PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable
                : PendingIntentFlags.CancelCurrent;

            var pendingIntent = PendingIntent.GetBroadcast(
                Platform.AppContext, _pendingIntentId++, intent, pendingIntentFlags);

            long triggerTime = GetNotifyTime(notifyTime.Value);
            var alarmManager = Platform.AppContext.GetSystemService(Context.AlarmService) as AlarmManager;
            alarmManager?.Set(AlarmType.RtcWakeup, triggerTime, pendingIntent);
        }
        else
        {
            Show(title, message);
        }
    }

    public void ReceiveNotification(string title, string message)
    {
        _notificationEventManager.RaiseEvent(this, new NotificationEventArgs
        {
            Title = title,
            Message = message
        }, nameof(NotificationReceived));
    }

    /// <summary>
    /// Shows a notification in the Android status bar immediately.
    /// </summary>
    public void Show(string title, string message)
    {
        var intent = new Intent(Platform.AppContext, typeof(MainActivity));
        intent.PutExtra(TitleKey, title);
        intent.PutExtra(MessageKey, message);
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetActivity(
            Platform.AppContext, _pendingIntentId++, intent, pendingIntentFlags);

        var builder = new NotificationCompat.Builder(Platform.AppContext, ChannelId)
            .SetContentIntent(pendingIntent)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetAutoCancel(true);

        _compatManager.Notify(_messageId++, builder.Build());
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channelNameJava = new Java.Lang.String(ChannelName);
            var channel = new NotificationChannel(ChannelId, channelNameJava, NotificationImportance.Default)
            {
                Description = ChannelDescription
            };

            var manager = (NotificationManager)Platform.AppContext.GetSystemService(Context.NotificationService)!;
            manager.CreateNotificationChannel(channel);
        }
        _channelInitialized = true;
    }

    private static long GetNotifyTime(DateTime notifyTime)
    {
        DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(notifyTime);
        long utcAlarmTime = new DateTimeOffset(utcTime).ToUnixTimeMilliseconds();
        return utcAlarmTime;
    }
}

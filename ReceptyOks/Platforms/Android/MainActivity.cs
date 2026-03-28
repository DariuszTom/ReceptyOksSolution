using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using ReceptyOks.Services;

namespace ReceptyOks;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CreateNotificationFromIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        CreateNotificationFromIntent(intent);
    }

    private static void CreateNotificationFromIntent(Intent? intent)
    {
        if (intent?.Extras is null)
            return;

        string? title = intent.GetStringExtra(Platforms.Android.NotificationManagerService.TitleKey);
        string? message = intent.GetStringExtra(Platforms.Android.NotificationManagerService.MessageKey);

        if (title is null && message is null)
            return;

        var service = IPlatformApplication.Current?.Services.GetService<INotificationManagerService>();
        service?.ReceiveNotification(title ?? string.Empty, message ?? string.Empty);
    }
}

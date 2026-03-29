using CommunityToolkit.Maui.ApplicationModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReceptyOks.Models;
using ReceptyOks.Services;

namespace ReceptyOks;

public partial class App : Application
{
    private readonly IBadge _badge;

    public App(IBadge badge, IEnumerable<IHostedService> hostedServices, IServiceProvider serviceProvider, ILogger<App> logger)
    {
        InitializeComponent();
        _badge = badge;

        // Request notification permission before starting hosted services
        // so ShopingListNotification can deliver alerts on Android 13+.
        var appNotification = serviceProvider.GetRequiredService<AppNotification>();
        RequestNotificationPermissionAsync(appNotification, logger).ContinueWith(_ =>
        {
            // MAUI does not auto-start hosted services; start them manually.
            Array.ForEach(
                hostedServices.ToArray(),
                s => _ = s.StartAsync(CancellationToken.None));
        }, TaskScheduler.Default);

        WeakReferenceMessenger.Default.Register<BadgeCountMessage>(this, (r, m) =>
              {
                  try
                  {
#if ANDROID
                      _badge.SetCount(m.Count);
#endif
                  }
                  catch (Exception)
                  {
                      // Badge API not available (unpackaged app or unsupported Windows configuration)
                  }
              });
    }

    private static async Task RequestNotificationPermissionAsync(AppNotification appNotification, ILogger logger)
    {
        var granted = await appNotification.RequestPermissionAsync().ConfigureAwait(false);
        if (!granted)
        {
            logger.LogWarning("Notification permission was denied — shopping list alerts will not be shown");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
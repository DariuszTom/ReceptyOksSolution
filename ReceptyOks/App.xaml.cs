using CommunityToolkit.Maui.ApplicationModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Hosting;
using ReceptyOks.Models;
using ReceptyOks.Services;

namespace ReceptyOks;

public partial class App : Application
{
    private readonly IBadge _badge;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IHostedService> _hostedServices;
    private readonly ILogger<App> _logger;
    private int _initialized;

    public App(IBadge badge, IEnumerable<IHostedService> hostedServices, IServiceProvider serviceProvider, ILogger<App> logger)
    {
        InitializeComponent();
        _badge = badge;
        _serviceProvider = serviceProvider;
        _hostedServices = hostedServices;
        _logger = logger;

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

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        // Defer permission request and hosted service startup until the Activity
        // is fully initialized. Requesting permissions in the App constructor can
        // fail with NullReferenceException on devices where Platform.CurrentActivity
        // is not yet set during OnCreate.
        window.Activated += OnWindowActivated;

        return window;
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            return;

        try
        {
            var appNotification = _serviceProvider.GetRequiredService<AppNotification>();
            var granted = await appNotification.RequestPermissionAsync().ConfigureAwait(false);
            if (!granted)
            {
                _logger.LogWarning("Notification permission was denied — shopping list alerts will not be shown");
            }

            // MAUI does not auto-start hosted services; start them manually.
            foreach (var service in _hostedServices)
            {
                await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize notification permission or hosted services");
        }
    }
}
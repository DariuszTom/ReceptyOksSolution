using CommunityToolkit.Maui.ApplicationModel;
using CommunityToolkit.Mvvm.Messaging;
using ReceptyOks.Models;

namespace ReceptyOks;

public partial class App : Application
{
    private readonly IBadge _badge;

    public App(IBadge badge)
    {
        InitializeComponent();
        _badge = badge;

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
        return new Window(new AppShell());
    }
}
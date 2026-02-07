using CommunityToolkit.Maui.ApplicationModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
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
        _badge.SetCount(m.Count);
 });
    }

    protected override Window CreateWindow(IActivationState? activationState)
 {
   return new Window(new AppShell());
    }
}
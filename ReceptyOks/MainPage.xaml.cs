using CommunityToolkit.Maui.ApplicationModel;
using CommunityToolkit.Mvvm.Messaging;
using ReceptyOks.Models;
namespace ReceptyOks;

public partial class MainPage : ContentPage
{
    private readonly IBadge badge;

    public MainPage(IBadge badge)
    {
        InitializeComponent();
        this.badge = badge;

        WeakReferenceMessenger.Default.Register<BadgeCountMessage>(this, (r, m) =>
        {
            badge.SetCount(m.Count);
        });
    }
}

using CommunityToolkit.Maui.ApplicationModel;

namespace ReceptyOks;

public partial class MainPage : ContentPage
{
    private readonly IBadge badge;
    public MainPage()
	{
		InitializeComponent();
        this.badge = badge;
    }
    public void SetCount(uint value)
    {
        badge.SetCount(value);
    }
}

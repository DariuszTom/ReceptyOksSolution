using Android.Content;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace ReceptyOks.Platforms.Android.Handlers;

public class CustomShellRenderer : ShellRenderer
{
    public CustomShellRenderer(Context context) : base(context)
    {
    }

    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
    {
        return new CustomBottomNavViewAppearanceTracker(this, shellItem);
    }
}

public class CustomBottomNavViewAppearanceTracker : ShellBottomNavViewAppearanceTracker
{
    public CustomBottomNavViewAppearanceTracker(IShellContext shellContext, ShellItem shellItem)
        : base(shellContext, shellItem)
    {
    }

    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);

        // Wy³¹cz domyœlny indicator (podkreœlenie pod zak³adk¹)
        bottomView.ItemActiveIndicatorEnabled = false;
    }
}

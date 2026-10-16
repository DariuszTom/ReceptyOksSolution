using UraniumUI.Handlers;

namespace ReceptyOks.Handlers;

/// <summary>
/// UraniumUI 3.0.0's StatefulButtonHandler resets the visual state in DisconnectHandler, after MAUI
/// has already cleared PlatformView. Leaving a page while a button is hovered/pressed then maps
/// TextColor etc. onto a missing native view and crashes ("PlatformView cannot be null here").
/// Property updates without a native view have nothing to update, so they are skipped.
/// </summary>
public class SafeStatefulButtonHandler : StatefulButtonHandler
{
    public override void UpdateValue(string property)
    {
        if (((IElementHandler)this).PlatformView is null)
            return;

        base.UpdateValue(property);
    }
}

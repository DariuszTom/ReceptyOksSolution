using Microsoft.Maui.Handlers;

namespace ReceptyOks.Controls;

/// <summary>
/// Editor without native chrome (no border, underline or focus highlight) so it blends into its container.
/// </summary>
public class BorderlessEditor : Editor
{
    /// <summary>
    /// Registers the handler mapping that strips native chrome. Call once at startup.
    /// </summary>
    public static void ConfigureHandler()
    {
        EditorHandler.Mapper.AppendToMapping(nameof(BorderlessEditor), (handler, view) =>
        {
            if (view is not BorderlessEditor)
            {
                return;
            }

#if WINDOWS
            var textBox = handler.PlatformView;
            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            var zero = new Microsoft.UI.Xaml.Thickness(0);

            foreach (var key in new[]
            {
                "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushDisabled",
                "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
            })
            {
                textBox.Resources[key] = transparent;
            }

            textBox.Resources["TextControlBorderThemeThickness"] = zero;
            textBox.Resources["TextControlBorderThemeThicknessFocused"] = zero;
            textBox.BorderThickness = zero;
            textBox.Background = transparent;
            textBox.UseSystemFocusVisuals = false;
            textBox.FocusVisualPrimaryThickness = zero;
            textBox.FocusVisualSecondaryThickness = zero;
#elif ANDROID
            var editText = handler.PlatformView;
            editText.Background = null;
            editText.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            editText.SetPadding(0, editText.PaddingTop, 0, editText.PaddingBottom);
#elif IOS || MACCATALYST
            handler.PlatformView.Layer.BorderWidth = 0;
#endif
        });
    }
}

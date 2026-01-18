using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ReceptyOks.Converters
{
    public class EyeIconConverter : IValueConverter
    {
        // MaterialSymbols: "visibility" = \ue8f4, "visibility_off" = \ue8f5
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            // Return the glyph for the correct icon
            return isVisible ? "\ue8f5" : "\ue8f4";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

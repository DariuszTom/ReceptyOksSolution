using System.Globalization;

namespace ReceptyOks.Converters;

public class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ByteArrayToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
        {
            return null; // brak obrazu -> null
        }

        // tworzymy ImageSource ze streamu
        return ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class IconSelectionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is string currentIcon && values[1] is string selectedIcon)
        {
            return currentIcon == selectedIcon ? Application.Current!.Resources["Primary"] : Colors.Transparent;
        }
        return Colors.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CategoryIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string iconName && !string.IsNullOrEmpty(iconName))
        {
            return iconName;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class HasCategoryIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string iconName && !string.IsNullOrEmpty(iconName);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Zwraca true gdy wartoœæ jest null lub pusta tablica bajtów.
/// U¿ywany do pokazania fallback ikony.
/// </summary>
public class IsNullOrEmptyBytesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return true;
        if (value is byte[] bytes && bytes.Length == 0) return true;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            bool isDarkMode = Application.Current?.RequestedTheme == AppTheme.Dark;

            if (isDarkMode)
            {
                return level switch
                {
                    "Debug" => Color.FromArgb("#B0B0B0"),      // Light Gray
                    "Information" => Color.FromArgb("#64B5F6"), // Light Blue
                    "Warning" => Color.FromArgb("#FFB74D"),    // Light Orange
                    "Error" => Color.FromArgb("#EF5350"),      // Light Red
                    "Fatal" => Color.FromArgb("#EC407A"),      // Light Pink
                    _ => Color.FromArgb("#E0E0E0")
                };
            }
            else
            {
                return level switch
                {
                    "Debug" => Color.FromArgb("#6E6E6E"),      // Gray500
                    "Information" => Color.FromArgb("#1976D2"), // Blue
                    "Warning" => Color.FromArgb("#F57C00"),    // Orange
                    "Error" => Color.FromArgb("#D32F2F"),      // Red
                    "Fatal" => Color.FromArgb("#C2185B"),      // Dark Pink
                    _ => Color.FromArgb("#212121")             // Gray900
                };
            }
        }
        return Color.FromArgb("#212121");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LogLevelToBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            // SprawdŸ czy jesteœmy w dark mode
            bool isDarkMode = Application.Current?.RequestedTheme == AppTheme.Dark;

            if (isDarkMode)
            {
                return level switch
                {
                    "Debug" => Color.FromArgb("#404040"),      // Gray600
                    "Information" => Color.FromArgb("#1565C0"), // Dark Blue
                    "Warning" => Color.FromArgb("#E65100"),    // Dark Orange
                    "Error" => Color.FromArgb("#C62828"),      // Dark Red
                    "Fatal" => Color.FromArgb("#880E4F"),      // Dark Pink
                    _ => Color.FromArgb("#303030")
                };
            }
            else
            {
                return level switch
                {
                    "Debug" => Color.FromArgb("#E1E1E1"),      // Gray100
                    "Information" => Color.FromArgb("#E3F2FD"), // Light Blue
                    "Warning" => Color.FromArgb("#FFF3E0"),    // Light Orange
                    "Error" => Color.FromArgb("#FFEBEE"),      // Light Red
                    "Fatal" => Color.FromArgb("#FCE4EC"),      // Light Pink
                    _ => Color.FromArgb("#F5F5F5")
                };
            }
        }
        return Color.FromArgb("#F5F5F5");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LogLevelToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            return level switch
            {
                "Debug" => "??",
                "Information" => "??",
                "Warning" => "??",
                "Error" => "?",
                "Fatal" => "??",
                _ => "??"
            };
        }
        return "??";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringIsNotNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string str && !string.IsNullOrEmpty(str);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }
}

public class IsNotZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            int intValue => intValue != 0,
            double doubleValue => doubleValue != 0,
            decimal decimalValue => decimalValue != 0,
            _ => false
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a boolean (IsExpanded) to a Material Symbol chevron icon.
/// Returns expand_more when collapsed (false), expand_less when expanded (true).
/// </summary>
public class BoolToExpandIconConverter : IValueConverter
{
    // Material Symbols: expand_less = U+E5CE, expand_more = U+E5CF
    private const string ExpandLess = "\ue5ce";
    private const string ExpandMore = "\ue5cf";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? ExpandLess : ExpandMore;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
   => throw new NotImplementedException();
}

/// <summary>
/// Returns true when the string value is not null or empty.
/// </summary>
public class IsNotNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string str && !string.IsNullOrEmpty(str);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
      => throw new NotImplementedException();
}

/// <summary>
/// Konwertuje liczbê posi³ków na wysokoœæ CollectionView.
/// Zwraca 0 gdy brak posi³ków, lub 44 * count dla widocznych elementów.
/// </summary>
public class MealCountToHeightConverter : IValueConverter
{
    private const int ItemHeight = 44;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count && count > 0)
        {
            return count * ItemHeight;
        }
        return 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a boolean to TextDecorations.Strikethrough when true.
/// </summary>
public class BoolToStrikethroughConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TextDecorations.Strikethrough : TextDecorations.None;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts IsListening boolean to appropriate colors for recording button.
/// Parameter values: "Recording" (background), "RecordingBorder" (border), "RecordingIcon" (icon color).
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isListening = value is true;
        string param = parameter as string ?? "Recording";

        return param switch
        {
            "Recording" => isListening ? Color.FromArgb("#E53935") : Colors.Transparent,
            "RecordingBorder" => isListening ? Color.FromArgb("#E53935") : Application.Current?.Resources["Primary"] as Color ?? Colors.Blue,
            "RecordingIcon" => isListening ? Colors.White : Application.Current?.Resources["Primary"] as Color ?? Colors.Blue,
            _ => Colors.Transparent
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    => throw new NotImplementedException();
}

/// <summary>
/// Converts IsListening boolean to microphone or pause glyph.
/// Material Symbols: mic = U+E029, pause = U+E034.
/// </summary>
public class BoolToGlyphConverter : IValueConverter
{
    private const string MicGlyph = "\ue029";
    private const string PauseGlyph = "\ue034";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? PauseGlyph : MicGlyph;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

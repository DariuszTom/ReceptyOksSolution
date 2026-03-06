using System.Globalization;

namespace ReceptyOks.Converters;

/// <summary>
/// Converts a <see cref="Color"/> to a border thickness value. Returns 3 when the bound color
/// matches the converter parameter color, otherwise 0. Used for the selected-color indicator.
/// </summary>
public sealed class ColorMatchToBorderConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Color selectedColor
			&& parameter is string paramColorName
			&& Color.TryParse(paramColorName, out var paramColor))
		{
			return selectedColor.ToArgbHex() == paramColor.ToArgbHex() ? 3d : 0d;
		}

		return 0d;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
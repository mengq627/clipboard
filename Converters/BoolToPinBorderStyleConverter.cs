using System.Globalization;
using Microsoft.Maui.Controls;

namespace clipboard.Converters;

public class BoolToPinBorderStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPinned && isPinned)
        {
            return Application.Current?.Resources["PinnedIconButtonBorderStyle"] as Style;
        }
        return Application.Current?.Resources["IconButtonBorderStyle"] as Style;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


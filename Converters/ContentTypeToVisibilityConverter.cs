using System.Globalization;
using Microsoft.Maui.Controls;

namespace clipboard.Converters;

/// <summary>
/// 根据ContentType和参数判断是否可见
/// </summary>
public class ContentTypeToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string contentType && parameter is string expectedType)
        {
            return contentType == expectedType;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


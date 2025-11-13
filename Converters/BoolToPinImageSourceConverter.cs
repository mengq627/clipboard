using System.Globalization;

namespace clipboard.Converters;

public class BoolToPinImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPinned)
        {
            // 如果已置顶，返回填充的图标，否则返回未填充的图标
            return isPinned ? "pin_filled_icon" : "pin_icon";
        }
        return "pin_icon";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


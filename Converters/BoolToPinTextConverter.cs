using System.Globalization;

namespace clipboard.Converters;

public class BoolToPinTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPinned)
        {
            return isPinned ? "取消置顶" : "置顶";
        }
        return "置顶";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


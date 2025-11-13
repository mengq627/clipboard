using System.Globalization;

namespace clipboard.Converters;

public class BoolToPinIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPinned)
        {
            // 置顶图标: E840, 取消置顶图标: E77A
            return isPinned ? "\uE77A" : "\uE840";
        }
        return "\uE840";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


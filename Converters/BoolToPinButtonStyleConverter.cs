using System.Globalization;
using Microsoft.Maui.Controls;

namespace clipboard.Converters;

public class BoolToPinButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPinned && isPinned)
        {
            // 已置顶时返回亮色样式
            return Application.Current?.Resources["PinnedIconButtonStyle"] as Style;
        }
        // 未置顶时返回默认样式
        return Application.Current?.Resources["IconButtonStyle"] as Style;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


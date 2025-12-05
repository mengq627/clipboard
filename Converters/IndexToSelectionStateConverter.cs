using System.Globalization;

namespace clipboard.Converters;

/// <summary>
/// 将项目索引和选中索引比较，返回是否选中
/// </summary>
public class IndexToSelectionStateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentIndex && parameter is int selectedIndex)
        {
            return currentIndex == selectedIndex ? "Selected" : "Normal";
        }
        return "Normal";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


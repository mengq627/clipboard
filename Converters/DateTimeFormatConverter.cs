using System.Globalization;

namespace clipboard.Converters;

public class DateTimeFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            var now = DateTime.Now;
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var date = dateTime.Date;
            
            // 今天
            if (date == today)
            {
                return dateTime.ToString("今天 HH:mm", culture);
            }
            // 昨天
            else if (date == yesterday)
            {
                return dateTime.ToString("昨天 HH:mm", culture);
            }
            // 本周内（最近7天）
            else if ((today - date).TotalDays <= 7)
            {
                var dayOfWeek = dateTime.DayOfWeek;
                string dayName = dayOfWeek switch
                {
                    DayOfWeek.Monday => "周一",
                    DayOfWeek.Tuesday => "周二",
                    DayOfWeek.Wednesday => "周三",
                    DayOfWeek.Thursday => "周四",
                    DayOfWeek.Friday => "周五",
                    DayOfWeek.Saturday => "周六",
                    DayOfWeek.Sunday => "周日",
                    _ => dayOfWeek.ToString()
                };
                return $"{dayName} {dateTime:HH:mm}";
            }
            // 超过一周，显示完整日期
            else
            {
                return dateTime.ToString("yyyy-MM-dd HH:mm", culture);
            }
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


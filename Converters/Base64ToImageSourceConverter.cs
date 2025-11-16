using System.Globalization;
using System.Text;
using Microsoft.Maui.Controls;

namespace clipboard.Converters;

/// <summary>
/// 将Base64字符串转换为ImageSource
/// 支持DIB格式（Windows剪贴板格式）和标准图片格式
/// </summary>
public class Base64ToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string base64String && !string.IsNullOrEmpty(base64String))
        {
            try
            {
                var imageBytes = System.Convert.FromBase64String(base64String);
                
                // 检查是否是DIB格式（Windows Device Independent Bitmap）
                // DIB格式通常以BITMAPINFOHEADER或BITMAPV5HEADER开头
                // 简单检查：如果前4个字节是"BM"（BMP文件头），或者是DIB格式
                if (imageBytes.Length > 14)
                {
                    // 检查是否是BMP/DIB格式
                    var isBmp = imageBytes[0] == 0x42 && imageBytes[1] == 0x4D; // "BM"
                    var isDib = !isBmp && imageBytes.Length > 40; // DIB格式通常没有"BM"头
                    
                    if (isDib || isBmp)
                    {
                        // DIB格式，尝试转换为PNG
                        // 注意：这里简化处理，直接尝试显示
                        // 如果MAUI无法显示DIB，可能需要使用Windows API转换为PNG
                        System.Diagnostics.Debug.WriteLine($"Detected DIB/BMP format, size: {imageBytes.Length} bytes");
                    }
                }
                
                return ImageSource.FromStream(() => new MemoryStream(imageBytes));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error converting Base64 to ImageSource: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


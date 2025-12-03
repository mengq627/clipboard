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
                // 清理 Base64 字符串：移除空白字符（空格、换行符等）
                base64String = base64String.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
                
                // 验证 Base64 字符串格式
                if (string.IsNullOrEmpty(base64String))
                {
                    System.Diagnostics.Debug.WriteLine("Base64 string is empty after cleaning");
                    return null;
                }
                
                // 检查 Base64 字符串长度（必须是 4 的倍数，或者需要填充）
                var remainder = base64String.Length % 4;
                if (remainder > 0)
                {
                    // 添加填充字符
                    base64String = base64String.PadRight(base64String.Length + (4 - remainder), '=');
                }
                
                // 验证是否包含有效的 Base64 字符
                foreach (var c in base64String)
                {
                    if (!char.IsLetterOrDigit(c) && c != '+' && c != '/' && c != '=')
                    {
                        System.Diagnostics.Debug.WriteLine($"Invalid Base64 character found: '{c}' ({(int)c})");
                        return null;
                    }
                }
                
                var imageBytes = System.Convert.FromBase64String(base64String);
                
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Decoded image bytes are null or empty");
                    return null;
                }
                
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
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error converting Base64 to ImageSource (FormatException): {ex.Message}");
                var length = base64String?.Length ?? 0;
                var preview = base64String != null && length > 0 
                    ? base64String.Substring(0, Math.Min(100, length)) 
                    : "null";
                System.Diagnostics.Debug.WriteLine($"Base64 string length: {length}, first 100 chars: {preview}");
                return null;
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


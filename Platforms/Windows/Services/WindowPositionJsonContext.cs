using System.Text.Json.Serialization;
using System.Text.Json;

namespace clipboard.Platforms.Windows.Services;

/// <summary>
/// 窗口位置数据类
/// </summary>
internal class WindowPosition
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// JSON 序列化上下文，用于裁剪兼容性
/// 确保 WindowPosition 类型在裁剪时被保留
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WindowPosition))]
internal partial class WindowPositionJsonContext : JsonSerializerContext
{
}


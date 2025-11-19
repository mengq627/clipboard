using System.Text.Json.Serialization;
using System.Text.Json;

namespace clipboard.Models;

/// <summary>
/// JSON 序列化上下文，用于裁剪兼容性
/// 确保 ClipboardItem 和 ClipboardGroup 类型在裁剪时被保留
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ClipboardItem))]
[JsonSerializable(typeof(ClipboardGroup))]
[JsonSerializable(typeof(List<ClipboardItem>))]
[JsonSerializable(typeof(List<ClipboardGroup>))]
[JsonSerializable(typeof(ClipboardData))]
internal partial class ClipboardJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 内部数据类，用于序列化
/// </summary>
internal class ClipboardData
{
    public List<ClipboardItem> Items { get; set; } = new();
    public List<ClipboardGroup> Groups { get; set; } = new();
}


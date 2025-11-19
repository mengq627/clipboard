using System.Text.Json.Serialization;
using System.Text.Json;

namespace clipboard.Models;

/// <summary>
/// JSON 序列化上下文，用于裁剪兼容性
/// 确保 AppSettings 和 HotkeyConfig 类型在裁剪时被保留
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HotkeyConfig))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}


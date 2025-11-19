using System.Text.Json.Serialization;

namespace clipboard.Models;

/// <summary>
/// 应用设置
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 每个分组（包括未分组）的最大内容个数
    /// </summary>
    public int MaxItemsPerGroup { get; set; } = 100;
    
    /// <summary>
    /// 快捷键配置
    /// </summary>
    public HotkeyConfig Hotkey { get; set; } = new();
}

/// <summary>
/// 快捷键配置
/// </summary>
public class HotkeyConfig
{
    /// <summary>
    /// 是否使用Win键
    /// </summary>
    public bool UseWinKey { get; set; } = true;
    
    /// <summary>
    /// 是否使用Alt键（与Win键互斥）
    /// </summary>
    public bool UseAltKey { get; set; } = false;
    
    /// <summary>
    /// 快捷键的字母键（如 'V'）
    /// </summary>
    public char Key { get; set; } = 'V';
}


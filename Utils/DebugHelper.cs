using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace clipboard.Utils;

/// <summary>
/// 调试辅助类，提供带文件名、函数名和行号的调试输出
/// </summary>
public static class DebugHelper
{
    [Conditional("DEBUG")]
    /// <summary>
    /// 输出调试信息，包含文件名、函数名和行号
    /// </summary>
    /// <param name="message">要输出的消息</param>
    /// <param name="memberName">调用方法名（自动填充）</param>
    /// <param name="filePath">文件路径（自动填充）</param>
    /// <param name="lineNumber">行号（自动填充）</param>
    public static void DebugWrite(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        System.Diagnostics.Debug.WriteLine($"[{fileName}:{memberName}:{lineNumber}] {message}");
    }
}


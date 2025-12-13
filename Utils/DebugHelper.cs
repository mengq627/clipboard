using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace clipboard.Utils;

/// <summary>
/// 调试辅助类，提供带文件名、函数名和行号的调试输出
/// </summary>
public static class DebugHelper
{
    private static readonly object _logLock = new object();
    private static string? _logFilePath;
    private static bool _enableFileLogging = false;

    static DebugHelper()
    {
        _logFilePath = Path.Combine(AppContext.BaseDirectory, "app.log");
    }

    /// <summary>
    /// 设置是否启用文件日志
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public static void SetFileLoggingEnabled(bool enabled)
    {
        _enableFileLogging = enabled;
    }

    /// <summary>
    /// 获取是否启用文件日志
    /// </summary>
    public static bool IsFileLoggingEnabled => _enableFileLogging;

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
        var fileName = Path.GetFileName(filePath);
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{fileName}:{memberName}:{lineNumber}] {message}";

        System.Diagnostics.Debug.WriteLine(logMessage);

        // 如果启用了文件日志，也写入文件
        if (_enableFileLogging)
        {
            WriteToFile(logMessage);
        }
    }

    /// <summary>
    /// 写入日志到文件（无论是否在 DEBUG 模式下）
    /// </summary>
    private static void WriteToFile(string message)
    {
        if (string.IsNullOrEmpty(_logFilePath))
            return;

        lock (_logLock)
        {
            try
            {
                // 使用追加模式写入文件
                using (var writer = new StreamWriter(_logFilePath, append: true, encoding: Encoding.UTF8))
                {
                    writer.WriteLine(message);
                }
            }
            catch
            {
                // 忽略写入错误，避免影响主程序运行
            }
        }
    }
}


//#define LOG_TO_FILE

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace clipboard.Utils;

/// <summary>
/// 调试辅助类，提供带文件名、函数名和行号的调试输出
/// </summary>
public static class DebugHelper
{
#if LOG_TO_FILE
    private static readonly object _logLock = new object();
    private static string? _logFilePath;
    
    static DebugHelper()
    {
        _logFilePath  = Path.Combine(AppContext.BaseDirectory, "app.log");
    }
#endif

    /// <summary>
    /// 输出调试信息，包含文件名、函数名和行号
    /// </summary>
    /// <param name="message">要输出的消息</param>
    /// <param name="memberName">调用方法名（自动填充）</param>
    /// <param name="filePath">文件路径（自动填充）</param>
    /// <param name="lineNumber">行号（自动填充）</param>
#if DEBUG
    [Conditional("DEBUG")]
#elif LOG_TO_FILE
    // 在 Release 模式下，如果定义了 LOG_TO_FILE，方法存在但不使用 Conditional
    // 这样调用不会被优化掉，会执行文件写入
#else
    // 如果既没有 DEBUG 也没有 LOG_TO_FILE，使用 Conditional 让调用被优化掉
    [Conditional("NEVER_DEFINED")]
#endif
    public static void DebugWrite(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
#if DEBUG || LOG_TO_FILE
        var fileName = Path.GetFileName(filePath);
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{fileName}:{memberName}:{lineNumber}] {message}";
        
#if DEBUG
        System.Diagnostics.Debug.WriteLine(logMessage);
#endif

#if LOG_TO_FILE
        WriteToFile(logMessage);
#endif
#endif
    }

#if LOG_TO_FILE
    [Conditional("LOG_TO_FILE")]
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
#endif
}


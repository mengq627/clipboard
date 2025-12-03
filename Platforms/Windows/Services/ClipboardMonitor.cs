#if WINDOWS
using System;
using System.Runtime.InteropServices;
using clipboard.Utils;

namespace clipboard.Platforms.Windows.Services;

/// <summary>
/// 提供了基于 Windows 消息的剪贴板内容更新通知。
/// 使用纯 Win32 API 实现，不依赖 WinForms。
/// </summary>
public sealed class ClipboardMonitor : IDisposable
{
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isDisposed = false;
    private WndProcDelegate? _wndProcDelegate;
    
    /// <summary>
    /// 当剪贴板内容更新时触发。
    /// </summary>
    public event EventHandler? ClipboardUpdate;

    /// <summary>
    /// 构造函数，初始化并开始监听剪贴板。
    /// </summary>
    public ClipboardMonitor()
    {
        // 创建消息窗口
        _hwnd = CreateMessageWindow();
        
        if (_hwnd == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to create message window. Error: {error}");
        }
        
        // 注册剪贴板监听
        if (!NativeMethods.AddClipboardFormatListener(_hwnd))
        {
            var error = Marshal.GetLastWin32Error();
            DestroyMessageWindow();
            throw new InvalidOperationException($"Failed to add clipboard format listener. Error: {error}");
        }
        
        DebugHelper.DebugWrite("Clipboard monitoring started and listener registered.");
    }
    
    /// <summary>
    /// 触发 <see cref="ClipboardUpdate"/> 事件。
    /// </summary>
    private void OnClipboardUpdate()
    {
        ClipboardUpdate?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// 创建消息窗口（仅用于接收消息，不可见）
    /// </summary>
    private IntPtr CreateMessageWindow()
    {
        // 窗口类名（使用 GUID 确保唯一性）
        var className = $"ClipboardMonitor_{Guid.NewGuid():N}";
        
        // 保存窗口过程委托，防止被 GC 回收（必须在 RegisterClass 之前）
        _wndProcDelegate = WndProc;
        
        // 将委托转换为函数指针
        var wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        
        // 获取模块句柄
        var hInstance = NativeMethods.GetModuleHandle(null);
        
        // 如果窗口类已存在，先取消注册
        NativeMethods.UnregisterClass(className, hInstance);
        
        // 注册窗口类
        var wc = new WNDCLASS
        {
            style = 0,
            lpfnWndProc = wndProcPtr,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = className
        };
        
        var atom = NativeMethods.RegisterClass(ref wc);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to register window class. Error: {error}");
        }
        
        // 创建消息窗口
        var hwnd = NativeMethods.CreateWindowEx(
            0,                              // dwExStyle
            className,                       // lpClassName
            "ClipboardMonitor",             // lpWindowName
            0,                              // dwStyle (0 = 不可见窗口)
            0, 0, 0, 0,                     // x, y, nWidth, nHeight
            NativeMethods.HWND_MESSAGE,      // hWndParent (消息窗口)
            IntPtr.Zero,                    // hMenu
            hInstance,                      // hInstance
            IntPtr.Zero                     // lpParam
        );
        
        return hwnd;
    }
    
    /// <summary>
    /// 窗口过程，处理 Windows 消息
    /// </summary>
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            // 检查实例是否已释放
            if (_isDisposed || _hwnd == IntPtr.Zero)
            {
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }
            
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                // 在后台线程上触发事件，避免阻塞消息循环
                try
                {
                    OnClipboardUpdate();
                }
                catch (Exception ex)
                {
                    // 记录异常但不抛出，避免破坏 Windows 消息循环
                    DebugHelper.DebugWrite($"Error in OnClipboardUpdate: {ex.Message}");
                }
                return IntPtr.Zero;
            }
            
            // 其他消息使用默认处理
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            // 捕获所有异常，避免破坏 Windows 消息循环
            DebugHelper.DebugWrite($"Error in WndProc: {ex.Message}");
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }
    
    /// <summary>
    /// 销毁消息窗口
    /// </summary>
    private void DestroyMessageWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                // 移除剪贴板监听
                NativeMethods.RemoveClipboardFormatListener(_hwnd);
            }
            catch (Exception ex)
            {
                DebugHelper.DebugWrite($"Error removing clipboard listener: {ex.Message}");
            }
            
            try
            {
                // 销毁窗口
                NativeMethods.DestroyWindow(_hwnd);
            }
            catch (Exception ex)
            {
                DebugHelper.DebugWrite($"Error destroying window: {ex.Message}");
            }
            
            _hwnd = IntPtr.Zero;
        }
        
        // 注意：窗口过程委托 (_wndProcDelegate) 必须保持引用直到窗口被销毁
        // 但窗口销毁后，可以安全地清除引用
        // 实际上，我们应该保持引用直到 Dispose 完成，以确保 GC 不会回收它
    }
    
    /// <summary>
    /// 实现 IDisposable 接口，用于释放资源。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            DestroyMessageWindow();
            DebugHelper.DebugWrite("Clipboard monitoring stopped and resources disposed.");
            _isDisposed = true;
        }
    }
    
    // 析构函数（如果调用者忘记调用 Dispose）
    ~ClipboardMonitor()
    {
        Dispose(false);
    }
}

// ----------------------------------------------------

// Windows API 声明
internal static class NativeMethods
{
    // 消息窗口常量
    public static IntPtr HWND_MESSAGE = new IntPtr(-3);
    public const int WM_CLIPBOARDUPDATE = 0x031D;

    // 剪贴板监听 API
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    // 窗口创建和销毁
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    // 窗口类注册
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    // 窗口过程
    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    // 获取模块句柄
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}

// 窗口过程委托
internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

// WNDCLASS 结构（WNDCLASSW - Unicode 版本）
// 注意：lpfnWndProc 必须是函数指针（IntPtr），不能是委托
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WNDCLASS
{
    public uint style;
    public IntPtr lpfnWndProc;  // 函数指针，不是委托
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? lpszMenuName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string lpszClassName;
}
#endif

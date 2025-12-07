#if WINDOWS
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Maui.Platform;
using MauiWindow = Microsoft.Maui.Controls.Window;
using System.Diagnostics;

using clipboard.Utils;

namespace clipboard.Platforms.Windows.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private MauiWindow? _mainWindow;
    private bool _isDisposed = false;
    private Action? _exitHandler;

    public void Initialize(MauiWindow mainWindow)
    {
        _mainWindow = mainWindow;
        
        // 创建托盘图标
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // 可以使用自定义图标
            Text = "剪贴板管理器",
            Visible = true
        };

        // 创建上下文菜单
        var contextMenu = new ContextMenuStrip();
        
        var showItem = new ToolStripMenuItem("显示窗口");
        showItem.Click += (s, e) => ShowWindow();
        contextMenu.Items.Add(showItem);

        var hideItem = new ToolStripMenuItem("隐藏窗口");
        hideItem.Click += (s, e) => HideWindow();
        contextMenu.Items.Add(hideItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        
        // 双击托盘图标显示/隐藏窗口
        _notifyIcon.DoubleClick += (s, e) =>
        {
            if (_mainWindow != null)
            {
                var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (platformWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                    // 检查窗口是否可见
                    var isVisible = Vanara.PInvoke.User32.IsWindowVisible(hwnd);
                    if (isVisible)
                        HideWindow();
                    else
                        ShowWindow();
                }
            }
        };

        // 隐藏任务栏图标
        HideTaskbarIcon();
    }

    private void ShowWindow()
    {
        if (_mainWindow == null)
            return;

        var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (platformWindow == null)
            return;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
        // 确保任务栏图标隐藏
        HideTaskbarIcon(hwnd);
        // 使用Windows API显示窗口
        Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_SHOW);
        BringWindowToForeground(hwnd, platformWindow);
    }
    
    /// <summary>
    /// 将窗口带到前台（使用多种技术确保可靠性，即使在全屏应用运行时也能工作）
    /// </summary>
    private void BringWindowToForeground(IntPtr hwnd, Microsoft.UI.Xaml.Window platformWindow)
    {
        if (hwnd == IntPtr.Zero)
            return;

        try
        {
            // 方法1: 先恢复窗口（如果被最小化）
            Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_RESTORE);
            
            // 方法2: 使用 SetWindowPos 将窗口设置为最顶层（临时）
            // 这样可以绕过 Windows 的前台窗口保护机制
            Vanara.PInvoke.User32.SetWindowPos(
                hwnd,
                Vanara.PInvoke.User32.SpecialWindowHandles.HWND_TOPMOST,
                0, 0, 0, 0,
                Vanara.PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE | 
                Vanara.PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE |
                Vanara.PInvoke.User32.SetWindowPosFlags.SWP_SHOWWINDOW);
            
            // 方法3: 立即将窗口恢复为普通窗口（移除最顶层状态）
            // 使用 HWND_NOTOPMOST 明确移除最顶层标志，确保窗口可以被其他应用覆盖
            Vanara.PInvoke.User32.SetWindowPos(
                hwnd,
                Vanara.PInvoke.User32.SpecialWindowHandles.HWND_NOTOPMOST,
                0, 0, 0, 0,
                Vanara.PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE | 
                Vanara.PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE);
            
            // 方法4: 使用 AttachThreadInput 附加到前台线程
            // 这是最可靠的方法，可以绕过 Windows 的前台窗口保护
            var foregroundHwnd = Vanara.PInvoke.User32.GetForegroundWindow();
            if (foregroundHwnd != IntPtr.Zero && foregroundHwnd != hwnd)
            {
                var foregroundThreadId = Vanara.PInvoke.User32.GetWindowThreadProcessId(foregroundHwnd, out _);
                var currentThreadId = Vanara.PInvoke.Kernel32.GetCurrentThreadId();
                
                if (foregroundThreadId != currentThreadId)
                {
                    // 附加到前台线程
                    Vanara.PInvoke.User32.AttachThreadInput(currentThreadId, foregroundThreadId, true);
                    
                    // 现在可以安全地设置前台窗口
                    Vanara.PInvoke.User32.BringWindowToTop(hwnd);
                    Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
                    
                    // 分离线程
                    Vanara.PInvoke.User32.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
                else
                {
                    // 如果已经在同一线程，直接设置
                    Vanara.PInvoke.User32.BringWindowToTop(hwnd);
                    Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
                }
            }
            else
            {
                // 如果没有前台窗口，直接设置
                Vanara.PInvoke.User32.BringWindowToTop(hwnd);
                Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
            }
            
            // 方法5: 激活 WinUI 窗口
            platformWindow.Activate();
            
            // 方法6: 再次确保窗口在最前面（某些情况下需要）
            Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_SHOW);
        }
        catch (Exception ex)
        {
            DebugHelper.DebugWrite($"Error bringing window to foreground: {ex.Message}");
            // 如果高级方法失败，尝试基本方法
            try
            {
                Vanara.PInvoke.User32.BringWindowToTop(hwnd);
                Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
                platformWindow.Activate();
            }
            catch
            {
                // 忽略错误，避免影响主程序运行
            }
        }
    }

    public void HideWindow()
    {
        if (_mainWindow != null)
        {
            var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (platformWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                // 使用Windows API隐藏窗口
                Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_HIDE);
            }
        }
    }
    
    /// <summary>
    /// 切换窗口显示/隐藏状态（供 HotkeyService 调用）
    /// </summary>
    public void ToggleWindow()
    {
        DebugHelper.DebugWrite("Toggling window visibility via TrayIconService");

        if (_mainWindow == null)
        {
            DebugHelper.DebugWrite("Main window is null, cannot toggle visibility");
            return;
        }

        // _mainWindow.Handler = NULL
        var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

        if (platformWindow == null)
        {
            DebugHelper.DebugWrite("Platform window is null, cannot toggle visibility");
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
        var isVisible = Vanara.PInvoke.User32.IsWindowVisible(hwnd);
                
        if (isVisible)
        {
            DebugHelper.DebugWrite("Window is currently visible, checking focus to decide hide/show");
            // 窗口可见，检查是否有焦点
            var foregroundHwnd = Vanara.PInvoke.User32.GetForegroundWindow();
            var hasFocus = (foregroundHwnd == hwnd);

            if (hasFocus)
            {
                DebugHelper.DebugWrite("Window has focus, hiding window");
                // 窗口可见且有焦点，隐藏窗口
                Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_HIDE);
            }
            else
            {
                DebugHelper.DebugWrite("Window is visible but does not have focus, bringing to front");
                BringWindowToForeground(hwnd, platformWindow);
            }
        }
        else
        {
            DebugHelper.DebugWrite("Window is currently hidden, showing and focusing");
            // 窗口不可见，显示并聚焦
            // 先恢复窗口（如果被最小化）
            Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_RESTORE);
            // 然后显示窗口
            Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_SHOW);
            // 确保窗口在最前面
            Vanara.PInvoke.User32.BringWindowToTop(hwnd);
            // 设置窗口为前台窗口
            Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
            // 激活 WinUI 窗口
            platformWindow.Activate();
        }
    }

    /// <summary>
    /// 设置退出处理器，用于标记应用正在退出
    /// </summary>
    public void SetExitHandler(Action exitHandler)
    {
        _exitHandler = exitHandler;
    }
    
    private void ExitApplication()
    {
        // 调用退出处理器，标记应用正在退出
        _exitHandler?.Invoke();
        
        // 清理托盘图标
        _notifyIcon?.Dispose();
        
        // 在主线程上退出 MAUI 应用
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // 关闭所有窗口（现在会真正关闭，因为 _isExiting 已设置）
            if (_mainWindow != null)
            {
                var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (platformWindow != null)
                {
                    platformWindow.Close();
                }
            }
            
            // 退出应用
            Microsoft.Maui.Controls.Application.Current?.Quit();
        });
    }

    private void HideTaskbarIcon()
    {
        // 使用Windows API隐藏任务栏图标
        // 这需要在窗口创建后调用
        if (_mainWindow != null)
        {
            _mainWindow.HandlerChanged += (s, e) =>
            {
                var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (platformWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                    HideTaskbarIcon(hwnd);
                }
            };
        }
    }

    public void HideTaskbarIcon(IntPtr hwnd)
    {
        // 使用Windows API隐藏任务栏图标
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        
        try
        {
            var exStyle = (int)Vanara.PInvoke.User32.GetWindowLong(hwnd, (Vanara.PInvoke.User32.WindowLongFlags)GWL_EXSTYLE);
            var newStyle = exStyle | WS_EX_TOOLWINDOW;
            Vanara.PInvoke.User32.SetWindowLong(hwnd, (Vanara.PInvoke.User32.WindowLongFlags)GWL_EXSTYLE, 
                new nint(newStyle));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error hiding taskbar icon: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _notifyIcon?.Dispose();
            _isDisposed = true;
        }
    }
}
#endif


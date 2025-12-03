#if WINDOWS
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Maui.Platform;
using MauiWindow = Microsoft.Maui.Controls.Window;

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
        if (_mainWindow != null)
        {
            var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (platformWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                // 确保任务栏图标隐藏
                HideTaskbarIcon(hwnd);
                // 使用Windows API显示窗口
                Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_SHOW);
                Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
                platformWindow.Activate();
            }
        }
    }

    private void HideWindow()
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
        if (_mainWindow != null)
        {
            // _mainWindow.Handler = NULL
            var platformWindow = _mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (platformWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                var isVisible = Vanara.PInvoke.User32.IsWindowVisible(hwnd);
                
                if (isVisible)
                {
                    // 窗口可见，检查是否有焦点
                    var foregroundHwnd = Vanara.PInvoke.User32.GetForegroundWindow();
                    var hasFocus = (foregroundHwnd == hwnd);
                    
                    if (hasFocus)
                    {
                        // 窗口可见且有焦点，隐藏窗口
                        Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_HIDE);
                    }
                    else
                    {
                        // 窗口可见但失去焦点，重新聚焦到窗口
                        Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_SHOW);
                        Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
                        platformWindow.Activate();
                    }
                }
                else
                {
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
                    
                    // 再次确保窗口在最前面（某些情况下需要多次调用）
                    System.Threading.Thread.Sleep(10);
                    Vanara.PInvoke.User32.BringWindowToTop(hwnd);
                    Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
                }
            }
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


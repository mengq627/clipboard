#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using MauiWindow = Microsoft.Maui.Controls.Window;

namespace clipboard.Platforms.Windows.Services;

/// <summary>
/// 全局快捷键服务
/// 使用低级键盘 Hook (Low-Level Keyboard Hook) 拦截 Win+V，即使系统已注册也能优先处理
/// </summary>
public class HotkeyService : IDisposable
{
    private const int MOD_WIN = 0x0008;
    private const int VK_V = 0x56;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _isWinPressed = false;
    private bool _isVPressed = false;
    private MauiWindow? _mainWindow;
    private TrayIconService? _trayIconService;
    
    // 键盘 Hook 委托（必须保持引用以防止被 GC 回收）
    private LowLevelKeyboardProcDelegate? _keyboardProcDelegate;
    
    // 键盘 Hook 委托类型
    private delegate IntPtr LowLevelKeyboardProcDelegate(int nCode, IntPtr wParam, IntPtr lParam);
    
    // Windows API 声明
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProcDelegate lpfn, IntPtr hMod, uint dwThreadId);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
    
    public void Initialize(MauiWindow mainWindow, TrayIconService trayIconService)
    {
        _mainWindow = mainWindow;
        _trayIconService = trayIconService;
        
        // 安装低级键盘 Hook
        InstallKeyboardHook();
    }
    
    /// <summary>
    /// 安装低级键盘 Hook 以拦截 Win+V
    /// </summary>
    private void InstallKeyboardHook()
    {
        try
        {
            // 创建键盘 Hook 委托（必须保持引用）
            _keyboardProcDelegate = LowLevelKeyboardProc;
            
            // 获取当前模块句柄
            var hMod = GetModuleHandle(null);
            
            // 安装低级键盘 Hook
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProcDelegate, hMod, 0);
            
            if (_hookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Failed to install keyboard hook. Error code: {error}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Keyboard hook installed successfully");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error installing keyboard hook: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 低级键盘 Hook 过程，拦截 Win+V 组合键
    /// </summary>
    private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 如果 nCode < 0，必须调用 CallNextHookEx 并返回其结果
        if (nCode < 0)
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
        
        // 检查是否是 Win 键或 V 键
        var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        var vkCode = hookStruct.vkCode;
        
        // 检查是否是 Win 键（左 Win 或右 Win）
        const uint VK_LWIN = 0x5B;
        const uint VK_RWIN = 0x5C;
        bool isWinKey = (vkCode == VK_LWIN || vkCode == VK_RWIN);
        bool isVKey = (vkCode == VK_V);
        
        var message = wParam.ToInt32();
        bool isKeyDown = (message == WM_KEYDOWN || message == WM_SYSKEYDOWN);
        bool isKeyUp = (message == WM_KEYUP || message == WM_SYSKEYUP);
        
        // 处理 Win 键按下/释放
        if (isWinKey)
        {
            if (isKeyDown)
            {
                _isWinPressed = true;
            }
            else if (isKeyUp)
            {
                _isWinPressed = false;
                // Win 键释放时，也重置 V 键状态（防止状态不同步）
                if (_isVPressed)
                {
                    _isVPressed = false;
                }
            }
        }
        
        // 处理 V 键按下/释放
        if (isVKey)
        {
            if (isKeyDown)
            {
                // 如果 Win 键和 V 键同时按下，拦截并处理
                if (_isWinPressed)
                {
                    _isVPressed = true;
                    
                    // 在主线程上执行切换窗口操作
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ToggleWindow();
                    });
                    
                    // 返回 1 表示已处理，阻止消息继续传递（这样系统剪贴板历史就不会弹出）
                    return (IntPtr)1;
                }
            }
            else if (isKeyUp)
            {
                _isVPressed = false;
            }
        }
        
        // 如果 Win 键释放，重置所有状态
        if (isWinKey && isKeyUp)
        {
            _isWinPressed = false;
            _isVPressed = false;
        }
        
        // 其他情况，继续传递消息
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
    
    /// <summary>
    /// 卸载键盘 Hook
    /// </summary>
    private void UninstallKeyboardHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _keyboardProcDelegate = null; // 释放委托引用
            System.Diagnostics.Debug.WriteLine("Keyboard hook uninstalled");
        }
    }
    
    public void ToggleWindow()
    {
        if (_mainWindow != null)
        {
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
                    Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_SHOW);
                    Vanara.PInvoke.User32.SetForegroundWindow(hwnd);
                    platformWindow.Activate();
                }
            }
        }
    }
    
    public void Dispose()
    {
        UninstallKeyboardHook();
    }
}
#endif


#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using MauiWindow = Microsoft.Maui.Controls.Window;
using clipboard.Utils;

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
    private bool _isModifierPressed = false; // 修饰键（Win或Alt）是否按下
    private bool _isKeyPressed = false; // 字母键是否按下
    private TrayIconService? _trayIconService;
    
    // 快捷键配置
    private bool _useWinKey = true;
    private bool _useAltKey = false;
    private char _hotkeyKey = 'V';
    
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
        _trayIconService = trayIconService;
        
        // 安装低级键盘 Hook
        InstallKeyboardHook();
    }
    
    /// <summary>
    /// 更新快捷键配置
    /// </summary>
    public void UpdateHotkeyConfig(bool useWinKey, bool useAltKey, char key)
    {
        _useWinKey = useWinKey;
        _useAltKey = useAltKey;
        _hotkeyKey = char.ToUpperInvariant(key);
        System.Diagnostics.Debug.WriteLine($"Hotkey updated: {(useWinKey ? "Win" : "Alt")} + {_hotkeyKey}");
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
    /// We need to record the state of modifier keys and target key separately.
    /// </summary>
    private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Call other hooks
        if (nCode < 0)
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
        
        // 检查是否是 Win 键或 V 键
        var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        var vkCode = hookStruct.vkCode;
        
        // 检查是否是修饰键（Win 或 Alt）
        // TODO: Only support `Win` + `v` now
        const uint VK_LWIN = 0x5B;
        const uint VK_RWIN = 0x5C;
        const uint VK_LMENU = 0xA4; // 左 Alt
        const uint VK_RMENU = 0xA5; // 右 Alt
        bool isWinKey = (vkCode == VK_LWIN || vkCode == VK_RWIN);
        bool isAltKey = (vkCode == VK_LMENU || vkCode == VK_RMENU);
        bool isModifierKey = (_useWinKey && isWinKey) || (_useAltKey && isAltKey);

        // 检查是否是配置的字母键
        uint targetKeyCode = (uint)_hotkeyKey;
        bool isTargetKey = (vkCode == targetKeyCode);
        
        var message = wParam.ToInt32();
        bool isKeyDown = (message == WM_KEYDOWN || message == WM_SYSKEYDOWN);
        bool isKeyUp = (message == WM_KEYUP || message == WM_SYSKEYUP);

        DebugHelper.DebugWrite($"Key event: vkCode={vkCode}, isModifierKey={isModifierKey}, isTargetKey={isTargetKey}, " +
            $"isKeyDown={isKeyDown}, isKeyUp={isKeyUp}");

        if (isModifierKey)
        {
            if (isKeyDown)
            {
                _isModifierPressed = true;
            }
            else if (isKeyUp)
            {
                _isModifierPressed = false;
                _isKeyPressed = false;
            }

            goto NextHook;
        }
        
        // 处理字母键按下/释放
        if (isTargetKey)
        {
            if (isKeyDown)
            {
                _isKeyPressed = true;
                // 如果修饰键和字母键同时按下，拦截并处理
                if (_isModifierPressed)
                {
                    DebugHelper.DebugWrite($"Hotkey has been pressed");

                    // 在主线程上执行切换窗口操作，委托给 TrayIconService
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _trayIconService?.ToggleWindow();
                    });
                    
                    // 返回 1 表示已处理，阻止消息继续传递（这样系统剪贴板历史就不会弹出）
                    return (IntPtr)1;
                }
            }
            else if (isKeyUp)
            {
                _isKeyPressed = false;
            }
        }
        
NextHook:
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
    
    public void Dispose()
    {
        UninstallKeyboardHook();
    }
}
#endif


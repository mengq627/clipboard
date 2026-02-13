#if WINDOWS
using clipboard.Utils;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRT.Interop;
using MauiWindow = Microsoft.Maui.Controls.Window;

namespace clipboard.Platforms.Windows.Services;

/// <summary>
/// 全局快捷键服务
/// 使用低级键盘 Hook (Low-Level Keyboard Hook) 拦截 Win+V，即使系统已注册也能优先处理
/// </summary>
public class HotkeyService : IDisposable
{
    private const int MOD_WIN = 0x0008;
    private const int MOD_ALT = 0x0001;
    private const int VK_V = 0x56;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _isKeyPressed = false; // 字母键是否按下（保留用于兼容性，但新实现中不再需要）
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
    
    private IntPtr _windowHandle = IntPtr.Zero;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0x1234; // 热键 ID

    // TODO: 挪到NativeMethods类里
    // RegisterHotKey 相关 API
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    // 检测按键状态的 API
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern short GetAsyncKeyState(int vKey);
    
    // 窗口消息处理委托
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _originalWndProc = IntPtr.Zero;
    
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("Comctl32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("Comctl32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    public delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

    private const int GWLP_WNDPROC = -4;

    // 必须保持对委托的引用，否则会被 GC 回收导致程序崩溃
    private SUBCLASSPROC _subclassDelegate;

    public void Initialize(MauiWindow mainWindow, TrayIconService trayIconService)
    {
        // 入口函数
        _trayIconService = trayIconService;

        // 先尝试注册热键，失败后安装低级键盘 Hook
        // 获取原生 WinUI 3 窗口对象
        var nativeWindow = mainWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

        if (nativeWindow == null)
        {
            return;
        }
        int res = RegisterWinV(nativeWindow);
        if (res < 0)
        {
            RestartExplorer();

            res = RegisterWinV(nativeWindow);
            if (res < 0)
            {
                InstallKeyboardHook();
            }
            
        }
    }
    
    /// <summary>
    /// 更新快捷键配置
    /// </summary>
    public void UpdateHotkeyConfig(bool useWinKey, bool useAltKey, char key)
    {
        _useWinKey = useWinKey;
        _useAltKey = useAltKey;
        _hotkeyKey = char.ToUpperInvariant(key);
        DebugHelper.DebugWrite($"Hotkey updated: {(useWinKey ? "Win" : "Alt")} + {_hotkeyKey}");
        // 不需要重新安装 Hook，因为 Hook 会实时检测按键状态
    }
    
    /// <summary>
    /// 使用 RegisterHotKey 方式安装热键
    /// 注意：RegisterHotKey 不支持 Win 键（被系统保留），如果使用 Win 键，应该使用 LowLevelKeyboardHook
    /// </summary>
    private void InstallRegisterHotKey()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            DebugHelper.DebugWrite("Window handle is zero, cannot register hotkey");
            return;
        }
        
        if (!_useAltKey && !_useWinKey)
        {
            DebugHelper.DebugWrite("No modifier key selected, cannot register hotkey");
            return;
        }
        
        try
        {
            // 只支持 Alt 键，因为 Win 键被系统保留
            uint vkCode = (uint)_hotkeyKey;
            uint modifiers = MOD_ALT;
            
            bool success = RegisterHotKey(_windowHandle, HOTKEY_ID, modifiers, vkCode);
            
            if (!success)
            {
                var error = Marshal.GetLastWin32Error();
                DebugHelper.DebugWrite($"Failed to register hotkey Alt+{_hotkeyKey}. Error code: {error}");
                // 如果注册失败，回退到 LowLevelKeyboardHook
                DebugHelper.DebugWrite("Falling back to LowLevelKeyboardHook");
                InstallKeyboardHook();
            }
            else
            {
                DebugHelper.DebugWrite($"Hotkey registered successfully: Alt + {_hotkeyKey}");
                
                // 子类化窗口以接收 WM_HOTKEY 消息
                SubclassWindow();
            }
        }
        catch (Exception ex)
        {
            DebugHelper.DebugWrite($"Error registering hotkey: {ex.Message}");
            // 如果出错，回退到 LowLevelKeyboardHook
            InstallKeyboardHook();
        }
    }
    
    /// <summary>
    /// 子类化窗口以接收 WM_HOTKEY 消息
    /// </summary>
    private void SubclassWindow()
    {
        if (_windowHandle == IntPtr.Zero || _wndProcDelegate != null)
            return;
        
        try
        {
            _wndProcDelegate = CustomWndProc;
            _originalWndProc = GetWindowLongPtr(_windowHandle, GWLP_WNDPROC);
            SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
            DebugHelper.DebugWrite("Window subclassed successfully");
        }
        catch (Exception ex)
        {
            DebugHelper.DebugWrite($"Error subclassing window: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 自定义窗口过程，处理 WM_HOTKEY 消息
    /// 注意：由于 RegisterHotKey 不支持 Win 键，这里只处理 Alt 键的情况
    /// 如果用户选择 Win 键，会回退到 LowLevelKeyboardHook
    /// </summary>
    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            // 如果使用 RegisterHotKey，说明已经注册了 Alt+V（因为 Win 键不支持）
            // 收到 WM_HOTKEY 消息时，Alt 键肯定是按下的（因为 RegisterHotKey 要求修饰键按下）
            DebugHelper.DebugWrite($"Hotkey received via RegisterHotKey: Alt + {_hotkeyKey}");
            
            // 直接触发，因为 Alt 键已经按下（RegisterHotKey 的要求）
            DebugHelper.DebugWrite("Hotkey triggered: Alt + target key");
            
            // 在主线程上执行切换窗口操作
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _trayIconService?.ToggleWindow();
            });
            
            // 返回 0 表示已处理
            return IntPtr.Zero;
        }
        
        // 调用原始窗口过程
        if (_originalWndProc != IntPtr.Zero)
        {
            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }
        
        return IntPtr.Zero;
    }
    
    /// <summary>
    /// 卸载 RegisterHotKey
    /// </summary>
    private void UnregisterRegisterHotKey()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            DebugHelper.DebugWrite("Hotkey unregistered");
        }
        
        // 恢复原始窗口过程
        if (_windowHandle != IntPtr.Zero && _originalWndProc != IntPtr.Zero && _wndProcDelegate != null)
        {
            try
            {
                SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, _originalWndProc);
                _wndProcDelegate = null;
                _originalWndProc = IntPtr.Zero;
                DebugHelper.DebugWrite("Window unsubclassed");
            }
            catch (Exception ex)
            {
                DebugHelper.DebugWrite($"Error unsubclassing window: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 安装低级键盘 Hook 以拦截 Win+V
    /// 注意：此方法已保留但不调用，使用 RegisterHotKey 方式替代
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
    /// 使用实时检测 Win 键状态的方式，而不是跟踪修饰键状态
    /// </summary>
    private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Call other hooks
        if (nCode < 0)
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
        
        // 检查是否是目标键
        var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        var vkCode = hookStruct.vkCode;
        
        // 检查是否是配置的字母键
        uint targetKeyCode = (uint)_hotkeyKey;
        bool isTargetKey = (vkCode == targetKeyCode);
        
        var message = wParam.ToInt32();
        bool isKeyDown = (message == WM_KEYDOWN || message == WM_SYSKEYDOWN);
        bool isKeyUp = (message == WM_KEYUP || message == WM_SYSKEYUP);

        // 处理目标键按下/释放
        if (isTargetKey)
        {
            if (isKeyDown)
            {
                DebugHelper.DebugWrite($"Target key {_hotkeyKey} down detected in hook");
                // 实时查询 Win 键的系统状态
                // 检查 Win 键是否被按下 (GetAsyncKeyState 返回负值表示键被按下)
                const int VK_LWIN_INT = 0x5B; // 左 Win 键
                //const int VK_RWIN_INT = 0x5C; // 右 Win 键
                
                bool isWinPressed = false;
                
                if (_useWinKey)
                {
                    // 检查左 Win 键或右 Win 键是否按下
                    short leftWinState = GetAsyncKeyState(VK_LWIN_INT);
                    //short rightWinState = GetAsyncKeyState(VK_RWIN_INT);
                    //isWinPressed = (leftWinState & 0x8000) != 0 || (rightWinState & 0x8000) != 0;
                    isWinPressed = (leftWinState & 0x8000) != 0;
                }
                else if (_useAltKey)
                {
                    // 检查 Alt 键是否按下
                    const int VK_LMENU_INT = 0xA4; // 左 Alt
                    const int VK_RMENU_INT = 0xA5; // 右 Alt
                    short leftAltState = GetAsyncKeyState(VK_LMENU_INT);
                    short rightAltState = GetAsyncKeyState(VK_RMENU_INT);
                    isWinPressed = (leftAltState & 0x8000) != 0 || (rightAltState & 0x8000) != 0;
                }
                
                _isKeyPressed = true;
                
                // 仅当修饰键实时处于按下状态时才触发
                if (isWinPressed)
                {
                    DebugHelper.DebugWrite($"Hotkey ({(_useWinKey ? "Win" : "Alt")} + {_hotkeyKey}) has been pressed");

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
        
        // 即使不是目标键，也要确保所有按键消息继续传递
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


    // 检查系统剪贴板历史是否开启
    public static bool IsSystemClipboardHistoryEnabled()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Clipboard"))
            {
                if (key != null)
                {
                    var val = key.GetValue("EnableClipboardHistory");
                    // 1 = 开启, 0 = 关闭
                    if (val is int i && i == 1)
                    {
                        DebugHelper.DebugWrite("系统剪贴板历史功能已开启");
                        return true;
                    }
                }
            }
        }
        catch { /* 处理权限异常 */ }
        DebugHelper.DebugWrite("系统剪贴板历史功能未开启");
        return false;
    }

    // 禁用系统剪贴板历史（为了让我们能接管 Win+V）
    public static void DisableSystemClipboardHistory()
    {
        try
        {
            // --- 1. 禁用剪贴板历史记录功能 (防止系统收集剪贴板数据) ---
            string clipboardPath = @"Software\Microsoft\Clipboard";
            using (var clipboardKey = Registry.CurrentUser.OpenSubKey(clipboardPath, true))
            {
                // 如果路径不存在，CreateSubKey 会创建它
                var targetKey = clipboardKey ?? Registry.CurrentUser.CreateSubKey(clipboardPath);
                targetKey.SetValue("EnableClipboardHistory", 0, RegistryValueKind.DWord);
                DebugHelper.DebugWrite("已禁用系统剪贴板历史记录数据功能。");
            }

            // --- 2. 释放 Win+V 热键占用 (DisabledHotkeys) ---
            string explorerAdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            using (var advKey = Registry.CurrentUser.OpenSubKey(explorerAdvancedPath, true))
            {
                if (advKey != null)
                {
                    // 读取现有的禁用列表
                    object existingValue = advKey.GetValue("DisabledHotkeys");
                    string currentDisabledKeys = existingValue?.ToString() ?? "";

                    // 如果 'V' 还没在禁用列表中，则添加它
                    if (!currentDisabledKeys.Contains("V", StringComparison.OrdinalIgnoreCase))
                    {
                        string newValue = currentDisabledKeys + "V";
                        advKey.SetValue("DisabledHotkeys", newValue, RegistryValueKind.String);
                        DebugHelper.DebugWrite($"已将 'V' 添加到 DisabledHotkeys。当前值: {newValue}");
                    }
                    else
                    {
                        DebugHelper.DebugWrite("'V' 已经存在于 DisabledHotkeys 列表中，无需重复添加。");
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            DebugHelper.DebugWrite("无法修改注册表：权限不足。请尝试以管理员身份运行程序。");
        }
        catch (Exception ex)
        {
            DebugHelper.DebugWrite($"修改注册表时发生异常: {ex.Message}");
        }
    }

    public static void RestartExplorer()
    {
        try
        {
            // 1. 终止所有名为 explorer 的进程
            // 注意：Kill() 之后 Windows 默认会自动尝试重启一个，
            // 但为了保险和即时性，我们会手动再启动一个。
            var explorers = Process.GetProcessesByName("explorer");
            foreach (var p in explorers)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(3000); // 等待最多3秒让它退出
                }
                catch { /* 忽略单个进程操作异常 */ }
            }

            // 2. 重新启动资源管理器
            // 显式指定路径以确保安全
            string explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName = explorerPath,
                UseShellExecute = true
            });

            DebugHelper.DebugWrite("资源管理器已尝试重启。");
        }
        catch (Exception ex)
        {
            DebugHelper.DebugWrite($"重启资源管理器时发生错误: {ex.Message}");
        }
    }

    private IntPtr _hwnd;
    //private const int HOTKEY_ID = 9000; // 唯一的 ID

    private int RegisterWinV(Microsoft.UI.Xaml.Window window)
    {
        // 1. 获取窗口句柄 (MAUI 9 / WinUI 3 获取句柄的方式)
        _hwnd = WindowNative.GetWindowHandle(window);

        // 2. 先尝试禁用系统的剪贴板历史（核心步骤！）
        // 如果你不做这一步，下面的 RegisterHotKey 很大概率会返回 false
        if (IsSystemClipboardHistoryEnabled())
        {
            // 实际上这里应该弹窗询问用户是否允许接管
            DisableSystemClipboardHistory();
        }

        // 3. 注册快捷键 Win + V
        // MOD_WIN = 0x0008, VK_V = 0x56
        bool success = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_WIN, VK_V);

        if (!success)
        {
            DebugHelper.DebugWrite("注册 Win+V 失败！可能被系统或其他软件占用了。");
            return -1;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Win+V 注册成功！");

            // 4. 挂钩消息循环处理
            // 在 WinUI 3 中，直接挂钩 WndProc 比较麻烦，
            // 通常建议使用名为 PInvoke.User32 的库或者创建一个不可见的 NativeWindow 来接收消息
            // 这里为了演示逻辑，假设你有一个处理消息的方法
            InitializeMessageHandling();
            return 0;
        }
    }

    private void InitializeMessageHandling()
    {
        // 创建委托并挂钩
        _subclassDelegate = new SUBCLASSPROC(OnWndProc);

        // 100 是子类 ID，可以随便写一个
        SetWindowSubclass(_hwnd, _subclassDelegate, 100, IntPtr.Zero);
    }

    private IntPtr OnWndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_ID)
            {
                DebugHelper.DebugWrite("Hotkey Win+V 被触发 via RegisterHotKey");

                // 模仿你 LowLevel 钩子里的逻辑
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _trayIconService?.ToggleWindow();
                });

                return IntPtr.Zero; // 消息已处理
            }
        }

        // 传递给原始窗口过程
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    // 注意：WinUI 3 并没有直接暴露 AddHook 像 WPF 那样。
    // 你可能需要使用 subclassing 或者创建一个隐藏的 Win32 窗口来接收 WM_HOTKEY 消息

    public void Dispose()
    {
        // 卸载 LowLevelKeyboardHook
        UninstallKeyboardHook();
        
        // 卸载 RegisterHotKey 方式（保留但不调用）
        UnregisterRegisterHotKey();
    }
}
#endif


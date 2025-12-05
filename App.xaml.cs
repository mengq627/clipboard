#if WINDOWS
using clipboard.Platforms.Windows.Services;
using System.Runtime.InteropServices;
#endif

namespace clipboard
{
    public partial class App : Application
    {
#if WINDOWS
        private TrayIconService? _trayIconService;
        private WindowPositionService? _windowPositionService;
        private HotkeyService? _hotkeyService;
        private Services.AppSettingsService? _settingsService;
        
        /// <summary>
        /// 更新快捷键配置（供SettingsViewModel调用）
        /// </summary>
        public void UpdateHotkeyConfig(bool useWinKey, bool useAltKey, char key)
        {
            _hotkeyService?.UpdateHotkeyConfig(useWinKey, useAltKey, key);
        }
        
        /// <summary>
        /// 隐藏主窗口（供 MainPage 调用）
        /// </summary>
        public void HideMainWindow()
        {
#if WINDOWS
            _trayIconService?.HideWindow();
#endif
        }
        
        // 窗口子类化相关
        private IntPtr _originalWndProc = IntPtr.Zero;
        private WndProcDelegate? _wndProcDelegate;
        private Microsoft.UI.Xaml.Window? _platformWindowForMinimize;
        
        // Windows API 声明
        private const int GWLP_WNDPROC = -4;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
#endif

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            
#if WINDOWS
            // 设置窗口大小：宽度为屏幕的1/4，高度为屏幕的1/2
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            var screenWidth = displayInfo.Width / displayInfo.Density;
            var screenHeight = displayInfo.Height / displayInfo.Density;
            
            window.Width = screenWidth / 4;
            window.Height = screenHeight / 2;
            
            // 设置最小窗口大小，避免窗口太小无法使用
            window.MinimumWidth = 300;
            window.MinimumHeight = 400;

            // 初始化窗口位置服务
            _windowPositionService = new WindowPositionService();
            
            // 等待窗口创建后恢复位置和初始化托盘
            window.HandlerChanged += (s, e) =>
            {
                var platformWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (platformWindow != null)
                {
                    // 恢复窗口位置
                    _windowPositionService?.RestoreWindowPosition(platformWindow);
                    
                    // 初始化托盘图标（必须在获取hwnd之前）
                    _trayIconService = new TrayIconService();
                    _trayIconService.Initialize(window);
                    
                    // 确保任务栏图标始终隐藏
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                    _trayIconService.HideTaskbarIcon(hwnd);
                    
                    // 初始化快捷键服务
                    _hotkeyService = new HotkeyService();
                    _hotkeyService.Initialize(window, _trayIconService);
                    
                    // 加载并应用快捷键配置
                    _settingsService = Handler?.MauiContext?.Services.GetService<Services.AppSettingsService>();
                    if (_settingsService != null && _hotkeyService != null)
                    {
                        var settings = _settingsService.GetSettings();
                        _hotkeyService.UpdateHotkeyConfig(
                            settings.Hotkey.UseWinKey,
                            settings.Hotkey.UseAltKey,
                            settings.Hotkey.Key
                        );
                    }
                    
                    // 监听窗口位置变化
                    platformWindow.Activated += (sender, args) =>
                    {
                        if (args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated)
                        {
                            _windowPositionService?.RestoreWindowPosition(platformWindow);
                            // 每次激活时都确保任务栏图标隐藏
                            var hwnd2 = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                            _trayIconService.HideTaskbarIcon(hwnd2);
                        }
                    };
                    
                    // 监听窗口移动和大小变化
                    var timer = new System.Timers.Timer(500); // 每500ms保存一次位置
                    timer.Elapsed += (s, e) =>
                    {
                        var hwnd3 = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                        if (Vanara.PInvoke.User32.IsWindowVisible(hwnd3))
                        {
                            _windowPositionService?.SaveWindowPosition(platformWindow);
                            // 定期确保任务栏图标隐藏
                            _trayIconService.HideTaskbarIcon(hwnd3);
                        }
                    };
                    timer.Start();
                    
                    // 窗口关闭时停止定时器
                    platformWindow.Closed += (s, e) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                    };
                    
                    // 处理窗口关闭按钮点击 - 隐藏窗口而不是关闭
                    // WinUI 3 使用 Closed 事件而不是 Closing
                    // 注意：需要检查是否是真正的退出请求（通过标志位）
                    bool isExiting = false;
                    
                    // 提供一个方法来设置退出标志
                    _trayIconService?.SetExitHandler(() => { isExiting = true; });
                    
                    platformWindow.Closed += (sender, args) =>
                    {
                        // 如果是退出请求，允许窗口关闭
                        if (isExiting)
                        {
                            return; // 不阻止关闭
                        }
                        
                        // 保存位置
                        _windowPositionService?.SaveWindowPosition(platformWindow);
                        // 使用Windows API隐藏窗口而不是真正关闭
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                        Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_HIDE);
                        // 阻止默认关闭行为
                        args.Handled = true;
                    };
                    
                    // 拦截窗口最小化事件 - 隐藏窗口而不是最小化
                    InterceptMinimizeButton(platformWindow);
                }
            };
#endif
            
            return window;
        }
        
#if WINDOWS
        /// <summary>
        /// 拦截窗口最小化按钮，使其隐藏窗口而不是最小化
        /// </summary>
        private void InterceptMinimizeButton(Microsoft.UI.Xaml.Window platformWindow)
        {
            try
            {
                // 保存 platformWindow 引用，以便在窗口过程中使用
                _platformWindowForMinimize = platformWindow;
                
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                
                // 创建窗口过程委托（必须保持引用）
                _wndProcDelegate = CustomWndProc;
                
                // 保存原始窗口过程
                _originalWndProc = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
                
                // 设置新的窗口过程
                var newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
                SetWindowLongPtr(hwnd, GWLP_WNDPROC, newWndProc);
                
                System.Diagnostics.Debug.WriteLine("Minimize button interception installed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error intercepting minimize button: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 自定义窗口过程，拦截 WM_SYSCOMMAND 消息中的 SC_MINIMIZE
        /// </summary>
        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // 拦截最小化命令
            if (msg == WM_SYSCOMMAND && wParam.ToInt32() == SC_MINIMIZE)
            {
                // 保存窗口位置
                if (_platformWindowForMinimize != null && _windowPositionService != null)
                {
                    _windowPositionService.SaveWindowPosition(_platformWindowForMinimize);
                }
                
                // 隐藏窗口而不是最小化
                Vanara.PInvoke.User32.ShowWindow(hWnd, Vanara.PInvoke.ShowWindowCommand.SW_HIDE);
                
                // 返回 0 表示消息已处理
                return IntPtr.Zero;
            }
            
            // 其他消息传递给原始窗口过程
            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }
#endif
    }
}
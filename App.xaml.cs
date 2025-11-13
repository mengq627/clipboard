#if WINDOWS
using clipboard.Platforms.Windows.Services;
#endif

namespace clipboard
{
    public partial class App : Application
    {
#if WINDOWS
        private TrayIconService? _trayIconService;
        private WindowPositionService? _windowPositionService;
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
                    
                    // 监听窗口位置变化
                    platformWindow.Activated += (sender, args) =>
                    {
                        if (args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated)
                        {
                            _windowPositionService?.RestoreWindowPosition(platformWindow);
                        }
                    };
                    
                    // 监听窗口移动和大小变化
                    var timer = new System.Timers.Timer(500); // 每500ms保存一次位置
                    timer.Elapsed += (s, e) =>
                    {
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                        if (Vanara.PInvoke.User32.IsWindowVisible(hwnd))
                        {
                            _windowPositionService?.SaveWindowPosition(platformWindow);
                        }
                    };
                    timer.Start();
                    
                    // 窗口关闭时停止定时器
                    platformWindow.Closed += (s, e) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                    };
                    
                    // 初始化托盘图标
                    _trayIconService = new TrayIconService();
                    _trayIconService.Initialize(window);
                    
                    // 处理窗口关闭按钮点击 - 隐藏窗口而不是关闭
                    // WinUI 3 使用 Closed 事件而不是 Closing
                    platformWindow.Closed += (sender, args) =>
                    {
                        // 保存位置
                        _windowPositionService?.SaveWindowPosition(platformWindow);
                        // 使用Windows API隐藏窗口而不是真正关闭
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                        Vanara.PInvoke.User32.ShowWindow(hwnd, Vanara.PInvoke.ShowWindowCommand.SW_HIDE);
                        // 阻止默认关闭行为
                        args.Handled = true;
                    };
                }
            };
#endif
            
            return window;
        }
    }
}
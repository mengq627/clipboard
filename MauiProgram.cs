using Microsoft.Extensions.Logging;
using clipboard.Services;
#if WINDOWS
using clipboard.Platforms.Windows.Services;
using System.Threading;
#endif

namespace clipboard
{
    public static class MauiProgram
    {
        private static Mutex? _singleInstanceMutex;

        public static MauiApp CreateMauiApp()
        {
#if WINDOWS
            // 检查是否已有实例在运行
            const string mutexName = "Global\\ClipboardManager_SingleInstance";
            bool createdNew;

            // 确保在应用退出时释放 Mutex
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            };

            _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);
            
            if (!createdNew)
            {
                // 已有实例在运行，退出当前进程
                System.Diagnostics.Debug.WriteLine("Another instance is already running. Exiting...");
                Environment.Exit(0);
                return null!; // 不会执行到这里，但为了编译通过
            }
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // 注册服务
            var settingsService = new AppSettingsService();
            builder.Services.AddSingleton<AppSettingsService>(settingsService);
            
            var clipboardManager = new ClipboardManagerService();
            clipboardManager.SetSettingsService(settingsService);
            builder.Services.AddSingleton<IClipboardService>(clipboardManager);

#if WINDOWS
            var windowsService = new WindowsClipboardService();
            clipboardManager.SetPlatformService(windowsService);
#endif

            return builder.Build();
        }
    }
}

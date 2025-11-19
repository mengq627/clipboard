using Microsoft.Extensions.Logging;
using clipboard.Services;
#if WINDOWS
using clipboard.Platforms.Windows.Services;
#endif

namespace clipboard
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
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

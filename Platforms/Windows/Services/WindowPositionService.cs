#if WINDOWS
using System.Text.Json;
using WinRT.Interop;
using XamlWindow = Microsoft.UI.Xaml.Window;

namespace clipboard.Platforms.Windows.Services;

public class WindowPositionService
{
    private readonly string _positionFilePath;
    private WindowPosition? _savedPosition;

    public WindowPositionService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipboardApp"
        );
        Directory.CreateDirectory(appDataPath);
        _positionFilePath = Path.Combine(appDataPath, "window_position.json");
        LoadPosition();
    }

    public void SaveWindowPosition(XamlWindow window)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            Vanara.PInvoke.User32.GetWindowRect(hwnd, out var rect);
            
            _savedPosition = new WindowPosition
            {
                X = rect.left,
                Y = rect.top,
                Width = rect.right - rect.left,
                Height = rect.bottom - rect.top
            };

            var json = JsonSerializer.Serialize(_savedPosition, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_positionFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving window position: {ex.Message}");
        }
    }

    public void RestoreWindowPosition(XamlWindow window)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            
            if (_savedPosition != null && IsValidPosition(_savedPosition))
            {
                // 恢复保存的位置
                Vanara.PInvoke.User32.SetWindowPos(
                    hwnd,
                    Vanara.PInvoke.User32.SpecialWindowHandles.HWND_TOP,
                    _savedPosition.X,
                    _savedPosition.Y,
                    _savedPosition.Width,
                    _savedPosition.Height,
                    Vanara.PInvoke.User32.SetWindowPosFlags.SWP_SHOWWINDOW);
            }
            else
            {
                // 第一次运行，设置在屏幕中间偏右下的位置
                var displayInfo = DeviceDisplay.MainDisplayInfo;
                var screenWidth = displayInfo.Width / displayInfo.Density;
                var screenHeight = displayInfo.Height / displayInfo.Density;
                
                var windowWidth = screenWidth / 4;
                var windowHeight = screenHeight / 2;
                
                // 中间偏右下：屏幕中心向右偏移25%，向下偏移25%
                var x = (int)(screenWidth / 2 + screenWidth * 0.25 - windowWidth / 2);
                var y = (int)(screenHeight / 2 + screenHeight * 0.25 - windowHeight / 2);
                
                Vanara.PInvoke.User32.SetWindowPos(
                    hwnd,
                    Vanara.PInvoke.User32.SpecialWindowHandles.HWND_TOP,
                    x,
                    y,
                    (int)windowWidth,
                    (int)windowHeight,
                    Vanara.PInvoke.User32.SetWindowPosFlags.SWP_SHOWWINDOW);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error restoring window position: {ex.Message}");
        }
    }

    private bool IsValidPosition(WindowPosition position)
    {
        // 检查位置是否在屏幕范围内
        var displayInfo = DeviceDisplay.MainDisplayInfo;
        var screenWidth = displayInfo.Width / displayInfo.Density;
        var screenHeight = displayInfo.Height / displayInfo.Density;
        
        return position.X >= 0 && 
               position.Y >= 0 && 
               position.X + position.Width <= screenWidth && 
               position.Y + position.Height <= screenHeight;
    }

    private void LoadPosition()
    {
        try
        {
            if (File.Exists(_positionFilePath))
            {
                var json = File.ReadAllText(_positionFilePath);
                _savedPosition = JsonSerializer.Deserialize<WindowPosition>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading window position: {ex.Message}");
        }
    }

    private class WindowPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
#endif


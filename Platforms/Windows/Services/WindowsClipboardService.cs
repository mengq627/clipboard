#if WINDOWS
using System.Runtime.InteropServices;
using System.Text;
using clipboard.Models;
using clipboard.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace clipboard.Platforms.Windows.Services;

public class WindowsClipboardService : IClipboardService
{
    private PeriodicTimer? _monitoringTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private string _lastClipboardContent = string.Empty;
    private bool _isMonitoring = false;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    public event EventHandler<ClipboardItem>? ClipboardChanged;

    public async Task StartMonitoringAsync()
    {
        if (_isMonitoring)
        {
            System.Diagnostics.Debug.WriteLine("Clipboard monitoring already started");
            return;
        }

        System.Diagnostics.Debug.WriteLine("Starting clipboard monitoring...");
        _isMonitoring = true;
        _lastClipboardContent = await GetClipboardTextAsync();
        System.Diagnostics.Debug.WriteLine($"Initial clipboard content: '{_lastClipboardContent}'");
        _cancellationTokenSource = new CancellationTokenSource();

        // 使用PeriodicTimer来检查剪贴板变化（每300ms检查一次）
        _monitoringTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
        
        // 在后台任务中运行监控循环
        _ = Task.Run(async () =>
        {
            try
            {
                while (await _monitoringTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
                {
                    // 防止并发检查
                    if (!await _checkLock.WaitAsync(100, _cancellationTokenSource.Token))
                        continue;

                    try
                    {
                        // 检查文本内容
                        var currentContent = await GetClipboardTextAsync();
                        var hasText = !string.IsNullOrEmpty(currentContent);
                        
                        string contentType = "Text";
                        string content = currentContent;
                        
                        // 检查图片内容（使用Windows API）
                        var hasImage = await CheckClipboardHasImageAsync();
                        
                        // 优先处理图片
                        if (hasImage)
                        {
                            try
                            {
                                var imageBytes = await GetClipboardImageAsync();
                                if (imageBytes != null && imageBytes.Length > 0)
                                {
                                    contentType = "Image";
                                    content = Convert.ToBase64String(imageBytes);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error getting image from clipboard: {ex.Message}");
                            }
                        }
                        
                        // 检查内容是否变化
                        var contentKey = contentType == "Image" ? $"IMAGE:{content.Substring(0, Math.Min(50, content.Length))}" : content;
                        if (contentKey != _lastClipboardContent)
                        {
                            // 只有当新内容不为空时才触发事件
                            if ((hasText && !string.IsNullOrEmpty(content)) || (hasImage && contentType == "Image"))
                            {
                                System.Diagnostics.Debug.WriteLine($"Clipboard content changed: Type={contentType}, Length={content.Length}");
                                _lastClipboardContent = contentKey;
                                
                                var item = new ClipboardItem
                                {
                                    Content = content,
                                    CreatedAt = DateTime.Now,
                                    LastUsedAt = DateTime.Now,
                                    ContentType = contentType
                                };

                                // 在主线程上触发事件
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    ClipboardChanged?.Invoke(this, item);
                                });
                            }
                            else
                            {
                                // 如果内容变为空，也更新_lastClipboardContent，但不触发事件
                                _lastClipboardContent = contentKey;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error monitoring clipboard: {ex.Message}");
                    }
                    finally
                    {
                        _checkLock.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要处理
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in monitoring loop: {ex.Message}");
            }
        }, _cancellationTokenSource.Token);
    }

    public Task StopMonitoringAsync()
    {
        _isMonitoring = false;
        _cancellationTokenSource?.Cancel();
        _monitoringTimer?.Dispose();
        _monitoringTimer = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        return Task.CompletedTask;
    }

    public async Task<List<ClipboardItem>> GetItemsAsync()
    {
        // 这个方法由 ClipboardManagerService 处理
        return await Task.FromResult(new List<ClipboardItem>());
    }

    public async Task<List<ClipboardGroup>> GetGroupsAsync()
    {
        // 这个方法由 ClipboardManagerService 处理
        return await Task.FromResult(new List<ClipboardGroup>());
    }

    public async Task AddItemAsync(ClipboardItem item)
    {
        // 这个方法由 ClipboardManagerService 处理
        await Task.CompletedTask;
    }

    public async Task RemoveItemAsync(string itemId)
    {
        // 这个方法由 ClipboardManagerService 处理
        await Task.CompletedTask;
    }

    public async Task UpdateItemAsync(ClipboardItem item)
    {
        // 这个方法由 ClipboardManagerService 处理
        await Task.CompletedTask;
    }

    public async Task PinItemAsync(string itemId, bool isPinned)
    {
        // 这个方法由 ClipboardManagerService 处理
        await Task.CompletedTask;
    }

    public async Task<ClipboardGroup> CreateGroupAsync(string name, string? color = null)
    {
        // 这个方法由 ClipboardManagerService 处理
        return await Task.FromResult(new ClipboardGroup());
    }

    public async Task AddItemToGroupAsync(string itemId, string? groupId)
    {
        // 这个方法由 ClipboardManagerService 处理
        await Task.CompletedTask;
    }

    public async Task RemoveGroupAsync(string groupId)
    {
        // 这个方法由 ClipboardManagerService 处理
        await Task.CompletedTask;
    }

    public async Task CopyToClipboardAsync(string content)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Clipboard.SetTextAsync(content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
            }
        });
    }

    public async Task<string> GetClipboardTextAsync()
    {
        try
        {
            // 确保在主线程上访问剪贴板
            if (MainThread.IsMainThread)
            {
                if (Clipboard.HasText)
                {
                    return await Clipboard.GetTextAsync() ?? string.Empty;
                }
                return string.Empty;
            }
            else
            {
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        if (Clipboard.HasText)
                        {
                            return await Clipboard.GetTextAsync() ?? string.Empty;
                        }
                        return string.Empty;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting clipboard text: {ex.Message}");
            return string.Empty;
        }
    }
    
    private Task<bool> CheckClipboardHasImageAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                // 使用Windows API检查剪贴板是否有图片
                // 剪贴板格式常量
                const uint CF_DIB = 8;
                const uint CF_DIBV5 = 17;
                const uint CF_BITMAP = 2;
                
                if (!Vanara.PInvoke.User32.OpenClipboard(IntPtr.Zero))
                    return false;
                
                try
                {
                    // 检查是否有DIB (Device Independent Bitmap) 格式
                    var hasDib = Vanara.PInvoke.User32.IsClipboardFormatAvailable(CF_DIB);
                    // 检查是否有DIBV5格式
                    var hasDibV5 = Vanara.PInvoke.User32.IsClipboardFormatAvailable(CF_DIBV5);
                    // 检查是否有位图格式
                    var hasBitmap = Vanara.PInvoke.User32.IsClipboardFormatAvailable(CF_BITMAP);
                    
                    return hasDib || hasDibV5 || hasBitmap;
                }
                finally
                {
                    Vanara.PInvoke.User32.CloseClipboard();
                }
            }
            catch
            {
                return false;
            }
        });
    }
    
    private Task<byte[]?> GetClipboardImageAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                // 剪贴板格式常量
                const uint CF_DIB = 8;
                const uint CF_DIBV5 = 17;
                
                if (!Vanara.PInvoke.User32.OpenClipboard(IntPtr.Zero))
                    return null;
                
                try
                {
                    // 尝试获取DIB格式的图片数据
                    var hMem = Vanara.PInvoke.User32.GetClipboardData(CF_DIB);
                    if (hMem == IntPtr.Zero)
                    {
                        // 尝试DIBV5格式
                        hMem = Vanara.PInvoke.User32.GetClipboardData(CF_DIBV5);
                    }
                    
                    if (hMem == IntPtr.Zero)
                        return null;
                    
                    var ptr = Vanara.PInvoke.Kernel32.GlobalLock(hMem);
                    if (ptr == IntPtr.Zero)
                        return null;
                    
                    try
                    {
                        // 获取数据大小
                        var size = Vanara.PInvoke.Kernel32.GlobalSize(hMem);
                        if (size == 0)
                            return null;
                        
                        // 复制数据
                        var buffer = new byte[size];
                        Marshal.Copy(ptr, buffer, 0, (int)size);
                        
                        // 将DIB转换为PNG格式（简化处理，直接返回DIB数据）
                        // 注意：实际应用中可能需要将DIB转换为更通用的格式如PNG
                        return buffer;
                    }
                    finally
                    {
                        Vanara.PInvoke.Kernel32.GlobalUnlock(hMem);
                    }
                }
                finally
                {
                    Vanara.PInvoke.User32.CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting clipboard image: {ex.Message}");
                return null;
            }
        });
    }
}
#endif


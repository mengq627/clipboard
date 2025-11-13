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
                        var currentContent = await GetClipboardTextAsync();
                        
                        // 检查内容是否变化（包括从空到有内容的情况）
                        if (currentContent != _lastClipboardContent)
                        {
                            // 只有当新内容不为空时才触发事件
                            if (!string.IsNullOrEmpty(currentContent))
                            {
                                System.Diagnostics.Debug.WriteLine($"Clipboard content changed: '{currentContent.Substring(0, Math.Min(50, currentContent.Length))}...'");
                                _lastClipboardContent = currentContent;
                                
                                var item = new ClipboardItem
                                {
                                    Content = currentContent,
                                    CreatedAt = DateTime.Now,
                                    LastUsedAt = DateTime.Now,
                                    ContentType = "Text"
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
                                _lastClipboardContent = currentContent;
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
}
#endif


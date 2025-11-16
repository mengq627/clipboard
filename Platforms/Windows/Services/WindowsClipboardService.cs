#if WINDOWS
using System.Runtime.InteropServices;
using System.Text;
using clipboard.Models;
using clipboard.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.Drawing;
using System.Drawing.Imaging;

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
                        string contentType = "Text";
                        string content = string.Empty;
                        bool hasContent = false;
                        
                        // 优先检查图片内容（使用Windows API）
                        var hasImage = await CheckClipboardHasImageAsync();
                        
                        if (hasImage)
                        {
                            try
                            {
                                var imageBytes = await GetClipboardImageAsync();
                                if (imageBytes != null && imageBytes.Length > 0)
                                {
                                    contentType = "Image";
                                    content = Convert.ToBase64String(imageBytes);
                                    hasContent = true;
                                    System.Diagnostics.Debug.WriteLine($"Detected image in clipboard, size: {imageBytes.Length} bytes");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error getting image from clipboard: {ex.Message}");
                            }
                        }
                        
                        // 如果没有图片，检查文本内容
                        if (!hasContent)
                        {
                            var currentContent = await GetClipboardTextAsync();
                            if (!string.IsNullOrEmpty(currentContent))
                            {
                                contentType = "Text";
                                content = currentContent;
                                hasContent = true;
                            }
                        }
                        
                        // 检查内容是否变化
                        var contentKey = contentType == "Image" ? $"IMAGE:{content.Substring(0, Math.Min(50, content.Length))}" : content;
                        if (hasContent && contentKey != _lastClipboardContent)
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
                        else if (!hasContent)
                        {
                            // 如果内容变为空，也更新_lastClipboardContent，但不触发事件
                            _lastClipboardContent = string.Empty;
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

    public async Task CopyToClipboardAsync(ClipboardItem item)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                if (item.ContentType == "Image")
                {
                    // 将Base64字符串转换回字节数组并放到剪贴板
                    var imageBytes = Convert.FromBase64String(item.Content);
                    SetClipboardImage(imageBytes);
                }
                else
                {
                    // 文本内容
                    Clipboard.SetTextAsync(item.Content);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying item to clipboard: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 将图片字节数组放到剪贴板
    /// 如果输入是PNG格式，需要转换为DIB格式
    /// </summary>
    private void SetClipboardImage(byte[] imageBytes)
    {
        try
        {
            // 检测图片格式：PNG格式以 89 50 4E 47 开头
            bool isPng = imageBytes.Length >= 4 && 
                         imageBytes[0] == 0x89 && 
                         imageBytes[1] == 0x50 && 
                         imageBytes[2] == 0x4E && 
                         imageBytes[3] == 0x47;
            
            byte[] dibData;
            if (isPng)
            {
                // PNG格式，需要转换为DIB格式
                System.Diagnostics.Debug.WriteLine("Converting PNG to DIB for clipboard");
                dibData = ConvertPngToDib(imageBytes);
                if (dibData == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to convert PNG to DIB");
                    return;
                }
            }
            else
            {
                // 假设已经是DIB格式
                dibData = imageBytes;
            }
            
            const uint CF_DIB = 8;
            
            if (!Vanara.PInvoke.User32.OpenClipboard(IntPtr.Zero))
            {
                System.Diagnostics.Debug.WriteLine("Failed to open clipboard");
                return;
            }
            
            try
            {
                // 清空剪贴板
                Vanara.PInvoke.User32.EmptyClipboard();
                
                // 分配全局内存
                var hMem = Vanara.PInvoke.Kernel32.GlobalAlloc(
                    Vanara.PInvoke.Kernel32.GMEM.GMEM_MOVEABLE,
                    (nuint)dibData.Length);
                
                if (hMem.IsNull)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to allocate global memory");
                    return;
                }
                
                try
                {
                    // 锁定内存并复制数据
                    var ptr = Vanara.PInvoke.Kernel32.GlobalLock(hMem);
                    if (ptr != IntPtr.Zero)
                    {
                        try
                        {
                            Marshal.Copy(dibData, 0, ptr, dibData.Length);
                        }
                        finally
                        {
                            Vanara.PInvoke.Kernel32.GlobalUnlock(hMem);
                        }
                    }
                    
                    // 将数据放到剪贴板
                    if (Vanara.PInvoke.User32.SetClipboardData(CF_DIB, hMem.DangerousGetHandle()) == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to set clipboard data");
                        Vanara.PInvoke.Kernel32.GlobalFree(hMem);
                    }
                    // 注意：如果SetClipboardData成功，系统会拥有hMem的所有权，我们不应该释放它
                }
                catch
                {
                    Vanara.PInvoke.Kernel32.GlobalFree(hMem);
                    throw;
                }
            }
            finally
            {
                Vanara.PInvoke.User32.CloseClipboard();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting clipboard image: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 将PNG格式转换为DIB格式
    /// </summary>
    private byte[]? ConvertPngToDib(byte[] pngData)
    {
        try
        {
            // 使用System.Drawing将PNG加载为位图
            using (var pngStream = new MemoryStream(pngData))
            using (var sourceBmp = new System.Drawing.Bitmap(pngStream))
            {
                // 转换为32位RGB格式（去掉Alpha通道，确保兼容性）
                // 使用Format24bppRgb或Format32bppRgb都可以，但DIB最终会转换为24位BGR
                using (var bmp = new System.Drawing.Bitmap(sourceBmp.Width, sourceBmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    // 使用白色背景绘制（处理透明区域）
                    g.Clear(System.Drawing.Color.White);
                    g.DrawImage(sourceBmp, 0, 0);
                    
                    // 将位图转换为DIB格式
                    return ConvertBitmapToDib(bmp);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error converting PNG to DIB: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 将System.Drawing.Bitmap转换为DIB格式
    /// </summary>
    private byte[]? ConvertBitmapToDib(System.Drawing.Bitmap bitmap)
    {
        try
        {
            // 获取位图信息
            int width = bitmap.Width;
            int height = bitmap.Height;
            int bitsPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat);
            
            // DIB格式使用24位RGB（BGR顺序），去掉Alpha通道以确保兼容性
            int dibBitsPerPixel = 24;
            
            // 计算每行字节数（必须是4的倍数）
            int stride = ((width * dibBitsPerPixel + 31) / 32) * 4;
            int imageSize = stride * height;
            
            // BITMAPINFOHEADER结构（40字节）
            using (var ms = new MemoryStream())
            {
                // biSize (4 bytes) - BITMAPINFOHEADER大小
                WriteUInt32(ms, 40);
                
                // biWidth (4 bytes)
                WriteInt32(ms, width);
                
                // biHeight (4 bytes) - 正数表示自下而上的DIB
                WriteInt32(ms, height);
                
                // biPlanes (2 bytes)
                WriteUInt16(ms, 1);
                
                // biBitCount (2 bytes) - 使用计算后的DIB位深度
                WriteUInt16(ms, (ushort)dibBitsPerPixel);
                
                // biCompression (4 bytes) - 0 = BI_RGB
                WriteUInt32(ms, 0);
                
                // biSizeImage (4 bytes) - 图像数据大小
                WriteUInt32(ms, (uint)imageSize);
                
                // biXPelsPerMeter (4 bytes)
                WriteInt32(ms, 0);
                
                // biYPelsPerMeter (4 bytes)
                WriteInt32(ms, 0);
                
                // biClrUsed (4 bytes) - 0表示使用所有颜色
                WriteUInt32(ms, 0);
                
                // biClrImportant (4 bytes)
                WriteUInt32(ms, 0);
                
                // 如果是32位或24位，不需要颜色表，直接写入位图数据
                if (bitsPerPixel >= 24)
                {
                    // 锁定位图数据
                    var rect = new System.Drawing.Rectangle(0, 0, width, height);
                    var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
                    
                    try
                    {
                        // 分配DIB数据缓冲区
                        int dibSize = 40 + imageSize;
                        var dibData = new byte[dibSize];
                        
                        // 复制BITMAPINFOHEADER
                        ms.Position = 0;
                        ms.Read(dibData, 0, 40);
                        
                        // 复制位图数据（自下而上，DIB格式要求）
                        int srcStride = bmpData.Stride;
                        int dstOffset = 40;
                        int srcBytesPerPixel = bitsPerPixel / 8;
                        int dstBytesPerPixel = dibBitsPerPixel / 8;
                        
                        unsafe
                        {
                            byte* srcPtr = (byte*)bmpData.Scan0;
                            for (int y = 0; y < height; y++)
                            {
                                // DIB格式是自下而上的，所以从底部开始
                                int srcRow = height - 1 - y;
                                int srcOffset = srcRow * srcStride;
                                int dstRowOffset = dstOffset + y * stride;
                                
                                // 复制一行数据（BGR格式，不是RGB）
                                for (int x = 0; x < width; x++)
                                {
                                    int srcPixelOffset = srcOffset + x * srcBytesPerPixel;
                                    int dstPixelOffset = dstRowOffset + x * dstBytesPerPixel;
                                    
                                    // 统一转换为24位BGR格式
                                    // System.Drawing使用BGR顺序（Format24bppRgb实际上是BGR）
                                    if (bitsPerPixel == 24)
                                    {
                                        // 24位：BGR（System.Drawing的Format24bppRgb就是BGR顺序）
                                        dibData[dstPixelOffset + 0] = srcPtr[srcPixelOffset + 0]; // B
                                        dibData[dstPixelOffset + 1] = srcPtr[srcPixelOffset + 1]; // G
                                        dibData[dstPixelOffset + 2] = srcPtr[srcPixelOffset + 2]; // R
                                    }
                                    else if (bitsPerPixel == 32)
                                    {
                                        // 32位：可能是BGRA或ARGB，需要根据实际格式处理
                                        // System.Drawing的Format32bppRgb实际上是BGRA顺序
                                        dibData[dstPixelOffset + 0] = srcPtr[srcPixelOffset + 0]; // B
                                        dibData[dstPixelOffset + 1] = srcPtr[srcPixelOffset + 1]; // G
                                        dibData[dstPixelOffset + 2] = srcPtr[srcPixelOffset + 2]; // R
                                        // 跳过Alpha通道（索引3）
                                    }
                                    else
                                    {
                                        // 其他格式，转换为24位BGR
                                        // 这里简化处理，实际应该根据具体格式转换
                                        System.Diagnostics.Debug.WriteLine($"Unsupported pixel format: {bitsPerPixel} bits per pixel");
                                    }
                                }
                                
                                // 填充到4字节对齐
                                int rowDataSize = width * dstBytesPerPixel;
                                for (int x = rowDataSize; x < stride; x++)
                                {
                                    dibData[dstRowOffset + x] = 0;
                                }
                            }
                        }
                        
                        return dibData;
                    }
                    finally
                    {
                        bitmap.UnlockBits(bmpData);
                    }
                }
                else
                {
                    // 对于小于24位的位图，需要颜色表（这里简化处理，转换为32位）
                    // 为了简化，我们转换为32位RGB格式
                    using (var bmp32 = new System.Drawing.Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                    using (var g = System.Drawing.Graphics.FromImage(bmp32))
                    {
                        g.DrawImage(bitmap, 0, 0);
                        return ConvertBitmapToDib(bmp32);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error converting Bitmap to DIB: {ex.Message}");
            return null;
        }
    }
    
    private void WriteUInt32(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)(value & 0xFF));
        ms.WriteByte((byte)((value >> 8) & 0xFF));
        ms.WriteByte((byte)((value >> 16) & 0xFF));
        ms.WriteByte((byte)((value >> 24) & 0xFF));
    }
    
    private void WriteInt32(MemoryStream ms, int value)
    {
        WriteUInt32(ms, (uint)value);
    }
    
    private void WriteUInt16(MemoryStream ms, ushort value)
    {
        ms.WriteByte((byte)(value & 0xFF));
        ms.WriteByte((byte)((value >> 8) & 0xFF));
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
                        
                        // 复制DIB数据
                        var dibBuffer = new byte[size];
                        Marshal.Copy(ptr, dibBuffer, 0, (int)size);
                        
                        // 将DIB转换为PNG格式，以便MAUI Image控件可以显示
                        return ConvertDibToPng(dibBuffer);
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

    /// <summary>
    /// 将DIB格式转换为PNG格式，以便MAUI Image控件可以显示
    /// 通过添加BITMAPFILEHEADER使DIB数据可以被System.Drawing.Bitmap识别
    /// </summary>
    private byte[]? ConvertDibToPng(byte[] dibData)
    {
        try
        {
            // 尝试添加BITMAPFILEHEADER使DIB数据可以被Bitmap识别
            using (var ms = new MemoryStream())
            {
                // 写入BITMAPFILEHEADER (14 bytes)
                ms.WriteByte(0x42); // 'B'
                ms.WriteByte(0x4D); // 'M'
                
                // 文件大小 (4 bytes, little-endian)
                uint fileSize = (uint)(14 + dibData.Length);
                ms.WriteByte((byte)(fileSize & 0xFF));
                ms.WriteByte((byte)((fileSize >> 8) & 0xFF));
                ms.WriteByte((byte)((fileSize >> 16) & 0xFF));
                ms.WriteByte((byte)((fileSize >> 24) & 0xFF));
                
                // 保留字段 (4 bytes)
                ms.WriteByte(0);
                ms.WriteByte(0);
                ms.WriteByte(0);
                ms.WriteByte(0);
                
                // 数据偏移量 (4 bytes, little-endian)
                // DIB格式：BITMAPFILEHEADER(14) + BITMAPINFOHEADER(40) = 54
                // 但实际偏移量取决于BITMAPINFOHEADER的大小，这里使用14作为安全值
                // 如果DIB数据已经包含BITMAPINFOHEADER，偏移量应该是14
                uint dataOffset = 14;
                ms.WriteByte((byte)(dataOffset & 0xFF));
                ms.WriteByte((byte)((dataOffset >> 8) & 0xFF));
                ms.WriteByte((byte)((dataOffset >> 16) & 0xFF));
                ms.WriteByte((byte)((dataOffset >> 24) & 0xFF));
                
                // 写入DIB数据
                ms.Write(dibData, 0, dibData.Length);
                ms.Position = 0;
                
                using (var bmp = new System.Drawing.Bitmap(ms))
                using (var pngStream = new MemoryStream())
                {
                    bmp.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
                    var pngData = pngStream.ToArray();
                    System.Diagnostics.Debug.WriteLine($"Successfully converted DIB to PNG using System.Drawing, size: {dibData.Length} -> {pngData.Length} bytes");
                    return pngData;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error converting DIB to PNG using System.Drawing: {ex.Message}");
            return dibData;
        }
    }
}
#endif


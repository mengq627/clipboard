using clipboard.Models;

namespace clipboard.Services;

public interface IClipboardService
{
    event EventHandler<ClipboardItem>? ClipboardChanged;
    
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
    Task<List<ClipboardItem>> GetItemsAsync();
    Task<List<ClipboardGroup>> GetGroupsAsync();
    Task AddItemAsync(ClipboardItem item);
    Task RemoveItemAsync(string itemId);
    Task UpdateItemAsync(ClipboardItem item);
    Task PinItemAsync(string itemId, bool isPinned);
    Task<ClipboardGroup> CreateGroupAsync(string name, string? color = null);
    Task AddItemToGroupAsync(string itemId, string? groupId);
    Task RemoveGroupAsync(string groupId);
    Task CopyToClipboardAsync(string content);
    Task<string> GetClipboardTextAsync();
}


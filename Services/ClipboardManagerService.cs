using System.Collections.ObjectModel;
using System.Text.Json;
using clipboard.Models;

namespace clipboard.Services;

public class ClipboardManagerService : IClipboardService
{
    private readonly List<ClipboardItem> _items = new();
    private readonly List<ClipboardGroup> _groups = new();
    private readonly string _dataFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IClipboardService? _platformService;
    private bool _ignoreNextChange = false;

    public event EventHandler<ClipboardItem>? ClipboardChanged;

    public ClipboardManagerService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipboardApp"
        );
        Directory.CreateDirectory(appDataPath);
        _dataFilePath = Path.Combine(appDataPath, "clipboard_data.json");
        LoadData();
    }

    public void SetPlatformService(IClipboardService platformService)
    {
        _platformService = platformService;
        if (_platformService != null)
        {
            _platformService.ClipboardChanged += OnPlatformClipboardChanged;
        }
    }

    private async void OnPlatformClipboardChanged(object? sender, ClipboardItem item)
    {
        if (_ignoreNextChange)
        {
            _ignoreNextChange = false;
            return;
        }
        await HandleNewClipboardItem(item);
    }

    private async Task HandleNewClipboardItem(ClipboardItem newItem)
    {
        await _lock.WaitAsync();
        try
        {
            // 检查重复内容 - 删除旧的内容
            var existingItem = _items.FirstOrDefault(i => 
                i.Content == newItem.Content && !i.IsPinned);
            
            if (existingItem != null)
            {
                _items.Remove(existingItem);
            }

            // 添加新项到列表开头（最新的在前面）
            _items.Insert(0, newItem);

            // 限制历史记录数量（保留最多500条，不包括置顶的）
            var nonPinnedItems = _items.Where(i => !i.IsPinned).Skip(500).ToList();
            foreach (var item in nonPinnedItems)
            {
                _items.Remove(item);
            }

            SaveData();
            ClipboardChanged?.Invoke(this, newItem);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StartMonitoringAsync()
    {
        if (_platformService != null)
        {
            await _platformService.StartMonitoringAsync();
        }
    }

    public async Task StopMonitoringAsync()
    {
        if (_platformService != null)
        {
            await _platformService.StopMonitoringAsync();
        }
    }

    public async Task<List<ClipboardItem>> GetItemsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // 置顶项显示在最上面，然后按时间倒序
            return _items
                .OrderByDescending(i => i.IsPinned)
                .ThenByDescending(i => i.LastUsedAt)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ClipboardGroup>> GetGroupsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // 更新每个组的项目列表
            foreach (var group in _groups)
            {
                group.Items = _items
                    .Where(i => i.GroupId == group.Id)
                    .OrderByDescending(i => i.IsPinned)
                    .ThenByDescending(i => i.LastUsedAt)
                    .ToList();
            }
            return _groups.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddItemAsync(ClipboardItem item)
    {
        await HandleNewClipboardItem(item);
    }

    public async Task RemoveItemAsync(string itemId)
    {
        await _lock.WaitAsync();
        try
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                _items.Remove(item);
                SaveData();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateItemAsync(ClipboardItem item)
    {
        await _lock.WaitAsync();
        try
        {
            var index = _items.FindIndex(i => i.Id == item.Id);
            if (index >= 0)
            {
                _items[index] = item;
                SaveData();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PinItemAsync(string itemId, bool isPinned)
    {
        await _lock.WaitAsync();
        try
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                item.IsPinned = isPinned;
                SaveData();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ClipboardGroup> CreateGroupAsync(string name, string? color = null)
    {
        await _lock.WaitAsync();
        try
        {
            var group = new ClipboardGroup
            {
                Name = name,
                Color = color ?? "#512BD4"
            };
            _groups.Add(group);
            SaveData();
            return group;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddItemToGroupAsync(string itemId, string? groupId)
    {
        await _lock.WaitAsync();
        try
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                item.GroupId = groupId;
                SaveData();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveGroupAsync(string groupId)
    {
        await _lock.WaitAsync();
        try
        {
            var group = _groups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                // 移除组中所有项目的组关联
                foreach (var item in _items.Where(i => i.GroupId == groupId))
                {
                    item.GroupId = null;
                }
                _groups.Remove(group);
                SaveData();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CopyToClipboardAsync(string content)
    {
        if (_platformService != null)
        {
            _ignoreNextChange = true;
            await _platformService.CopyToClipboardAsync(content);
        }
    }

    public async Task<string> GetClipboardTextAsync()
    {
        if (_platformService != null)
        {
            return await _platformService.GetClipboardTextAsync();
        }
        return string.Empty;
    }

    private void LoadData()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                var json = File.ReadAllText(_dataFilePath);
                var data = JsonSerializer.Deserialize<ClipboardData>(json);
                if (data != null)
                {
                    _items.Clear();
                    _items.AddRange(data.Items);
                    _groups.Clear();
                    _groups.AddRange(data.Groups);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
        }
    }

    private void SaveData()
    {
        try
        {
            var data = new ClipboardData
            {
                Items = _items,
                Groups = _groups
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            File.WriteAllText(_dataFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
        }
    }

    private class ClipboardData
    {
        public List<ClipboardItem> Items { get; set; } = new();
        public List<ClipboardGroup> Groups { get; set; } = new();
    }
}


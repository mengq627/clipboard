using System.Collections.ObjectModel;
using System.Windows.Input;
using clipboard.Models;
using clipboard.Services;

namespace clipboard.ViewModels;

public class ClipboardViewModel : BindableObject
{
    private readonly IClipboardService _clipboardService;
    private ClipboardGroup? _selectedGroup;

    public ClipboardViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        _clipboardService.ClipboardChanged += OnClipboardChanged;

        Items = new ObservableCollection<ClipboardItem>();
        Groups = new ObservableCollection<ClipboardGroup>();

        LoadDataCommand = new Command(async () => await LoadDataAsync());
        CopyItemCommand = new Command<string>(async (id) => await CopyItemAsync(id));
        DeleteItemCommand = new Command<string>(async (id) => await DeleteItemAsync(id));
        PinItemCommand = new Command<string>(async (id) => await PinItemAsync(id));
        CreateGroupCommand = new Command(async () => await CreateGroupAsync());
        AddToGroupCommand = new Command<string>(async (itemId) => await AddToGroupAsync(itemId));
        RemoveFromGroupCommand = new Command<string>(async (itemId) => await RemoveFromGroupAsync(itemId));
        DeleteGroupCommand = new Command<string>(async (groupId) => await DeleteGroupAsync(groupId));
        ClearAllCommand = new Command(async () => await ClearAllAsync());

        _ = LoadDataAsync();
        _ = _clipboardService.StartMonitoringAsync();
    }

    public ObservableCollection<ClipboardItem> Items { get; }
    public ObservableCollection<ClipboardGroup> Groups { get; }

    public ClipboardGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup?.Id != value?.Id)
            {
                _selectedGroup = value;
                OnPropertyChanged();
                // 切换分组时只更新项目列表，不重新加载分组列表
                _ = LoadItemsAsync();
            }
        }
    }

    public ICommand LoadDataCommand { get; }
    public ICommand CopyItemCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand PinItemCommand { get; }
    public ICommand CreateGroupCommand { get; }
    public ICommand AddToGroupCommand { get; }
    public ICommand RemoveFromGroupCommand { get; }
    public ICommand DeleteGroupCommand { get; }
    public ICommand ClearAllCommand { get; }

    private async void OnClipboardChanged(object? sender, ClipboardItem item)
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var items = await _clipboardService.GetItemsAsync();
            var groups = await _clipboardService.GetGroupsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // 先更新分组列表
                Groups.Clear();
                foreach (var group in groups)
                {
                    Groups.Add(group);
                }

                // 如果选择了分组，更新SelectedGroup引用（因为之前的对象可能已经过时）
                if (_selectedGroup != null)
                {
                    _selectedGroup = Groups.FirstOrDefault(g => g.Id == _selectedGroup.Id);
                }

                // 然后更新项目列表
                LoadItemsInternal(items);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
        }
    }

    private async Task LoadItemsAsync()
    {
        try
        {
            var items = await _clipboardService.GetItemsAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadItemsInternal(items);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading items: {ex.Message}");
        }
    }

    private void LoadItemsInternal(List<ClipboardItem> items)
    {
        // 过滤项目（根据选择的分组）
        var filteredItems = items.Where(item => 
            _selectedGroup == null || item.GroupId == _selectedGroup.Id).ToList();
        
        // 检查是否需要更新（避免不必要的刷新）
        if (Items.Count == filteredItems.Count)
        {
            var currentIds = Items.Select(i => i.Id).OrderBy(id => id).ToList();
            var newIds = filteredItems.Select(i => i.Id).OrderBy(id => id).ToList();
            
            if (currentIds.SequenceEqual(newIds))
            {
                // ID相同，只更新现有项目的属性，避免整个列表刷新
                foreach (var newItem in filteredItems)
                {
                    var existingItem = Items.FirstOrDefault(i => i.Id == newItem.Id);
                    if (existingItem != null)
                    {
                        // 更新属性
                        existingItem.Content = newItem.Content;
                        existingItem.IsPinned = newItem.IsPinned;
                        existingItem.LastUsedAt = newItem.LastUsedAt;
                        existingItem.GroupId = newItem.GroupId;
                        existingItem.ContentType = newItem.ContentType;
                    }
                }
                return;
            }
        }
        
        // 需要完全刷新
        Items.Clear();
        foreach (var item in filteredItems)
        {
            Items.Add(item);
        }
    }

    private async Task CopyItemAsync(string itemId)
    {
        try
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                await _clipboardService.CopyToClipboardAsync(item.Content);
                item.LastUsedAt = DateTime.Now;
                await _clipboardService.UpdateItemAsync(item);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying item: {ex.Message}");
        }
    }

    private async Task DeleteItemAsync(string itemId)
    {
        try
        {
            await _clipboardService.RemoveItemAsync(itemId);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting item: {ex.Message}");
        }
    }

    private async Task PinItemAsync(string itemId)
    {
        try
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                await _clipboardService.PinItemAsync(itemId, !item.IsPinned);
                // 只更新项目列表，不更新分组列表，避免分组栏刷新
                await LoadItemsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error pinning item: {ex.Message}");
        }
    }

    private async Task CreateGroupAsync()
    {
        try
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page as Page;
            if (page == null) return;
            
            var groupName = await page.DisplayPromptAsync(
                "创建分组",
                "请输入分组名称:",
                "确定",
                "取消",
                "新分组",
                -1,
                Microsoft.Maui.Keyboard.Default);

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                await _clipboardService.CreateGroupAsync(groupName);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating group: {ex.Message}");
        }
    }

    private async Task AddToGroupAsync(string itemId)
    {
        try
        {
            var groupNames = Groups.Select(g => g.Name).ToList();
            groupNames.Add("无分组");

            var page = Application.Current?.Windows.FirstOrDefault()?.Page as Page;
            if (page == null) return;
            
            var selected = await page.DisplayActionSheet(
                "选择分组",
                "取消",
                null,
                groupNames.ToArray());

            if (selected != null && selected != "取消")
            {
                var groupId = selected == "无分组" 
                    ? null 
                    : Groups.FirstOrDefault(g => g.Name == selected)?.Id;
                
                await _clipboardService.AddItemToGroupAsync(itemId, groupId);
                
                // 更新分组列表以刷新 ItemCount
                await UpdateGroupsAsync();
                // 更新项目列表
                await LoadItemsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding to group: {ex.Message}");
        }
    }

    private async Task UpdateGroupsAsync()
    {
        try
        {
            var groups = await _clipboardService.GetGroupsAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // 更新分组列表，通过替换对象来触发 UI 更新
                var existingGroupIds = Groups.Select(g => g.Id).ToHashSet();
                var newGroupIds = groups.Select(g => g.Id).ToHashSet();
                
                // 移除不存在的分组
                for (int i = Groups.Count - 1; i >= 0; i--)
                {
                    if (!newGroupIds.Contains(Groups[i].Id))
                    {
                        Groups.RemoveAt(i);
                    }
                }
                
                // 更新或添加分组
                foreach (var group in groups)
                {
                    var existingIndex = Groups.ToList().FindIndex(g => g.Id == group.Id);
                    if (existingIndex >= 0)
                    {
                        // 替换现有分组以触发 UI 更新（包括 ItemCount）
                        Groups[existingIndex] = group;
                    }
                    else
                    {
                        // 添加新分组
                        Groups.Add(group);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating groups: {ex.Message}");
        }
    }

    private async Task RemoveFromGroupAsync(string itemId)
    {
        try
        {
            await _clipboardService.AddItemToGroupAsync(itemId, null);
            // 更新分组列表以刷新 ItemCount
            await UpdateGroupsAsync();
            // 更新项目列表
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing from group: {ex.Message}");
        }
    }

    private async Task DeleteGroupAsync(string groupId)
    {
        try
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page as Page;
            if (page == null) return;
            
            var confirmed = await page.DisplayAlert(
                "确认删除",
                "确定要删除这个分组吗？分组中的项目将移到未分组。",
                "删除",
                "取消");

            if (confirmed)
            {
                await _clipboardService.RemoveGroupAsync(groupId);
                if (SelectedGroup?.Id == groupId)
                {
                    SelectedGroup = null;
                }
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting group: {ex.Message}");
        }
    }

    private async Task ClearAllAsync()
    {
        try
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page as Page;
            if (page == null) return;
            
            var confirmed = await page.DisplayAlert(
                "确认清除",
                "确定要清除所有未置顶的历史记录吗？",
                "清除",
                "取消");

            if (confirmed)
            {
                var itemsToRemove = Items.Where(i => !i.IsPinned).ToList();
                foreach (var item in itemsToRemove)
                {
                    await _clipboardService.RemoveItemAsync(item.Id);
                }
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing all: {ex.Message}");
        }
    }
}


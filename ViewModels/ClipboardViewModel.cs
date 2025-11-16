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
        
        System.Diagnostics.Debug.WriteLine($"LoadItemsInternal: Current items count={Items.Count}, Filtered items count={filteredItems.Count}");
        
        // 检查是否需要更新（避免不必要的刷新）
        if (Items.Count == filteredItems.Count)
        {
            var currentIds = Items.Select(i => i.Id).OrderBy(id => id).ToList();
            var newIds = filteredItems.Select(i => i.Id).OrderBy(id => id).ToList();
            
            if (currentIds.SequenceEqual(newIds))
            {
                // ID相同，检查顺序是否相同
                var currentOrder = Items.Select(i => i.Id).ToList();
                var newOrder = filteredItems.Select(i => i.Id).ToList();
                
                if (currentOrder.SequenceEqual(newOrder))
                {
                    // 顺序也相同，只更新现有项目的属性，避免整个列表刷新
                    System.Diagnostics.Debug.WriteLine("LoadItemsInternal: Only updating properties, no list refresh");
                    foreach (var newItem in filteredItems)
                    {
                        var existingItem = Items.FirstOrDefault(i => i.Id == newItem.Id);
                        if (existingItem != null)
                        {
                            // 更新属性（会触发 PropertyChanged 事件）
                            existingItem.Content = newItem.Content;
                            existingItem.IsPinned = newItem.IsPinned;
                            existingItem.LastUsedAt = newItem.LastUsedAt;
                            existingItem.GroupId = newItem.GroupId;
                            existingItem.ContentType = newItem.ContentType;
                            System.Diagnostics.Debug.WriteLine($"Updated item {existingItem.Id}: IsPinned={existingItem.IsPinned}");
                        }
                    }
                    return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("LoadItemsInternal: Order changed, refreshing list");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("LoadItemsInternal: IDs changed, refreshing list");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("LoadItemsInternal: Count changed, refreshing list");
        }
        
        // 需要完全刷新
        System.Diagnostics.Debug.WriteLine("LoadItemsInternal: Performing full list refresh");
        Items.Clear();
        foreach (var item in filteredItems)
        {
            Items.Add(item);
            System.Diagnostics.Debug.WriteLine($"Added item {item.Id}: IsPinned={item.IsPinned}");
        }
    }

    private async Task CopyItemAsync(string itemId)
    {
        try
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                // 使用新的方法，传递完整的item信息以支持图片复制
                await _clipboardService.CopyToClipboardAsync(item);
                item.LastUsedAt = DateTime.Now;
                await _clipboardService.UpdateItemAsync(item);
                // 只更新项目列表，不刷新分组列表（因为复制操作不会影响分组）
                await LoadItemsAsync();
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
            // 获取要删除的项目，检查它是否属于某个分组
            var item = Items.FirstOrDefault(i => i.Id == itemId);
            var groupId = item?.GroupId;
            
            await _clipboardService.RemoveItemAsync(itemId);
            
            // 如果删除的项目属于某个分组，只更新该分组的 ItemCount
            if (groupId != null)
            {
                await UpdateGroupItemCountAsync(groupId);
            }
            
            // 更新项目列表
            await LoadItemsAsync();
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
                var wasPinned = item.IsPinned;
                var willBePinned = !wasPinned;
                var isFirstItem = Items.IndexOf(item) == 0;
                
                System.Diagnostics.Debug.WriteLine($"PinItemAsync: itemId={itemId}, wasPinned={wasPinned}, willBePinned={willBePinned}, isFirstItem={isFirstItem}");
                
                // 更新服务中的置顶状态
                await _clipboardService.PinItemAsync(itemId, willBePinned);
                
                // 如果置顶的是最上方的项目，只更新属性，不刷新列表
                if (isFirstItem && willBePinned)
                {
                    // 只更新置顶状态，不刷新列表
                    System.Diagnostics.Debug.WriteLine($"Updating IsPinned property directly for first item");
                    item.IsPinned = willBePinned;
                }
                else
                {
                    // 如果置顶的不是最上方的项目，或者取消置顶，需要重新排序
                    System.Diagnostics.Debug.WriteLine($"Reloading items list for reordering");
                    await LoadItemsAsync();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"PinItemAsync: Item not found with id={itemId}");
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
                // 获取当前项目的分组信息
                var item = Items.FirstOrDefault(i => i.Id == itemId);
                var oldGroupId = item?.GroupId;
                
                var newGroupId = selected == "无分组" 
                    ? null 
                    : Groups.FirstOrDefault(g => g.Name == selected)?.Id;
                
                await _clipboardService.AddItemToGroupAsync(itemId, newGroupId);
                
                // 只更新受影响的分组的 ItemCount，不刷新整个分组列表
                await UpdateGroupItemCountAsync(oldGroupId);
                await UpdateGroupItemCountAsync(newGroupId);
                
                // 更新项目列表
                await LoadItemsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding to group: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新分组列表（仅在分组增加或删除时调用）
    /// </summary>
    private async Task UpdateGroupsAsync()
    {
        try
        {
            var groups = await _clipboardService.GetGroupsAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var existingGroupIds = Groups.Select(g => g.Id).ToHashSet();
                var newGroupIds = groups.Select(g => g.Id).ToHashSet();
                
                // 检查是否有分组增加或删除
                var hasGroupAdded = newGroupIds.Except(existingGroupIds).Any();
                var hasGroupRemoved = existingGroupIds.Except(newGroupIds).Any();
                
                if (hasGroupAdded || hasGroupRemoved)
                {
                    // 有分组增加或删除，刷新整个分组列表
                    System.Diagnostics.Debug.WriteLine("Groups added or removed, refreshing entire group list");
                    Groups.Clear();
                    foreach (var group in groups)
                    {
                        Groups.Add(group);
                    }
                    
                    // 如果选择了分组，更新SelectedGroup引用
                    if (_selectedGroup != null)
                    {
                        _selectedGroup = Groups.FirstOrDefault(g => g.Id == _selectedGroup.Id);
                    }
                }
                else
                {
                    // 没有分组增加或删除，只更新现有分组的 ItemCount
                    System.Diagnostics.Debug.WriteLine("No groups added or removed, only updating ItemCount");
                    foreach (var newGroup in groups)
                    {
                        var existingGroup = Groups.FirstOrDefault(g => g.Id == newGroup.Id);
                        if (existingGroup != null)
                        {
                            // 只更新 Items 列表，这会触发 ItemCount 的 PropertyChanged
                            existingGroup.Items = newGroup.Items;
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating groups: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 只更新特定分组的 ItemCount（不刷新整个分组列表）
    /// </summary>
    private async Task UpdateGroupItemCountAsync(string? groupId)
    {
        if (groupId == null) return; // 无分组，不需要更新
        
        try
        {
            var groups = await _clipboardService.GetGroupsAsync();
            var updatedGroup = groups.FirstOrDefault(g => g.Id == groupId);
            
            if (updatedGroup != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existingGroup = Groups.FirstOrDefault(g => g.Id == groupId);
                    if (existingGroup != null)
                    {
                        // 只更新 Items 列表，这会触发 ItemCount 的 PropertyChanged
                        existingGroup.Items = updatedGroup.Items;
                        System.Diagnostics.Debug.WriteLine($"Updated ItemCount for group {groupId}: {existingGroup.ItemCount}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating group item count: {ex.Message}");
        }
    }

    private async Task RemoveFromGroupAsync(string itemId)
    {
        try
        {
            // 获取当前项目的分组信息
            var item = Items.FirstOrDefault(i => i.Id == itemId);
            var oldGroupId = item?.GroupId;
            
            await _clipboardService.AddItemToGroupAsync(itemId, null);
            
            // 只更新受影响的分组的 ItemCount，不刷新整个分组列表
            await UpdateGroupItemCountAsync(oldGroupId);
            
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


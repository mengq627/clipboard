using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace clipboard.Models;

public class ClipboardItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _content = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _lastUsedAt = DateTime.Now;
    private bool _isPinned;
    private string? _groupId;
    private string _contentType = "Text";

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime LastUsedAt
    {
        get => _lastUsedAt;
        set => SetProperty(ref _lastUsedAt, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                System.Diagnostics.Debug.WriteLine($"IsPinned changed for item {Id}: {value}");
            }
        }
    }

    public string? GroupId
    {
        get => _groupId;
        set => SetProperty(ref _groupId, value);
    }

    public string ContentType
    {
        get => _contentType;
        set => SetProperty(ref _contentType, value);
    }

    /// <summary>
    /// 是否被选中（用于键盘导航的视觉反馈）
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}


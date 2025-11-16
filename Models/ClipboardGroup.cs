using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace clipboard.Models;

public class ClipboardGroup : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string? _color;
    private DateTime _createdAt = DateTime.Now;
    private List<ClipboardItem> _items = new();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }
    
    [JsonIgnore]
    public int ItemCount => Items?.Count ?? 0;
    
    [JsonIgnore]
    public List<ClipboardItem> Items
    {
        get => _items;
        set
        {
            if (SetProperty(ref _items, value))
            {
                // 当 Items 列表变化时，通知 ItemCount 也变化了
                OnPropertyChanged(nameof(ItemCount));
            }
        }
    }

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


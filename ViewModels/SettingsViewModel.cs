using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using clipboard.Models;
using clipboard.Services;

namespace clipboard.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settingsService;
    private int _maxItemsPerGroup = 100;
    private bool _useWinKey = true;
    private bool _useAltKey = false;
    private string _hotkeyKey = "V";
    
    // 原始设置值（用于比较是否有修改）
    private int _originalMaxItemsPerGroup = 100;
    private bool _originalUseWinKey = true;
    private bool _originalUseAltKey = false;
    private char _originalHotkeyKey = 'V';
    
    private bool _isModified = false;

    public SettingsViewModel(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
        SaveCommand = new Command(async () => await SaveSettingsAsync(), () => IsModified);
    }

    public int MaxItemsPerGroup
    {
        get => _maxItemsPerGroup;
        set
        {
            if (SetProperty(ref _maxItemsPerGroup, value))
            {
                CheckIfModified();
            }
        }
    }

    public bool UseWinKey
    {
        get => _useWinKey;
        set
        {
            if (SetProperty(ref _useWinKey, value))
            {
                if (value)
                {
                    UseAltKey = false;
                }
                OnPropertyChanged(nameof(HotkeyDisplayText));
                CheckIfModified();
            }
        }
    }

    public bool UseAltKey
    {
        get => _useAltKey;
        set
        {
            if (SetProperty(ref _useAltKey, value))
            {
                if (value)
                {
                    UseWinKey = false;
                }
                OnPropertyChanged(nameof(HotkeyDisplayText));
                CheckIfModified();
            }
        }
    }

    public string HotkeyKey
    {
        get => _hotkeyKey;
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                var upperValue = value.ToUpperInvariant();
                if (upperValue.Length == 1 && char.IsLetter(upperValue[0]))
                {
                    if (SetProperty(ref _hotkeyKey, upperValue))
                    {
                        OnPropertyChanged(nameof(HotkeyDisplayText));
                        CheckIfModified();
                    }
                }
            }
        }
    }

    public string HotkeyDisplayText
    {
        get
        {
            var modifier = UseWinKey ? "Win" : (UseAltKey ? "Alt" : "");
            return string.IsNullOrEmpty(modifier) 
                ? $"快捷键：{HotkeyKey}" 
                : $"快捷键：{modifier} + {HotkeyKey}";
        }
    }

    public ICommand SaveCommand { get; }
    
    public bool IsModified
    {
        get => _isModified;
        private set
        {
            if (SetProperty(ref _isModified, value))
            {
                // 更新保存按钮的启用状态
                if (SaveCommand is Command cmd)
                {
                    cmd.ChangeCanExecute();
                }
            }
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsService.GetSettings();
        _originalMaxItemsPerGroup = settings.MaxItemsPerGroup;
        _originalUseWinKey = settings.Hotkey.UseWinKey;
        _originalUseAltKey = settings.Hotkey.UseAltKey;
        _originalHotkeyKey = settings.Hotkey.Key;
        
        MaxItemsPerGroup = settings.MaxItemsPerGroup;
        UseWinKey = settings.Hotkey.UseWinKey;
        UseAltKey = settings.Hotkey.UseAltKey;
        HotkeyKey = settings.Hotkey.Key.ToString();
        
        // 加载后重置修改状态
        IsModified = false;
    }
    
    private void CheckIfModified()
    {
        var currentKey = HotkeyKey.Length > 0 ? HotkeyKey[0] : 'V';
        var hasChanges = _maxItemsPerGroup != _originalMaxItemsPerGroup ||
                        _useWinKey != _originalUseWinKey ||
                        _useAltKey != _originalUseAltKey ||
                        currentKey != _originalHotkeyKey;
        
        IsModified = hasChanges;
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new AppSettings
            {
                MaxItemsPerGroup = MaxItemsPerGroup,
                Hotkey = new HotkeyConfig
                {
                    UseWinKey = UseWinKey,
                    UseAltKey = UseAltKey,
                    Key = HotkeyKey.Length > 0 ? HotkeyKey[0] : 'V'
                }
            };

            await _settingsService.SaveSettingsAsync(settings);
            
            // 更新原始值
            _originalMaxItemsPerGroup = settings.MaxItemsPerGroup;
            _originalUseWinKey = settings.Hotkey.UseWinKey;
            _originalUseAltKey = settings.Hotkey.UseAltKey;
            _originalHotkeyKey = settings.Hotkey.Key;
            
            // 重置修改状态（保存按钮会变灰）
            IsModified = false;
            
            // 通知其他服务设置已更新
            OnSettingsChanged?.Invoke(this, settings);
            
            // 更新快捷键服务（Windows平台）
            // 注意：HotkeyService在App.xaml.cs中创建，需要通过App实例获取
            // 这里先保存设置，应用重启后会自动加载
            // 如果需要立即生效，可以通过事件通知App.xaml.cs更新
#if WINDOWS
            // 通知App更新快捷键配置
            if (Application.Current is App app)
            {
                app.UpdateHotkeyConfig(settings.Hotkey.UseWinKey, settings.Hotkey.UseAltKey, settings.Hotkey.Key);
            }
#endif
            
            // 保存成功后自动返回主页面
            await Shell.Current.GoToAsync("//MainPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            var page = Application.Current?.Windows.FirstOrDefault()?.Page as Page;
            if (page != null)
            {
                await page.DisplayAlert("错误", $"保存设置失败：{ex.Message}", "确定");
            }
        }
    }

    public event EventHandler<AppSettings>? OnSettingsChanged;

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


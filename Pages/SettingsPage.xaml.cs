using clipboard.ViewModels;

namespace clipboard.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        // 延迟加载，在Loaded事件中获取服务
        Loaded += OnPageLoaded;
        
        // 监听页面出现事件，重置VisualStateManager
        Appearing += OnPageAppearing;
    }
    
    private void OnPageAppearing(object? sender, EventArgs e)
    {
        // 页面出现时，重置返回按钮的VisualStateManager
        // 先立即重置一次
        if (BackButtonBorder != null)
        {
            VisualStateManager.GoToState(BackButtonBorder, "Normal");
            
            // 然后延迟再重置一次，确保VisualStateManager完全初始化
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
            {
                if (BackButtonBorder != null)
                {
                    VisualStateManager.GoToState(BackButtonBorder, "Normal");
                }
            });
        }
    }
    
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        if (BindingContext == null)
        {
            var settingsService = Handler?.MauiContext?.Services.GetService<Services.AppSettingsService>();
            if (settingsService != null)
            {
                BindingContext = new SettingsViewModel(settingsService);
            }
        }
    }
    
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    private void OnWinKeyLabelTapped(object? sender, EventArgs e)
    {
        if (BindingContext is SettingsViewModel vm)
        {
            vm.UseWinKey = !vm.UseWinKey;
            if (vm.UseWinKey)
            {
                vm.UseAltKey = false;
            }
        }
    }
    
    private void OnAltKeyLabelTapped(object? sender, EventArgs e)
    {
        if (BindingContext is SettingsViewModel vm)
        {
            vm.UseAltKey = !vm.UseAltKey;
            if (vm.UseAltKey)
            {
                vm.UseWinKey = false;
            }
        }
    }
    
    private async void OnBackClicked(object? sender, EventArgs e)
    {
        // 清除悬停状态后再导航
        if (BackButtonBorder != null)
        {
            // 强制清除悬停状态
            VisualStateManager.GoToState(BackButtonBorder, "Normal");
        }
        // 立即导航，不等待
        await Shell.Current.GoToAsync("//MainPage");
    }
}


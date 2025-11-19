using clipboard.Models;
using clipboard.Services;
using clipboard.ViewModels;

namespace clipboard
{
    public partial class MainPage : ContentPage
    {
        public MainPage(IClipboardService clipboardService)
        {
            InitializeComponent();
            BindingContext = new ClipboardViewModel(clipboardService);
            
            // 监听页面出现事件，延迟清除悬停状态
            Appearing += OnPageAppearing;
        }
        
        private void OnPageAppearing(object? sender, EventArgs e)
        {
            // 页面出现时，重置设置按钮的VisualStateManager
            // 先立即重置一次
            if (SettingsButtonBorder != null)
            {
                VisualStateManager.GoToState(SettingsButtonBorder, "Normal");
                
                // 然后延迟再重置一次，确保VisualStateManager完全初始化
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
                {
                    if (SettingsButtonBorder != null)
                    {
                        VisualStateManager.GoToState(SettingsButtonBorder, "Normal");
                    }
                });
            }
        }

        private void OnAllGroupTapped(object? sender, EventArgs e)
        {
            if (BindingContext is ClipboardViewModel viewModel)
            {
                viewModel.SelectedGroup = null;
            }
        }

        private void OnGroupTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is ClipboardGroup group && BindingContext is ClipboardViewModel viewModel)
            {
                viewModel.SelectedGroup = group;
            }
        }

        private async void OnSettingsClicked(object? sender, EventArgs e)
        {
            // 清除悬停状态后再导航
            if (SettingsButtonBorder != null)
            {
                // 强制清除悬停状态
                VisualStateManager.GoToState(SettingsButtonBorder, "Normal");
            }
            // 立即导航，不等待
            await Shell.Current.GoToAsync("//SettingsPage");
        }
    }
}

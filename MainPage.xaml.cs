using clipboard.Models;
using clipboard.Services;
using clipboard.ViewModels;
#if WINDOWS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
#endif

namespace clipboard
{
    public partial class MainPage : ContentPage
    {
        private CollectionView? _itemsCollectionView;
        
        public MainPage(IClipboardService clipboardService)
        {
            InitializeComponent();
            BindingContext = new ClipboardViewModel(clipboardService);
            
            // 监听页面出现事件，延迟清除悬停状态
            Appearing += OnPageAppearing;
            
            // 设置页面可以接收键盘事件
#if WINDOWS
            this.On<Microsoft.Maui.Controls.PlatformConfiguration.Windows>()
                .SetIsLegacyColorModeEnabled(false);
#endif
            
            // 监听键盘事件
            this.Focused += OnPageFocused;
            this.Unfocused += OnPageUnfocused;
        }
        
        private void OnPageFocused(object? sender, FocusEventArgs e)
        {
            // 页面获得焦点时，设置初始选中项
            if (BindingContext is ClipboardViewModel viewModel && viewModel.Items.Count > 0)
            {
                if (viewModel.SelectedItemIndex < 0)
                {
                    viewModel.SelectedItemIndex = 0;
                }
            }
        }
        
        private void OnPageUnfocused(object? sender, FocusEventArgs e)
        {
            // 页面失去焦点时，清除选中状态
            if (BindingContext is ClipboardViewModel viewModel)
            {
                viewModel.SelectedItemIndex = -1;
            }
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
        
#if WINDOWS
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            
            // 在 Windows 平台上，通过 WinUI 窗口处理键盘事件
            if (Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Page page)
            {
                page.KeyDown += OnPageKeyDown;
            }
        }
        
        private void OnPageKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (BindingContext is not ClipboardViewModel viewModel)
                return;
            
            // 处理方向键和 hjkl
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Up:
                case Windows.System.VirtualKey.K:
                    viewModel.NavigateUp();
                    UpdateSelectedItemVisualState(viewModel);
                    ScrollToSelectedItem(viewModel);
                    e.Handled = true;
                    break;
                    
                case Windows.System.VirtualKey.Down:
                case Windows.System.VirtualKey.J:
                    viewModel.NavigateDown();
                    UpdateSelectedItemVisualState(viewModel);
                    ScrollToSelectedItem(viewModel);
                    e.Handled = true;
                    break;
                    
                case Windows.System.VirtualKey.Enter:
                    _ = HandleEnterKeyAsync(viewModel);
                    e.Handled = true;
                    break;
                    
                case Windows.System.VirtualKey.Escape:
                    // ESC 键隐藏窗口
                    if (Microsoft.Maui.Controls.Application.Current is App app)
                    {
                        app.HideMainWindow();
                    }
                    e.Handled = true;
                    break;
            }
        }
#endif
        
        private void UpdateSelectedItemVisualState(ClipboardViewModel viewModel)
        {
            // SelectedItemIndex 属性的 setter 已经会自动触发 PropertyChanged 事件
            // 不需要手动调用 OnPropertyChanged（这是受保护的方法）
            // 视觉状态更新将在 ScrollToSelectedItem 中处理
        }
        
        private void ScrollToSelectedItem(ClipboardViewModel viewModel)
        {
            if (viewModel.SelectedItemIndex >= 0 && 
                viewModel.SelectedItemIndex < viewModel.Items.Count)
            {
                var selectedItem = viewModel.Items[viewModel.SelectedItemIndex];
                if (selectedItem != null && _itemsCollectionView != null)
                {
                    _itemsCollectionView.ScrollTo(selectedItem, position: ScrollToPosition.MakeVisible, animate: true);
                    
                    // 延迟更新视觉状态，确保滚动完成后再更新
                    Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                    {
                        UpdateItemVisualStates(viewModel);
                    });
                }
            }
        }
        
        private void UpdateItemVisualStates(ClipboardViewModel viewModel)
        {
            // 在 MAUI 中，直接访问 CollectionView 的子元素比较复杂
            // 这里我们通过触发重新渲染来实现视觉状态更新
            // 实际应用中，可能需要使用 Behavior 或 Attached Property 来实现
            // 暂时通过重新设置 SelectedItemIndex 来触发更新
            var currentIndex = viewModel.SelectedItemIndex;
            viewModel.SelectedItemIndex = -1;
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(10), () =>
            {
                viewModel.SelectedItemIndex = currentIndex;
            });
        }
        
        private async Task HandleEnterKeyAsync(ClipboardViewModel viewModel)
        {
            // 确认选择并粘贴
            var success = await viewModel.ConfirmSelectionAsync();
            if (success)
            {
                // 隐藏窗口
                if (Microsoft.Maui.Controls.Application.Current is App app)
                {
                    app.HideMainWindow();
                }
            }
        }
    }
}

using clipboard.Models;
using clipboard.Services;
using clipboard.ViewModels;
#if WINDOWS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
#endif
using clipboard.Utils;

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
            
            // 在 Loaded 事件中初始化 CollectionView 的键盘事件处理
            Loaded += OnPageLoaded;
        }
        
        private void OnPageLoaded(object? sender, EventArgs e)
        {
            // 获取 CollectionView 引用
            if (_itemsCollectionView == null)
            {
                _itemsCollectionView = this.FindByName<CollectionView>("ItemsCollectionView");
            }
            
#if WINDOWS
            // 在 CollectionView 的 Handler 准备好后，设置键盘事件处理
            if (_itemsCollectionView != null)
            {
                _itemsCollectionView.HandlerChanged += OnCollectionViewHandlerChanged;
                // 如果 Handler 已经存在，立即设置
                if (_itemsCollectionView.Handler != null)
                {
                    OnCollectionViewHandlerChanged(_itemsCollectionView, EventArgs.Empty);
                }
            }
#endif
        }
        
        private void OnPageFocused(object? sender, FocusEventArgs e)
        {
            // 页面获得焦点时，设置初始选中项
            if (BindingContext is ClipboardViewModel viewModel && viewModel.Items.Count > 0)
            {
                if (viewModel.SelectedItem == null)
                {
                    viewModel.SelectedItem = viewModel.Items[0];
                }
            }
        }
        
        private void OnPageUnfocused(object? sender, FocusEventArgs e)
        {
            // 页面失去焦点时，不清除选中状态（保持选中以便下次打开时继续）
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
            
#if WINDOWS
            // 确保 CollectionView 可以获得焦点并选中第一个项目
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () =>
            {
                if (BindingContext is ClipboardViewModel viewModel && viewModel.Items.Count > 0)
                {
                    // 选中第一个项目
                    if (viewModel.SelectedItem == null)
                    {
                        viewModel.SelectedItem = viewModel.Items[0];
                    }
                    
                    // 让 CollectionView 获得焦点
                    if (_itemsCollectionView?.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control control)
                    {
                        control.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                        DebugHelper.DebugWrite("CollectionView focused on page appearing");
                    }
                }
            });
#endif
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
            
            // 在 CollectionView 级别处理键盘事件
            if (_itemsCollectionView != null)
            {
                _itemsCollectionView.HandlerChanged += OnCollectionViewHandlerChanged;
            }
        }
        
        private void OnCollectionViewHandlerChanged(object? sender, EventArgs e)
        {
            // 在 CollectionView 的 PlatformView 上处理键盘事件
            // 只处理 jk 键，让 CollectionView 的默认处理来处理方向键
            if (_itemsCollectionView?.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ItemsControl itemsControl)
            {
                // 只订阅 PreviewKeyDown 来处理 jk 键，转换为方向键
                itemsControl.PreviewKeyDown += OnCollectionViewPreviewKeyDown;
                DebugHelper.DebugWrite("CollectionView keyboard events attached");
            }
            
            // 设置 CollectionView 获得焦点并选中第一个项目
            if (_itemsCollectionView != null && BindingContext is ClipboardViewModel viewModel)
            {
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () =>
                {
                    // 让 CollectionView 获得焦点
                    if (_itemsCollectionView?.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control control)
                    {
                        control.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                        DebugHelper.DebugWrite("CollectionView focused");
                    }
                    
                    // 选中第一个项目
                    if (viewModel.Items.Count > 0 && viewModel.SelectedItem == null)
                    {
                        viewModel.SelectedItem = viewModel.Items[0];
                        DebugHelper.DebugWrite($"Selected first item: {viewModel.Items[0].Id}");
                    }
                });
            }
        }
        
        private void OnCollectionViewPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // 只处理 jk 键，将它们转换为方向键事件
            // 让 CollectionView 的默认处理来处理方向键和 Enter/Escape
            var key = e.Key;
            var originalKey = e.OriginalKey;
            
            // 检查是否是 jk 键
            bool isJ = key == Windows.System.VirtualKey.J || originalKey == Windows.System.VirtualKey.J;
            bool isK = key == Windows.System.VirtualKey.K || originalKey == Windows.System.VirtualKey.K;
            
            if (isJ || isK)
            {
                DebugHelper.DebugWrite($"Converting {(isJ ? "J" : "K")} key to {(isJ ? "Down" : "Up")} arrow key");
                
                // 创建一个新的方向键事件
                var targetKey = isJ ? Windows.System.VirtualKey.Down : Windows.System.VirtualKey.Up;
                
                // 通过模拟方向键事件来触发 CollectionView 的默认导航
                // 由于我们不能直接创建新的 KeyRoutedEventArgs，我们手动调用导航逻辑
                if (BindingContext is ClipboardViewModel viewModel)
                {
                    if (isJ && viewModel.SelectedItemIndex < viewModel.Items.Count - 1)
                    {
                        // 向下导航
                        var nextIndex = viewModel.SelectedItemIndex + 1;
                        if (nextIndex < viewModel.Items.Count)
                        {
                            viewModel.SelectedItem = viewModel.Items[nextIndex];
                            // 滚动到选中项
                            if (_itemsCollectionView != null)
                            {
                                _itemsCollectionView.ScrollTo(viewModel.SelectedItem, position: ScrollToPosition.MakeVisible, animate: true);
                            }
                        }
                    }
                    else if (isK && viewModel.SelectedItemIndex > 0)
                    {
                        // 向上导航
                        var prevIndex = viewModel.SelectedItemIndex - 1;
                        if (prevIndex >= 0)
                        {
                            viewModel.SelectedItem = viewModel.Items[prevIndex];
                            // 滚动到选中项
                            if (_itemsCollectionView != null)
                            {
                                _itemsCollectionView.ScrollTo(viewModel.SelectedItem, position: ScrollToPosition.MakeVisible, animate: true);
                            }
                        }
                    }
                }
                
                e.Handled = true; // 阻止 jk 键的默认处理
                return;
            }
            
            // 处理 Enter 和 Escape 键
            if (key == Windows.System.VirtualKey.Enter)
            {
                if (BindingContext is ClipboardViewModel viewModel && viewModel.SelectedItem != null)
                {
                    _ = HandleEnterKeyAsync(viewModel);
                    e.Handled = true;
                }
            }
            else if (key == Windows.System.VirtualKey.Escape)
            {
                if (Microsoft.Maui.Controls.Application.Current is App app)
                {
                    app.HideMainWindow();
                    e.Handled = true;
                }
            }
            
            // 其他键（包括方向键）让 CollectionView 的默认处理来处理
        }
#endif

        
        private async Task HandleEnterKeyAsync(ClipboardViewModel viewModel)
        {
            // 确认选择并粘贴
            if (viewModel.SelectedItem != null)
            {
                var success = await viewModel.CopyItemAsync(viewModel.SelectedItem.Id);
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
}

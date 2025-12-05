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
            
#if WINDOWS
            // 确保页面可以获得焦点以接收键盘事件
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                if (Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Page page)
                {
                    page.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
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
            
            // 在 Windows 平台上，通过 WinUI 窗口处理键盘事件
            // 尝试多种方式获取键盘事件
            if (Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Page page)
            {
                page.KeyDown += OnPageKeyDown;
                // 确保页面可以获得焦点
                page.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
            
            // 也尝试在窗口级别处理
            if (this.Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window)
            {
                window.Content.KeyDown += OnWindowContentKeyDown;
            }
            
            // 在 CollectionView 级别处理键盘事件，拦截方向键和 jk 键
            // 这样可以确保我们的代码优先处理这些按键
            if (_itemsCollectionView != null)
            {
                _itemsCollectionView.HandlerChanged += OnCollectionViewHandlerChanged;
            }
        }
        
        private void OnCollectionViewHandlerChanged(object? sender, EventArgs e)
        {
            // 在 CollectionView 的 PlatformView 上处理键盘事件
            if (_itemsCollectionView?.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ItemsControl itemsControl)
            {
                itemsControl.KeyDown += OnCollectionViewKeyDown;
                itemsControl.PreviewKeyDown += OnCollectionViewPreviewKeyDown;
                // 也监听字符事件，用于捕获 jk 键
                itemsControl.CharacterReceived += OnCollectionViewCharacterReceived;
                DebugHelper.DebugWrite("CollectionView keyboard events attached");
            }
        }
        
        private void OnCollectionViewCharacterReceived(object sender, Microsoft.UI.Xaml.Input.CharacterReceivedRoutedEventArgs e)
        {
            // 处理字符事件，用于捕获 jk 键（无论大小写）
            var character = (char)e.Character;
            DebugHelper.DebugWrite($"CharacterReceived: character='{character}' ({(int)character})");
            
            if (BindingContext is not ClipboardViewModel viewModel)
                return;
            
            // 检查是否是 j 或 k（不区分大小写）
            if (character == 'j' || character == 'J')
            {
                DebugHelper.DebugWrite("Character 'j' detected, navigating DOWN");
                e.Handled = true;
                var oldIndex = viewModel.SelectedItemIndex;
                viewModel.NavigateDown();
                var newIndex = viewModel.SelectedItemIndex;
                DebugHelper.DebugWrite($"NavigateDown: {oldIndex} -> {newIndex}");
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateSelectedItemVisualState(viewModel);
                    ScrollToSelectedItem(viewModel);
                });
            }
            else if (character == 'k' || character == 'K')
            {
                DebugHelper.DebugWrite("Character 'k' detected, navigating UP");
                e.Handled = true;
                var oldIndex = viewModel.SelectedItemIndex;
                viewModel.NavigateUp();
                var newIndex = viewModel.SelectedItemIndex;
                DebugHelper.DebugWrite($"NavigateUp: {oldIndex} -> {newIndex}");
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateSelectedItemVisualState(viewModel);
                    ScrollToSelectedItem(viewModel);
                });
            }
        }
        
        private void OnCollectionViewPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // PreviewKeyDown 在 KeyDown 之前触发，可以拦截事件
            // 这样可以阻止 CollectionView 的默认方向键处理
            DebugHelper.DebugWrite($"CollectionView PreviewKeyDown: Key={e.Key}, OriginalKey={e.OriginalKey}, Handled={e.Handled}");
            
            // 先处理事件
            bool handled = HandleKeyEvent(e);
            
            if (handled)
            {
                // 如果事件已被处理，确保阻止继续传递
                e.Handled = true;
                DebugHelper.DebugWrite("Key event handled in PreviewKeyDown, preventing default behavior");
            }
        }
        
        private void OnCollectionViewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // 如果 PreviewKeyDown 已经处理了事件，这里就不需要再处理了
            if (!e.Handled)
            {
                HandleKeyEvent(e);
            }
            else
            {
                DebugHelper.DebugWrite("KeyDown event already handled in PreviewKeyDown, skipping");
            }
        }
        
        private void OnWindowContentKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            HandleKeyEvent(e);
        }
        
        private void OnPageKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            HandleKeyEvent(e);
        }
        
        private bool HandleKeyEvent(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (BindingContext is not ClipboardViewModel viewModel)
                return false;
            
            // 添加调试信息
            DebugHelper.DebugWrite($"KeyDown event: Key={e.Key}, OriginalKey={e.OriginalKey}");
            
            // 处理方向键和 hjkl
            // 注意：VirtualKey.J 和 VirtualKey.K 对应大写和小写字母
            var key = e.Key;
            var originalKey = e.OriginalKey;
            
            // 检查是否是方向键或 hjkl
            // 在 WinUI 中，VirtualKey.J 和 VirtualKey.K 对应字母 J 和 K（大小写都相同）
            bool isUp = key == Windows.System.VirtualKey.Up || 
                       key == Windows.System.VirtualKey.K ||
                       originalKey == Windows.System.VirtualKey.K;
            bool isDown = key == Windows.System.VirtualKey.Down || 
                         key == Windows.System.VirtualKey.J ||
                         originalKey == Windows.System.VirtualKey.J;
            
            DebugHelper.DebugWrite($"isUp={isUp}, isDown={isDown}, key={key}, originalKey={originalKey}, Items.Count={viewModel.Items.Count}, SelectedItemIndex={viewModel.SelectedItemIndex}");
            
            if (isUp)
            {
                DebugHelper.DebugWrite("Navigating UP - calling NavigateUp()");
                var oldIndex = viewModel.SelectedItemIndex;
                viewModel.NavigateUp();
                var newIndex = viewModel.SelectedItemIndex;
                DebugHelper.DebugWrite($"NavigateUp completed: {oldIndex} -> {newIndex}");
                
                // 确保在主线程上更新 UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateSelectedItemVisualState(viewModel);
                    ScrollToSelectedItem(viewModel);
                });
                
                e.Handled = true;
                return true;
            }
            
            if (isDown)
            {
                DebugHelper.DebugWrite("Navigating DOWN - calling NavigateDown()");
                var oldIndex = viewModel.SelectedItemIndex;
                viewModel.NavigateDown();
                var newIndex = viewModel.SelectedItemIndex;
                DebugHelper.DebugWrite($"NavigateDown completed: {oldIndex} -> {newIndex}");
                
                // 确保在主线程上更新 UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateSelectedItemVisualState(viewModel);
                    ScrollToSelectedItem(viewModel);
                });
                
                e.Handled = true;
                return true;
            }
            
            // 处理其他键
            switch (key)
            {
                case Windows.System.VirtualKey.Enter:
                    _ = HandleEnterKeyAsync(viewModel);
                    e.Handled = true;
                    return true;
                    
                case Windows.System.VirtualKey.Escape:
                    // ESC 键隐藏窗口
                    if (Microsoft.Maui.Controls.Application.Current is App app)
                    {
                        app.HideMainWindow();
                    }
                    e.Handled = true;
                    return true;
            }
            
            return false;
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
            // 视觉状态现在通过 IsSelected 属性自动更新，不需要手动处理
            // 这个方法保留用于未来可能的扩展
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

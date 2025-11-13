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
    }
}

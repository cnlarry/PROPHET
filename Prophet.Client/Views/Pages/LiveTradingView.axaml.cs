using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Prophet.Client.ViewModels;
using Prophet.Client.Core;

namespace Prophet.Client.Views.Pages;

public partial class LiveTradingView : UserControl
{
    public LiveTradingView()
    {
        InitializeComponent();
        
        // 设置 DataContext
        DataContext = ServiceContainer.GetService<LiveTradingViewModel>();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}


using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Prophet.Client.Views.ContextMenus;

public partial class LiveTradingContextMenu : UserControl
{
    public LiveTradingContextMenu()
    {
        InitializeComponent();
    }
    
    private void OnMenuItemClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border iconBorder) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        
        // 将点击事件传递给 MainWindow 处理
        var mainWindow = this.FindLogicalAncestorOfType<MainWindow>();
        if (mainWindow != null)
        {
            var method = mainWindow.GetType().GetMethod("SideIcon_OnPointerPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(mainWindow, new object[] { iconBorder, e });
            }
        }
    }
}


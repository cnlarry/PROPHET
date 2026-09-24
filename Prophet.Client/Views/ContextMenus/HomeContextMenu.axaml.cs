using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Prophet.Client.Views.ContextMenus;

public partial class HomeContextMenu : UserControl
{
    public HomeContextMenu()
    {
        InitializeComponent();
    }
    
    private void OnMenuItemClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border iconBorder) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        
        // 将点击事件传递给 MainWindow 处理
        // 通过查找父窗口并调用其方法
        var mainWindow = this.FindLogicalAncestorOfType<MainWindow>();
        if (mainWindow != null)
        {
            // 使用反射或直接调用 MainWindow 的公共方法
            // 这里我们通过名称查找 MainWindow 中的 SideIcon_OnPointerPressed 方法
            var method = mainWindow.GetType().GetMethod("SideIcon_OnPointerPressed", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(mainWindow, new object[] { iconBorder, e });
            }
        }
    }
}


using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using System;
using System.Threading.Tasks;
using Prophet.Client.ViewModels;
using Prophet.Client.Core;
using Prophet.Client.Views.Pages;
using Prophet.Client.Views.Dialogs;

namespace Prophet.Client.Views;

public partial class MainWindow : Window
{
    private bool _isTradingActive;
    
    // 当前选中的菜单和视图
    private Border? _currentSelectedMenu;
    private Grid? _currentSelectedView;
    
    // 当前选中的侧边图标
    private Border? _currentSelectedSideIcon;
    private string? _currentSidePanelType;

    public MainWindow()
    {
        InitializeComponent();

        _isTradingActive = true;
        UpdateTradingStatus(true);
        
        // 初始化菜单选中状态（默认首页）
        _currentSelectedMenu = this.FindControl<Border>("MenuHome");
        _currentSelectedView = this.FindControl<Grid>("ViewHome");
        
        // 初始化回测视图的DataContext
        InitializeBacktestView();
        
        // 确保首页默认上下文的副窗口处于关闭状态
        var infoPanel = this.FindControl<Border>("InfoPanel");
        if (infoPanel != null)
        {
            infoPanel.IsVisible = false;
        }
    }
    
    private void InitializeBacktestView()
    {
        try
        {
            var backtestView = this.FindControl<Grid>("ViewBacktest")
                ?.FindDescendantOfType<BacktestView>();
            
            if (backtestView != null)
            {
                backtestView.DataContext = ServiceContainer.GetService<BacktestViewModel>();
            }
            else
            {
                Console.WriteLine("❌ [MainWindow] 未找到 BacktestView 控件");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MainWindow] 初始化回测视图失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
        }
    }

    private void ToggleTradingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _isTradingActive = !_isTradingActive;
        UpdateTradingStatus(_isTradingActive);
    }

    
    private void SideIcon_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border iconBorder) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        
        var iconName = iconBorder.Name;
        if (string.IsNullOrEmpty(iconName) || !iconName.StartsWith("Icon")) return;
        
        var panelType = iconName.Substring(4); // "IconConfig" -> "Config"
        var infoPanel = this.FindControl<Border>("InfoPanel");
        
        if (infoPanel == null) return;
        
        // 如果点击的是当前已选中的图标，则关闭面板
        if (_currentSelectedSideIcon == iconBorder && infoPanel.IsVisible)
        {
            infoPanel.IsVisible = false;
            iconBorder.Classes.Remove("Active");
            _currentSelectedSideIcon = null;
            _currentSidePanelType = null;
            return;
        }
        
        // 取消之前图标的选中状态
        if (_currentSelectedSideIcon != null)
        {
            _currentSelectedSideIcon.Classes.Remove("Active");
        }
        
        // 设置新图标为选中状态
        iconBorder.Classes.Add("Active");
        _currentSelectedSideIcon = iconBorder;
        _currentSidePanelType = panelType;
        
        // 显示面板并加载对应内容
        infoPanel.IsVisible = true;
        LoadSidePanelContentByType(panelType);
    }
    
    private async void MenuItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border menuItem) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        
        // 获取对应的视图名称
        var menuName = menuItem.Name;
        if (string.IsNullOrEmpty(menuName) || !menuName.StartsWith("Menu")) return;
        
        // 如果点击的是设置菜单，打开设置对话框
        if (menuName == "MenuSettings")
        {
            var settingsDialog = new SettingsDialog();
            await settingsDialog.ShowDialog(this);
            return;
        }
        
        // 如果点击的是AI菜单，打开AI会话弹窗
        if (menuName == "MenuAi")
        {
            await OpenAiChatDialog();
            return;
        }
        
        var viewName = "View" + menuName.Substring(4); // "MenuHome" -> "ViewHome"
        var targetView = this.FindControl<Grid>(viewName);
        
        if (targetView == null)
        {
            return;
        }
        
        // 如果点击的是当前菜单，不做处理
        if (_currentSelectedMenu == menuItem)
        {
            return;
        }
        
        // 取消之前菜单的选中状态
        if (_currentSelectedMenu != null)
        {
            _currentSelectedMenu.Classes.Remove("Selected");
        }
        
        // 隐藏之前的视图
        if (_currentSelectedView != null)
        {
            _currentSelectedView.IsVisible = false;
        }
        
        // 设置新菜单的选中状态（样式会自动处理颜色）
        menuItem.Classes.Add("Selected");
        
        // 显示新视图
        targetView.IsVisible = true;
        
        // 更新当前状态
        _currentSelectedMenu = menuItem;
        _currentSelectedView = targetView;
        
        // 动态切换上下文菜单
        SwitchContextMenu(viewName);
        
        // 检查当前是否显示AI策略助手面板
        var infoPanel = this.FindControl<Border>("InfoPanel");
        var sidePanelTitle = this.FindControl<TextBlock>("SidePanelTitle");
        
        // 如果切换到非策略模块，且当前显示的是AI策略助手，则关闭面板
        if (viewName != "ViewStrategy" && infoPanel != null && sidePanelTitle != null &&
            infoPanel.IsVisible && sidePanelTitle.Text == "AI策略助手")
        {
            CloseInfoPanel();
        }
        // 如果副窗口已打开且不是AI助手，更新内容
        else if (infoPanel != null && infoPanel.IsVisible && !string.IsNullOrEmpty(_currentSidePanelType))
        {
            LoadSidePanelContentByType(_currentSidePanelType);
        }
    }
    
    /// <summary>
    /// 打开AI会话弹窗
    /// </summary>
    private async Task OpenAiChatDialog()
    {
        try
        {
            var aiChatDialog = new AiChatDialog();
            
            // 如果当前是策略视图，传递策略视图引用
            var strategyView = this.FindControl<StrategyView>("ViewStrategy")?.FindDescendantOfType<StrategyView>();
            if (strategyView != null)
            {
                // 使用新的SetSessionContext方法
                aiChatDialog.SetSessionContext(
                    sessionTitle: "AI助手",
                    strategyView: strategyView
                );
            }
            
            // 如果有回测ViewModel，传递引用
            // TODO: 获取BacktestViewModel
            
            await aiChatDialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 打开AI会话弹窗失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 根据当前视图切换上下文菜单
    /// </summary>
    private void SwitchContextMenu(string viewName)
    {
        var homeContextMenu = this.FindControl<UserControl>("HomeContextMenuControl");
        var strategyContextMenu = this.FindControl<UserControl>("StrategyContextMenuControl");
        var liveTradingContextMenu = this.FindControl<UserControl>("LiveTradingContextMenuControl");
        
        if (homeContextMenu == null || strategyContextMenu == null) return;
        
        // 隐藏所有上下文菜单
        homeContextMenu.IsVisible = false;
        strategyContextMenu.IsVisible = false;
        if (liveTradingContextMenu != null)
        {
            liveTradingContextMenu.IsVisible = false;
        }
        
        // 根据当前视图显示对应的上下文菜单
        switch (viewName)
        {
            case "ViewHome":
                homeContextMenu.IsVisible = true;
                break;
            case "ViewStrategy":
                strategyContextMenu.IsVisible = true;
                break;
            case "ViewTrade":
                // 实盘交易显示专用上下文菜单
                if (liveTradingContextMenu != null)
                {
                    liveTradingContextMenu.IsVisible = true;
                }
                else
                {
                    homeContextMenu.IsVisible = true;
                }
                break;
            case "ViewMarket":
            case "ViewBacktest":
            case "ViewMarketplace":
            case "ViewData":
                // 其他页面暂时显示首页菜单，后续可以创建各自的菜单
                homeContextMenu.IsVisible = true;
                break;
            default:
                homeContextMenu.IsVisible = true;
                break;
        }
    }
    
    private void LoadSidePanelContentByType(string panelType)
    {
        var sidePanelContent = this.FindControl<ContentControl>("SidePanelContent");
        var sidePanelTitle = this.FindControl<TextBlock>("SidePanelTitle");
        
        if (sidePanelContent == null) return;
        
        // 根据当前视图判断是否显示策略内容
        bool isStrategyView = _currentSelectedView?.Name == "ViewStrategy";
        
        switch (panelType)
        {
            case "Config":
                if (sidePanelTitle != null) sidePanelTitle.Text = "配置";
                if (isStrategyView)
                {
                    var strategyPanel = new StrategySidePanel();
                    sidePanelContent.Content = strategyPanel;
                }
                else
                {
                    sidePanelContent.Content = CreatePlaceholderContent("配置");
                }
                break;
                
            case "Log":
                if (sidePanelTitle != null) sidePanelTitle.Text = "日志";
                if (isStrategyView)
                {
                    var strategyPanel = new StrategySidePanel();
                    strategyPanel.SwitchToTab("Log");
                    sidePanelContent.Content = strategyPanel;
                }
                else
                {
                    sidePanelContent.Content = CreatePlaceholderContent("日志");
                }
                break;
                
            case "Performance":
                if (sidePanelTitle != null) sidePanelTitle.Text = "性能";
                if (isStrategyView)
                {
                    var strategyPanel = new StrategySidePanel();
                    strategyPanel.SwitchToTab("Performance");
                    sidePanelContent.Content = strategyPanel;
                }
                else
                {
                    sidePanelContent.Content = CreatePlaceholderContent("性能");
                }
                break;
                
            case "Notification":
                if (sidePanelTitle != null) sidePanelTitle.Text = "通知";
                if (isStrategyView)
                {
                    var strategyPanel = new StrategySidePanel();
                    strategyPanel.SwitchToTab("Notification");
                    sidePanelContent.Content = strategyPanel;
                }
                else
                {
                    sidePanelContent.Content = CreateNotificationContent();
                }
                break;
        }
    }
    
    private Control CreatePlaceholderContent(string title)
    {
        return new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(16),
            Children =
            {
                new Border
                {
                    Classes = { "Card" },
                    Child = new TextBlock
                    {
                        Text = $"{title}功能开发中...",
                        Classes = { "Body" },
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20)
                    }
                }
            }
        };
    }
    
    private Control CreateNotificationContent()
    {
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 16,
                Margin = new Thickness(16),
                Children =
                {
                    new Border
                    {
                        Classes = { "Card" },
                        Child = new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                new TextBlock { Text = "系统通知", Classes = { "Body" }, FontWeight = FontWeight.SemiBold },
                                new TextBlock { Text = "暂无新通知", Classes = { "Caption" } }
                            }
                        }
                    }
                }
            }
        };
    }
    


    private void UpdateTradingStatus(bool isActive)
    {
        if (this.FindControl<TextBlock>("TradingStatusText") is { } statusText)
        {
            statusText.Text = isActive ? "运行中" : "空闲";
            statusText.Foreground = isActive
                ? new SolidColorBrush(Colors.OrangeRed)
                : new SolidColorBrush(Colors.LightGray);
        }

        if (this.FindControl<TextBlock>("TradingStatusDescription") is { } statusDesc)
        {
            statusDesc.Text = isActive ? "实盘执行中" : "未在执行实盘";
        }

        if (this.FindControl<Border>("InfoPanel") is { } infoPanel)
        {
            infoPanel.IsVisible = isActive;
        }

        if (this.FindControl<Path>("TradingStatusIcon") is { } icon)
        {
            icon.Fill = new SolidColorBrush(isActive ? Colors.LimeGreen : Colors.Gray);
        }
    }

    private void ShowAlert(string title, string message)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var dialog = Controls.ModernDialogPresets.Warning(title, message);
            await dialog.ShowDialog(this);
        });
    }
    
    /// <summary>
    /// 切换信息面板的显示状态
    /// </summary>
    /// <param name="title">面板标题</param>
    /// <param name="content">面板内容控件</param>
    public void ToggleInfoPanel(string title, Control content)
    {
        // 如果是AI策略助手，只在策略模块显示
        if (title == "AI策略助手")
        {
            bool isStrategyView = _currentSelectedView?.Name == "ViewStrategy";
            if (!isStrategyView)
            {
                // 如果不在策略模块，关闭面板（如果已打开）并返回
                var infoPanel = this.FindControl<Border>("InfoPanel");
                var sidePanelTitle = this.FindControl<TextBlock>("SidePanelTitle");
                if (infoPanel != null && sidePanelTitle != null && 
                    infoPanel.IsVisible && sidePanelTitle.Text == title)
                {
                    CloseInfoPanel();
                }
                return;
            }
        }
        
        var infoPanel2 = this.FindControl<Border>("InfoPanel");
        var sidePanelTitle2 = this.FindControl<TextBlock>("SidePanelTitle");
        var sidePanelContent = this.FindControl<ContentControl>("SidePanelContent");
        
        if (infoPanel2 == null || sidePanelTitle2 == null || sidePanelContent == null)
        {
            return;
        }
        
        // 如果当前面板已显示且是同一个面板，则隐藏
        if (infoPanel2.IsVisible && sidePanelTitle2.Text == title)
        {
            CloseInfoPanel();
        }
        else
        {
            // 显示面板并设置内容
            sidePanelTitle2.Text = title;
            sidePanelContent.Content = content;
            infoPanel2.IsVisible = true;
            
            // 清除侧边图标的激活状态（因为这是从其他地方触发的）
            if (_currentSelectedSideIcon != null)
            {
                _currentSelectedSideIcon.Classes.Remove("Active");
                _currentSelectedSideIcon = null;
                _currentSidePanelType = null;
            }
        }
    }
    
    /// <summary>
    /// 关闭信息面板
    /// </summary>
    public void CloseInfoPanel()
    {
        var infoPanel = this.FindControl<Border>("InfoPanel");
        if (infoPanel != null)
        {
            infoPanel.IsVisible = false;
        }
        
        // 清除侧边图标的激活状态
        if (_currentSelectedSideIcon != null)
        {
            _currentSelectedSideIcon.Classes.Remove("Active");
            _currentSelectedSideIcon = null;
            _currentSidePanelType = null;
        }
    }
    
    /// <summary>
    /// 检查信息面板是否可见且显示指定标题的内容
    /// </summary>
    /// <param name="title">面板标题</param>
    /// <returns>如果面板可见且显示指定标题则返回true</returns>
    public bool IsInfoPanelVisible(string title)
    {
        var infoPanel = this.FindControl<Border>("InfoPanel");
        var sidePanelTitle = this.FindControl<TextBlock>("SidePanelTitle");
        
        if (infoPanel == null || sidePanelTitle == null)
        {
            return false;
        }
        
        return infoPanel.IsVisible && sidePanelTitle.Text == title;
    }

}

// 扩展方法
public static class ControlExtensions
{
    public static T? FindDescendantOfType<T>(this Control control) where T : Control
    {
        if (control is T result)
        {
            return result;
        }

        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    var found = FindDescendantOfType<T>(childControl);
                    if (found != null)
                        return found;
                }
            }
        }
        else if (control is Decorator decorator && decorator.Child is Control childControl)
        {
            return FindDescendantOfType<T>(childControl);
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control content)
        {
            return FindDescendantOfType<T>(content);
        }

        return null;
    }
}


using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.Generic;

namespace Prophet.Client.Views;

public partial class StrategySidePanel : UserControl
{
    private Border? _currentSelectedTab;
    private StackPanel? _currentSelectedContent;

    public StrategySidePanel()
    {
        InitializeComponent();
        
        // 初始化选中状态
        _currentSelectedTab = this.FindControl<Border>("TabConfig");
        _currentSelectedContent = this.FindControl<StackPanel>("ContentConfig");
    }
    
    public void SwitchToTab(string tabName)
    {
        var targetTab = this.FindControl<Border>($"Tab{tabName}");
        var targetContent = this.FindControl<StackPanel>($"Content{tabName}");
        
        if (targetTab == null || targetContent == null) return;
        
        // 取消之前Tab的选中状态
        if (_currentSelectedTab != null)
        {
            _currentSelectedTab.Classes.Remove("Selected");
        }
        
        // 隐藏之前的内容
        if (_currentSelectedContent != null)
        {
            _currentSelectedContent.IsVisible = false;
        }
        
        // 设置新Tab为选中状态
        targetTab.Classes.Add("Selected");
        targetContent.IsVisible = true;
        
        // 更新当前状态
        _currentSelectedTab = targetTab;
        _currentSelectedContent = targetContent;
    }
    
    public void SwitchToNotificationTab()
    {
        SwitchToTab("Notification");
    }

    private void Tab_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border tabBorder) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        
        // 如果点击的是当前Tab，不做处理
        if (_currentSelectedTab == tabBorder) return;
        
        // 获取对应的内容名称
        var tabName = tabBorder.Name;
        if (string.IsNullOrEmpty(tabName) || !tabName.StartsWith("Tab")) return;
        
        var contentName = "Content" + tabName.Substring(3); // "TabConfig" -> "ContentConfig"
        var targetContent = this.FindControl<StackPanel>(contentName);
        
        if (targetContent == null) return;
        
        // 取消之前Tab的选中状态
        if (_currentSelectedTab != null)
        {
            _currentSelectedTab.Classes.Remove("Selected");
        }
        
        // 隐藏之前的内容
        if (_currentSelectedContent != null)
        {
            _currentSelectedContent.IsVisible = false;
        }
        
        // 设置新Tab的选中状态
        tabBorder.Classes.Add("Selected");
        
        // 显示新内容
        targetContent.IsVisible = true;
        
        // 更新当前状态
        _currentSelectedTab = tabBorder;
        _currentSelectedContent = targetContent;
    }
}


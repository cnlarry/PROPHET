using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Prophet.Client.Models;

namespace Prophet.Client.Views.Dialogs;

public partial class StrategyAnomalyDialog : UserControl
{
    private readonly StrategyInfo _strategy;
    public bool ShouldFix { get; private set; }
    
    public StrategyAnomalyDialog()
    {
        _strategy = null!;
        InitializeComponent();
    }
    
    public StrategyAnomalyDialog(StrategyInfo strategy)
    {
        _strategy = strategy;
        InitializeComponent();
        
        // 设置策略名称
        if (this.FindControl<TextBlock>("StrategyNameText") is { } nameText)
        {
            nameText.Text = $"策略：{strategy.Name}";
        }
        
        // 设置异常原因
        if (this.FindControl<TextBlock>("AnomalyReasonText") is { } reasonText)
        {
            reasonText.Text = strategy.AnomalyReason ?? "未知异常";
        }
        
        // 设置检测时间
        if (this.FindControl<TextBlock>("DetectionTimeText") is { } timeText)
        {
            timeText.Text = strategy.StatusChangedAt ?? "未知";
        }
        
        // 设置影响的实例数（占位数据，实际应从API获取）
        var affectedInstanceCount = 0; // TODO: 从API获取实际的受影响实例数
        
        if (affectedInstanceCount > 0)
        {
            if (this.FindControl<TextBlock>("InstanceCountText") is { } countText)
            {
                countText.Text = $"{affectedInstanceCount} 个实例已被暂停";
            }
        }
        else
        {
            if (this.FindControl<StackPanel>("InstanceImpactPanel") is { } panel)
            {
                panel.IsVisible = false;
            }
        }
        
        // 设置策略信息
        if (this.FindControl<TextBlock>("SymbolText") is { } symbolText)
        {
            // 策略不绑定标的；这里显示空
            symbolText.Text = "";
        }
        
        if (this.FindControl<TextBlock>("VersionText") is { } versionText)
        {
            versionText.Text = strategy.Version;
        }
        
        if (this.FindControl<TextBlock>("SubscriberText") is { } subscriberText)
        {
            subscriberText.Text = strategy.TotalSubscribers > 0
                ? $"{strategy.TotalSubscribers} 人"
                : "暂无订阅";
        }
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        ShouldFix = false;
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close();
    }
    
    private void OnFixClick(object? sender, RoutedEventArgs e)
    {
        ShouldFix = true;
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close();
    }
}


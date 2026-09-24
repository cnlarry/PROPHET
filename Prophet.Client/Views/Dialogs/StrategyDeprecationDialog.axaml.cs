using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Prophet.Client.Models;

namespace Prophet.Client.Views.Dialogs;

public partial class StrategyDeprecationDialog : UserControl
{
    private readonly StrategyInfo _strategy;
    
    public string Reason => ReasonTextBox?.Text ?? string.Empty;
    
    public StrategyDeprecationDialog()
    {
        _strategy = null!;
        InitializeComponent();
    }
    
    public StrategyDeprecationDialog(StrategyInfo strategy)
    {
        _strategy = strategy;
        InitializeComponent();
        
        // 设置策略名称
        if (this.FindControl<TextBlock>("StrategyNameText") is { } nameText)
        {
            nameText.Text = $"策略：{strategy.Name}";
        }
        
        // 设置订阅者数量
        if (this.FindControl<TextBlock>("SubscriberCountText") is { } countText)
        {
            countText.Text = strategy.TotalSubscribers > 0
                ? $"当前有 {strategy.TotalSubscribers} 人订阅此策略"
                : "当前没有用户订阅此策略";
        }
        
        // 如果没有订阅者，隐藏警告
        if (strategy.TotalSubscribers == 0)
        {
            if (this.FindControl<Border>("SubscriberWarning") is { } warning)
            {
                warning.IsVisible = false;
            }
        }
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close(false);
    }
    
    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        // 验证原因不为空
        if (string.IsNullOrWhiteSpace(Reason))
        {
            if (this.FindControl<TextBox>("ReasonTextBox") is { } textBox)
            {
                textBox.Focus();
            }
            return;
        }
        
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close(true);
    }
}


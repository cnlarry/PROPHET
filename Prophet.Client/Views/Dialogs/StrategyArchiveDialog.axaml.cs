using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Prophet.Client.Models;

namespace Prophet.Client.Views.Dialogs;

public partial class StrategyArchiveDialog : UserControl
{
    private readonly StrategyInfo _strategy;
    private readonly ComboBox? _reasonComboBox;
    
    public string Reason
    {
        get
        {
            if (_reasonComboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "其他原因";
            }
            return "其他原因";
        }
    }
    
    public StrategyArchiveDialog()
    {
        _strategy = null!;
        InitializeComponent();
    }
    
    public StrategyArchiveDialog(StrategyInfo strategy)
    {
        _strategy = strategy;
        InitializeComponent();
        
        _reasonComboBox = this.FindControl<ComboBox>("ReasonComboBox");
        
        // 设置策略名称
        if (this.FindControl<TextBlock>("StrategyNameText") is { } nameText)
        {
            nameText.Text = $"策略：{strategy.Name}";
        }
        
        // 设置运行实例数量警告（这里先用占位数据，实际应从API获取）
        var runningInstanceCount = 0; // TODO: 从API获取实际的运行实例数
        
        if (runningInstanceCount > 0)
        {
            if (this.FindControl<Border>("InstanceWarning") is { } warning)
            {
                warning.IsVisible = true;
            }
            
            if (this.FindControl<TextBlock>("InstanceCountText") is { } countText)
            {
                countText.Text = $"当前有 {runningInstanceCount} 个实例正在运行，将被强制停止";
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
        // 确认归档原因已选择
        if (_reasonComboBox?.SelectedIndex < 0)
        {
            _reasonComboBox?.Focus();
            return;
        }
        
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close(true);
    }
}


using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Prophet.Client.Controls;

/// <summary>
/// ModernDialog 预设对话框集合
/// 提供常用的对话框类型：确认、警告、输入等
/// </summary>
public static class ModernDialogPresets
{
    /// <summary>
    /// 创建确认对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="callback">回调函数，参数为用户选择（true=确定，false=取消）</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog Confirm(string title, string message, Action<bool>? callback = null)
    {
        var dialog = ModernDialogBuilder.Create()
            .WithTitle(title)
            .WithSize(400, 200)
            .AddText(message)
            .Build();
        
        dialog.AddButton("取消", () =>
        {
            callback?.Invoke(false);
            dialog.Close(false);
        });
        
        dialog.AddButton("确定", () =>
        {
            callback?.Invoke(true);
            dialog.Close(true);
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建警告对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="onClose">关闭回调</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog Warning(string title, string message, Action? onClose = null)
    {
        var dialog = ModernDialogBuilder.Create()
            .WithTitle(title)
            .WithSize(400, 180)
            .Build();
        
        // 添加警告图标和标题
        var titleText = new TextBlock
        {
            Text = $"⚠️ {title}",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#F59E0B"))
        };
        
        dialog.AddContent(titleText);
        
        // 添加消息
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
        };
        
        dialog.AddContent(messageText);
        
        dialog.AddButton("确定", () =>
        {
            onClose?.Invoke();
            dialog.Close();
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建错误对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">错误消息</param>
    /// <param name="onClose">关闭回调</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog Error(string title, string message, Action? onClose = null)
    {
        var dialog = ModernDialogBuilder.Create()
            .WithTitle(title)
            .WithSize(400, 180)
            .Build();
        
        // 添加错误图标和标题
        var titleText = new TextBlock
        {
            Text = $"❌ {title}",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#EF4444"))
        };
        
        dialog.AddContent(titleText);
        
        // 添加消息
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
        };
        
        dialog.AddContent(messageText);
        
        dialog.AddButton("确定", () =>
        {
            onClose?.Invoke();
            dialog.Close();
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建信息对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="onClose">关闭回调</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog Info(string title, string message, Action? onClose = null)
    {
        var dialog = ModernDialogBuilder.Create()
            .WithTitle(title)
            .WithSize(400, 180)
            .AddText(message)
            .Build();
        
        dialog.AddButton("确定", () =>
        {
            onClose?.Invoke();
            dialog.Close();
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建输入对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="label">输入框标签/占位符</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="callback">回调函数，参数为输入的文本（null表示取消）</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog Input(string title, string label, string defaultValue = "", Action<string?>? callback = null)
    {
        TextBox? inputBox = null;
        
        var dialog = ModernDialogBuilder.Create()
            .WithTitle(title)
            .WithSize(400, 200)
            .AddTextBox(out inputBox, label, defaultValue)
            .Build();
        
        var textBox = inputBox!;
        
        // 选中默认文本
        textBox.SelectionStart = 0;
        textBox.SelectionEnd = defaultValue.Length;
        
        // 聚焦到文本框
        textBox.AttachedToVisualTree += (s, e) => textBox.Focus();
        
        // 回车确认
        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                callback?.Invoke(textBox.Text?.Trim());
                dialog.Close(true);
            }
        };
        
        dialog.AddButton("取消", () =>
        {
            callback?.Invoke(null);
            dialog.Close(false);
        });
        
        dialog.AddButton("确定", () =>
        {
            callback?.Invoke(textBox.Text?.Trim());
            dialog.Close(true);
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建策略表单对话框
    /// </summary>
    /// <param name="callback">回调函数，参数为(策略名称, 交易对, 备注)，任一为null表示取消</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog StrategyForm(Action<string?, string?, string?>? callback = null)
    {
        TextBox? nameTextBox = null;
        ComboBox? symbolComboBox = null;
        TextBox? remarksTextBox = null;
        
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "ADAUSDT" };
        
        var dialog = ModernDialogBuilder.Create()
            .WithTitle("新建策略")
            .WithSize(520, 580)
            .AddLabel("策略名称：")
            .AddTextBox(out nameTextBox, "输入策略名称，例如：趋势追踪策略")
            .AddLabel("交易对：", margin: new Thickness(0, 16, 0, 6))
            .AddComboBox(out symbolComboBox, symbols)
            .AddLabel("备注（可选）：", margin: new Thickness(0, 16, 0, 6))
            .AddMultilineTextBox(out remarksTextBox, "简要描述策略用途...", height: 60)
            .AddTip("💡 提示：\n\n• 点击「保存」将保存策略到您的账户")
            .Build();
        
        var nameTb = nameTextBox!;
        var symbolCb = symbolComboBox!;
        var remarksTb = remarksTextBox!;
        
        // 聚焦到策略名称输入框
        nameTb.AttachedToVisualTree += (s, e) => nameTb.Focus();
        
        dialog.AddButton("取消", () =>
        {
            callback?.Invoke(null, null, null);
            dialog.Close(false);
        });
        
        dialog.AddButton("下一步", () =>
        {
            var name = nameTb.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                // TODO: 显示错误提示
                return;
            }
            
            var symbol = symbolCb.SelectedItem?.ToString() ?? "BTCUSDT";
            var remarks = remarksTb.Text?.Trim();
            
            callback?.Invoke(name, symbol, remarks);
            dialog.Close(true);
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建编辑策略信息对话框
    /// </summary>
    /// <param name="strategyName">策略名称</param>
    /// <param name="currentSymbol">当前交易对</param>
    /// <param name="currentRemarks">当前备注</param>
    /// <param name="callback">回调函数，参数为(交易对, 备注)，任一为null表示取消</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog EditStrategyInfo(string strategyName, string currentSymbol, string? currentRemarks, Action<string?, string?>? callback = null)
    {
        ComboBox? symbolComboBox = null;
        TextBox? remarksTextBox = null;
        
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "ADAUSDT" };
        var selectedIndex = Array.IndexOf(symbols, currentSymbol);
        if (selectedIndex == -1) selectedIndex = 0;
        
        var dialog = ModernDialogBuilder.Create()
            .WithTitle($"编辑策略信息 - {strategyName}")
            .WithSize(520, 480)
            .AddLabel("交易对：")
            .Build();
        
        // 手动创建并添加 ComboBox（因为需要设置 selectedIndex）
        var comboBox = new ComboBox
        {
            SelectedIndex = selectedIndex,
            Background = new SolidColorBrush(Color.Parse("#3C3C3C")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 13,
            ItemsSource = symbols
        };
        
        dialog.AddContent(comboBox);
        symbolComboBox = comboBox;
        
        // 添加备注标签和文本框
        var remarksLabel = new TextBlock
        {
            Text = "备注（可选）：",
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            FontSize = 13,
            Margin = new Thickness(0, 16, 0, 6)
        };
        dialog.AddContent(remarksLabel);
        
        var remarksBox = new TextBox
        {
            Text = currentRemarks ?? string.Empty,
            Watermark = "简要描述策略用途...",
            Height = 80,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Background = new SolidColorBrush(Color.Parse("#3C3C3C")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 13
        };
        dialog.AddContent(remarksBox);
        remarksTextBox = remarksBox;
        
        // 添加提示
        var tipBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 16, 0, 0),
            Child = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                Text = "💡 提示：\n\n• 只有草稿状态的策略才能编辑信息\n• 修改后将立即保存"
            }
        };
        dialog.AddContent(tipBorder);
        
        var symbolCb = symbolComboBox!;
        var remarksTb = remarksTextBox!;
        
        // 聚焦到交易对下拉框
        symbolCb.AttachedToVisualTree += (s, e) => symbolCb.Focus();
        
        dialog.AddButton("取消", () =>
        {
            callback?.Invoke(null, null);
            dialog.Close(false);
        });
        
        dialog.AddButton("保存", () =>
        {
            var symbol = symbolCb.SelectedItem?.ToString() ?? "BTCUSDT";
            var remarks = remarksTb.Text?.Trim();
            
            callback?.Invoke(symbol, remarks);
            dialog.Close(true);
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建激活策略对话框
    /// </summary>
    /// <param name="strategyName">策略名称</param>
    /// <param name="callback">回调函数，参数为是否公开（true=公开，false=私有，null=取消）</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog ActivateStrategy(string strategyName, Action<bool?>? callback = null)
    {
        var dialog = ModernDialogBuilder.Create()
            .WithTitle($"激活策略 - {strategyName}")
            .WithSize(450, 280)
            .AddText("激活策略后，该策略将可以用于实盘交易。请选择激活方式：")
            .Build();
        
        // 添加单选按钮组
        var radioPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 16, 0, 0) };
        
        var privateRadio = new RadioButton
        {
            Content = "私有激活（仅自己可用）",
            IsChecked = true,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            FontSize = 13
        };
        
        var publicRadio = new RadioButton
        {
            Content = "公开激活（其他用户可订阅）",
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            FontSize = 13
        };
        
        radioPanel.Children.Add(privateRadio);
        radioPanel.Children.Add(publicRadio);
        dialog.AddContent(radioPanel);
        
        // 添加提示
        var tipBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 16, 0, 0),
            Child = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                Text = "💡 提示：\n\n• 私有激活：只有您可以使用此策略\n• 公开激活：其他用户可以订阅和使用此策略"
            }
        };
        dialog.AddContent(tipBorder);
        
        dialog.AddButton("取消", () =>
        {
            callback?.Invoke(null);
            dialog.Close(false);
        });
        
        dialog.AddButton("激活", () =>
        {
            var makePublic = publicRadio.IsChecked == true;
            callback?.Invoke(makePublic);
            dialog.Close(true);
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建共享策略确认对话框
    /// </summary>
    /// <param name="strategyName">策略名称</param>
    /// <param name="callback">回调函数，参数为是否确认（true=确认，false=取消）</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog ShareStrategy(string strategyName, Action<bool>? callback = null)
    {
        var dialog = ModernDialogBuilder.Create()
            .WithTitle($"共享策略 - {strategyName}")
            .WithSize(450, 260)
            .Build();
        
        // 添加说明文本
        var infoText = new TextBlock
        {
            Text = "确认要将此策略设为公开共享吗？",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            Margin = new Thickness(0, 0, 0, 12)
        };
        dialog.AddContent(infoText);
        
        var descText = new TextBlock
        {
            Text = "共享后，其他用户将可以订阅和使用此策略。",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#A0A0A0")),
            Margin = new Thickness(0, 0, 0, 16)
        };
        dialog.AddContent(descText);
        
        // 添加提示
        var tipBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16),
            Child = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                Text = "💡 提示：\n\n• 共享后可随时取消\n• 订阅用户将使用锁定的策略版本"
            }
        };
        dialog.AddContent(tipBorder);
        
        dialog.AddButton("取消", () =>
        {
            callback?.Invoke(false);
            dialog.Close(false);
        });
        
        dialog.AddButton("确认共享", () =>
        {
            callback?.Invoke(true);
            dialog.Close(true);
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建取消共享策略确认对话框
    /// </summary>
    /// <param name="strategyName">策略名称</param>
    /// <param name="subscriberCount">订阅者数量</param>
    /// <param name="callback">回调函数，参数为是否确认（true=确认，false=取消）</param>
    /// <returns>配置好的对话框</returns>
    public static ModernDialog UnshareStrategy(string strategyName, int subscriberCount, Action<bool>? callback = null)
    {
        var dialog = ModernDialogBuilder.Create()
            .WithTitle($"取消共享 - {strategyName}")
            .WithSize(480, subscriberCount > 0 ? 300 : 260)
            .Build();
        
        // 添加说明文本
        var infoText = new TextBlock
        {
            Text = "确认要将此策略设为私有吗？",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            Margin = new Thickness(0, 0, 0, 12)
        };
        dialog.AddContent(infoText);
        
        if (subscriberCount > 0)
        {
            var warningPanel = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2D2416")),
                BorderBrush = new SolidColorBrush(Color.Parse("#F59E0B")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 16),
                Child = new TextBlock
                {
                    Text = $"⚠️ 当前有 {subscriberCount} 个用户订阅了此策略",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.Parse("#F59E0B"))
                }
            };
            dialog.AddContent(warningPanel);
        }
        
        // 添加提示
        var tipBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16),
            Child = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                Text = subscriberCount > 0
                    ? "💡 提示：\n\n• 取消共享后新用户将无法订阅\n• 已订阅用户仍可继续使用"
                    : "💡 提示：\n\n• 取消共享后，其他用户将无法订阅此策略"
            }
        };
        dialog.AddContent(tipBorder);
        
        dialog.AddButton("取消", () =>
        {
            callback?.Invoke(false);
            dialog.Close(false);
        });
        
        dialog.AddButton("确认取消共享", () =>
        {
            callback?.Invoke(true);
            dialog.Close(true);
        }, isPrimary: true);
        
        return dialog;
    }
    
    /// <summary>
    /// 创建包装自定义控件的对话框
    /// </summary>
    /// <param name="content">自定义控件</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>配置好的窗口</returns>
    public static Window CustomContent(Control content, double width = 600, double height = 500)
    {
        var rootPanel = new Panel
        {
            Background = Brushes.Transparent,
            ClipToBounds = false
        };
        
        var outerContainer = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(15),
            ClipToBounds = false
        };
        
        // 创建阴影层
        var shadowLayer1 = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 0)
        };
        
        var shadowLayer2 = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(1, 1, -1, -1)
        };
        
        var shadowLayer3 = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0)),
            CornerRadius = new CornerRadius(8.5),
            Margin = new Thickness(1.5, 1.5, -1.5, -1.5)
        };
        
        var mainBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            Margin = new Thickness(-3, -3, 3, 3),
            ClipToBounds = true,
            Child = content
        };
        
        var layeredContainer = new Grid();
        layeredContainer.Children.Add(shadowLayer1);
        layeredContainer.Children.Add(shadowLayer2);
        layeredContainer.Children.Add(shadowLayer3);
        layeredContainer.Children.Add(mainBorder);
        
        outerContainer.Child = layeredContainer;
        rootPanel.Children.Add(outerContainer);
        
        var window = new Window
        {
            Content = rootPanel,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            Background = Brushes.Transparent
        };
        
        // 事件处理
        window.Opened += (s, e) =>
        {
            window.Background = Brushes.Transparent;
        };
        
        window.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(Window.Background) && window.Background != Brushes.Transparent)
            {
                window.Background = Brushes.Transparent;
            }
        };
        
        window.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                window.Close();
            }
        };
        
        return window;
    }
}


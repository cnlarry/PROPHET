using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Prophet.Client.Controls;
using Prophet.Client.Models;
using Prophet.Client.Services.Strategy;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.RiskControl;

namespace Prophet.Client.Views.Dialogs;

public class CreateTradingInstanceDialog : ModernDialog
{
    private int _currentStep = 1;
    private const int TotalSteps = 3;
    
    // 控件引用 - 步骤1：基本信息
    private TextBox? _instanceNameTextBox;
    private ComboBox? _strategyComboBox;
    private ComboBox? _strategyVersionComboBox;
    private ComboBox? _exchangeComboBox;
    private CheckBox? _useTestnetCheckBox;
    private ComboBox? _symbolComboBox;
    private ComboBox? _timeframeComboBox;
    
    // 控件引用 - 步骤2：资金与交易参数
    private TextBox? _initialCapitalTextBox;
    private TextBox? _positionSizePercentTextBox;
    private ComboBox? _leverageComboBox;
    private ComboBox? _marginModeComboBox;
    private TextBox? _maxStopLossPercentTextBox;
    private TextBox? _minTakeProfitPercentTextBox;
    
    // 控件引用 - 步骤3：风控参数
    private TextBox? _maxDailyTradesTextBox;
    private TextBox? _maxPositionsTextBox;
    private TextBox? _maxDrawdownTextBox;
    private TextBox? _maxConsecutiveLossesTextBox;
    
    // 数据
    private readonly LocalStrategyService _strategyService = new();
    private List<StrategyInfo> _strategies = new();
    private List<VersionListItem> _strategyVersions = new();
    private StrategyInfo? _selectedStrategy;
    
    // 步骤内容面板
    private StackPanel? _step1Content;
    private StackPanel? _step2Content;
    private StackPanel? _step3Content;
    
    // 步骤指示器
    private Border? _step1Indicator;
    private Border? _step2Indicator;
    private Border? _step3Indicator;
    private TextBlock? _step1Text;
    private TextBlock? _step2Text;
    private TextBlock? _step3Text;
    
    // 按钮
    private Button? _backButton;
    private Button? _nextButton;
    private Button? _createButton;
    
    // 警告面板
    private Border? _warningPanel;
    private TextBlock? _warningText;
    
    // 主容器
    private StackPanel? _mainContainer;

    public CreateTradingInstanceDialog()
    {
        Title = "创建实盘交易实例";
        Width = 700;
        Height = 650;
        CanResize = false;
        
        BuildUI();
        _ = LoadStrategiesAsync();
        UpdateStepUI();
    }
    
    /// <summary>
    /// 构建UI
    /// </summary>
    private void BuildUI()
    {
        _mainContainer = new StackPanel { Spacing = 0 };
        
        // 添加步骤指示器
        _mainContainer.Children.Add(CreateStepIndicator());
        
        // 添加内容容器
        var contentContainer = new Border
        {
            Margin = new Thickness(0, 24, 0, 0)
        };
        
        var contentStack = new StackPanel { Spacing = 0 };
        
        // 创建三个步骤的内容
        _step1Content = CreateStep1Content();
        _step2Content = CreateStep2Content();
        _step3Content = CreateStep3Content();
        
        contentStack.Children.Add(_step1Content);
        contentStack.Children.Add(_step2Content);
        contentStack.Children.Add(_step3Content);
        
        contentContainer.Child = contentStack;
        _mainContainer.Children.Add(contentContainer);
        
        // 添加警告面板
        _mainContainer.Children.Add(CreateWarningPanel());
        
        SetContent(_mainContainer);
        
        // 添加底部按钮
        AddButtons();
    }
    
    /// <summary>
    /// 创建步骤指示器
    /// </summary>
    private StackPanel CreateStepIndicator()
    {
        var indicatorStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0)
        };
        
        // 步骤1
        _step1Indicator = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F0B90B")),
            CornerRadius = new CornerRadius(12),
            Width = 24,
            Height = 24,
            Child = new TextBlock
            {
                Text = "1",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#1E2329")),
                FontWeight = FontWeight.Bold
            }
        };
        indicatorStack.Children.Add(_step1Indicator);
        
        _step1Text = new TextBlock
        {
            Text = "基本信息",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#F0B90B")),
            FontWeight = FontWeight.SemiBold
        };
        indicatorStack.Children.Add(_step1Text);
        
        // 箭头
        indicatorStack.Children.Add(new Path
        {
            Data = Avalonia.Media.Geometry.Parse("M 0,0 L 8,5 L 0,10"),
            Stroke = new SolidColorBrush(Color.Parse("#8B8D91")),
            StrokeThickness = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        });
        
        // 步骤2
        _step2Indicator = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2B3139")),
            CornerRadius = new CornerRadius(12),
            Width = 24,
            Height = 24,
            Child = new TextBlock
            {
                Text = "2",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91"))
            }
        };
        indicatorStack.Children.Add(_step2Indicator);
        
        _step2Text = new TextBlock
        {
            Text = "资金与交易",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#8B8D91"))
        };
        indicatorStack.Children.Add(_step2Text);
        
        // 箭头
        indicatorStack.Children.Add(new Path
        {
            Data = Avalonia.Media.Geometry.Parse("M 0,0 L 8,5 L 0,10"),
            Stroke = new SolidColorBrush(Color.Parse("#8B8D91")),
            StrokeThickness = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        });
        
        // 步骤3
        _step3Indicator = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2B3139")),
            CornerRadius = new CornerRadius(12),
            Width = 24,
            Height = 24,
            Child = new TextBlock
            {
                Text = "3",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91"))
            }
        };
        indicatorStack.Children.Add(_step3Indicator);
        
        _step3Text = new TextBlock
        {
            Text = "风控参数",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#8B8D91"))
        };
        indicatorStack.Children.Add(_step3Text);
        
        return indicatorStack;
    }
    
    /// <summary>
    /// 创建步骤1的内容：基本信息
    /// </summary>
    private StackPanel CreateStep1Content()
    {
        var stack = new StackPanel { Spacing = 10 };
        
        // 实例名称
        _instanceNameTextBox = CreateTextBox("留空则自动生成", "");
        stack.Children.Add(CreateFormRow("实例名称：", _instanceNameTextBox));
        
        // 策略选择
        _strategyComboBox = CreateComboBox(new[] { "（请选择策略）" }, 0);
        _strategyComboBox.SelectionChanged += OnStrategySelectionChanged;
        stack.Children.Add(CreateFormRow("策略：", _strategyComboBox));
        
        // 策略版本
        _strategyVersionComboBox = CreateComboBox(new[] { "（请先选择策略）" }, 0);
        _strategyVersionComboBox.IsEnabled = false;
        stack.Children.Add(CreateFormRow("策略版本：", _strategyVersionComboBox));
        
        // 交易所
        _exchangeComboBox = CreateComboBox(new[]
        {
            "Binance (币安)",
            "OKX（暂未支持）",
            "Bybit（暂未支持）"
        }, 0);
        stack.Children.Add(CreateFormRow("交易所：", _exchangeComboBox));
        
        // 模拟交易
        _useTestnetCheckBox = new CheckBox
        {
            Content = "使用模拟交易（测试网）",
            IsChecked = true, // 默认勾选，更安全
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            FontSize = 13
        };
        stack.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            Margin = new Thickness(0, 8),
            Children =
            {
                new TextBlock
                {
                    Text = "模拟交易：",
                    Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new StackPanel
                {
                    [Grid.ColumnProperty] = 1,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Children = { _useTestnetCheckBox }
                }
            }
        });
        
        // 交易对
        _symbolComboBox = CreateComboBox(new[]
        {
            "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "XRPUSDT", "ADAUSDT",
            "DOGEUSDT", "MATICUSDT", "DOTUSDT", "LINKUSDT"
        }, 0);
        stack.Children.Add(CreateFormRow("交易对：", _symbolComboBox));
        
        // 时间周期
        _timeframeComboBox = CreateComboBox(new[]
        {
            "1m (1分钟)", "3m (3分钟)", "5m (5分钟)", "15m (15分钟)",
            "30m (30分钟)", "1h (1小时)", "4h (4小时)", "1d (1天)"
        }, 2); // 默认选择5m
        stack.Children.Add(CreateFormRow("时间周期：", _timeframeComboBox));
        
        return stack;
    }
    
    /// <summary>
    /// 创建步骤2的内容：资金与交易参数
    /// </summary>
    private StackPanel CreateStep2Content()
    {
        var stack = new StackPanel { Spacing = 16, IsVisible = false };
        
        // 资金配置卡片
        _initialCapitalTextBox = CreateTextBox("例如：10000", "10000");
        _positionSizePercentTextBox = CreateTextBox("例如：10 (表示10%)", "10");
        
        stack.Children.Add(CreateCard("资金配置", new Control[]
        {
            CreateFormRow("初始资金（USDT）：", _initialCapitalTextBox),
            CreateFormRow("仓位比例（%）：", _positionSizePercentTextBox),
            new TextBlock
            {
                Text = "提示：仓位比例基于初始资金，实际开仓价值 = 初始资金 × 仓位比例 × 杠杆倍率",
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
                FontSize = 11,
                Margin = new Thickness(200, -8, 0, 0),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        }));
        
        // 交易参数卡片
        _leverageComboBox = CreateComboBox(new[]
        {
            "1倍", "2倍", "3倍", "5倍", "10倍", "20倍", "25倍", "50倍", "75倍", "100倍", "125倍"
        }, 4); // 默认选择10倍
        
        _marginModeComboBox = CreateComboBox(new[]
        {
            "逐仓 (Isolated)",
            "全仓 (Cross)"
        }, 0); // 默认选择逐仓
        
        stack.Children.Add(CreateCard("交易参数", new Control[]
        {
            CreateFormRow("杠杆倍率：", _leverageComboBox),
            CreateFormRow("仓位模式：", _marginModeComboBox),
            new TextBlock
            {
                Text = "提示：逐仓模式更安全，每个仓位独立保证金；全仓模式资金利用率更高但风险更大",
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
                FontSize = 11,
                Margin = new Thickness(200, -8, 0, 0),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        }));
        
        // 止损止盈配置卡片
        _maxStopLossPercentTextBox = CreateTextBox("例如：20 (表示保证金的20%)", "20");
        _minTakeProfitPercentTextBox = CreateTextBox("例如：30 (表示保证金的30%)", "30");
        
        stack.Children.Add(CreateCard("止损止盈配置", new Control[]
        {
            CreateFormRow("最大止损比例（%）：", _maxStopLossPercentTextBox),
            CreateFormRow("最小止盈比例（%）：", _minTakeProfitPercentTextBox),
            new TextBlock
            {
                Text = "提示：止损止盈比例基于保证金计算，实际价格 = 开仓价 ± (开仓价 / 杠杆 × 比例)",
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
                FontSize = 11,
                Margin = new Thickness(200, -8, 0, 0),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        }));
        
        return stack;
    }
    
    /// <summary>
    /// 创建步骤3的内容：风控参数
    /// </summary>
    private StackPanel CreateStep3Content()
    {
        var stack = new StackPanel { Spacing = 16, IsVisible = false };
        
        // 交易频率限制
        _maxDailyTradesTextBox = CreateTextBox("例如：10", "10");
        
        stack.Children.Add(CreateCard("交易频率限制", new Control[]
        {
            CreateFormRow("日均最大交易次数：", _maxDailyTradesTextBox),
            new TextBlock
            {
                Text = "提示：限制每日最大交易次数，防止过度交易",
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
                FontSize = 11,
                Margin = new Thickness(200, -8, 0, 0)
            }
        }));
        
        // 持仓限制
        _maxPositionsTextBox = CreateTextBox("例如：3", "3");
        
        stack.Children.Add(CreateCard("持仓限制", new Control[]
        {
            CreateFormRow("最大同时持仓数：", _maxPositionsTextBox),
            new TextBlock
            {
                Text = "提示：限制同时持有的最大仓位数量",
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
                FontSize = 11,
                Margin = new Thickness(200, -8, 0, 0)
            }
        }));
        
        // 回撤限制
        _maxDrawdownTextBox = CreateTextBox("例如：15 (表示15%)", "15");
        
        stack.Children.Add(CreateCard("回撤限制", new Control[]
        {
            CreateFormRow("最大回撤限制（%）：", _maxDrawdownTextBox),
            new TextBlock
            {
                Text = "提示：当账户总权益回撤超过此比例时，将触发紧急停止",
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
                FontSize = 11,
                Margin = new Thickness(200, -8, 0, 0),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        }));
        
        // 熔断机制
        _maxConsecutiveLossesTextBox = CreateTextBox("例如：5", "5");
        
        stack.Children.Add(CreateCard("熔断机制", new Control[]
        {
            CreateFormRow("连续亏损次数：", _maxConsecutiveLossesTextBox),
            new TextBlock
            {
                Text = "提示：连续亏损达到此次数后，实例将自动暂停，需要手动恢复",
                Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
                FontSize = 11,
                Margin = new Thickness(200, -8, 0, 0),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        }));
        
        return stack;
    }
    
    /// <summary>
    /// 创建卡片容器
    /// </summary>
    private Border CreateCard(string title, Control[] children)
    {
        var stack = new StackPanel { Spacing = 12 };
        
        // 标题
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            Margin = new Thickness(0, 0, 0, 4)
        });
        
        // 添加子控件
        foreach (var child in children)
        {
            stack.Children.Add(child);
        }
        
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E2329")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16),
            Child = stack
        };
    }
    
    /// <summary>
    /// 创建表单行
    /// </summary>
    private Grid CreateFormRow(string label, Control control)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            Margin = new Thickness(0, 8)
        };
        
        // 标签 - 第一列，固定宽度250
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.Parse("#EAECEF")), // 使用 DarkText1 颜色
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });
        
        // 输入控件 - 第二列，自动填充剩余空间，左对齐
        control.SetValue(Grid.ColumnProperty, 1);
        control.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(control);
        
        return grid;
    }
    
    /// <summary>
    /// 创建文本框
    /// </summary>
    private TextBox CreateTextBox(string watermark, string? defaultText = null)
    {
        return new TextBox
        {
            Watermark = watermark,
            Text = defaultText,
            Background = new SolidColorBrush(Color.Parse("#2B3139")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 13,
            MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }
    
    /// <summary>
    /// 创建下拉框
    /// </summary>
    private ComboBox CreateComboBox(string[] items, int selectedIndex)
    {
        var comboBox = new ComboBox
        {
            Background = new SolidColorBrush(Color.Parse("#2B3139")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 13,
            MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        
        foreach (var item in items)
        {
            comboBox.Items.Add(new ComboBoxItem { Content = item });
        }
        
        comboBox.SelectedIndex = selectedIndex;
        
        return comboBox;
    }
    
    /// <summary>
    /// 创建警告面板
    /// </summary>
    private Border CreateWarningPanel()
    {
        _warningText = new TextBlock
        {
            Text = "请填写所有必填项",
            Foreground = new SolidColorBrush(Color.Parse("#F59E0B")),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        
        var warningStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        
        warningStack.Children.Add(new Path
        {
            Data = Avalonia.Media.Geometry.Parse("M509.824 874.624c-51.3536 0-94.336-21.76-112.9984-53.632h225.9712c-18.6624 31.8464-61.6448 53.632-112.9728 53.632zM507.5968 142.464c53.2992 0 98.3296 33.7152 110.0288 80.128 97.3312 47.4624 153.088 142.976 153.088 263.936l-0.0768 22.3232c-0.3072 34.432-1.9712 72.3968-11.0592 104.2688 13.5424 8.704 26.6496 20.8128 37.5552 34.944 17.5872 22.4 27.264 46.4384 27.264 67.6352v2.0224c0 60.7744-66.6368 88.448-128.6144 88.448H319.4112c-30.8992 0-59.776-6.8608-81.3824-19.3792-27.648-16.1536-42.8032-41.0112-42.8032-70.0672v-2.048c0-36.3264 26.6496-79.36 60.5696-101.5552-11.3152-40.3712-11.3152-92.8768-11.3152-132.6592 0-118.912 54.3232-209.9968 153.088-257.8432 11.6992-46.4384 56.704-80.1536 110.0288-80.1536z m0 64c-22.7584 0-41.1136 12.416-46.976 28.544l-1.024 3.2512-7.296 28.928-26.8544 13.0304c-76.2368 36.9408-116.9664 105.4464-116.9664 200.2432l0.1024 24.576c0.128 14.08 0.4352 24.32 1.0496 35.072l0.256 4.0704c1.1776 18.5344 3.2512 34.1248 6.2208 46.5664l1.3056 5.12 12.6464 45.1328-39.1936 25.6768c-16.0768 10.496-29.5936 31.744-31.4368 45.0816l-0.2048 2.944v2.0224c0 5.632 2.56 9.8048 10.88 14.6688 9.984 5.8112 25.216 9.8048 42.6496 10.624l6.656 0.1536h376.3712c21.632 0 42.0608-4.864 54.8352-12.4416 6.9888-4.1472 9.216-6.8096 9.7024-10.3936l0.0768-1.6128v-2.0224c0-5.5808-4.3008-16.256-13.9264-28.544-5.12-6.656-11.0592-12.544-16.9984-17.0496l-4.4544-3.1232-40.0128-25.6768 13.056-45.7216c3.328-11.6992 5.6064-26.24 6.9632-43.8528l0.512-7.7312c0.9472-15.7696 1.152-29.0816 1.152-57.472 0-94.08-39.8336-165.504-110.3872-202.9824l-6.7072-3.4048-26.752-13.056-7.2704-28.8256c-4.4544-17.664-23.7056-31.7952-47.9744-31.7952z M558.7712 337.3824a25.6 25.6 0 0 1 36.1984-0.3584 317.39154 317.39154 0 0 0 277.128161 473.102372 311.462624 311.462624 0 0 0 148.01553-36.963563 38.65571 38.65571 0 0 1 37.959396 0.931833 39.730902 39.730902 0 0 1 18.032504 33.202951c-3.793892 133.679637-58.820679 257.877106-154.66892 351.741362a505.852206 505.852206 0 0 1-356.843403 145.040832c-282.788279 0-512.456957-229.422919-512.456957-511.95264C0.01536 230.439232 228.718925 0.975353 510.577931 0z"),
            Fill = new SolidColorBrush(Color.Parse("#F59E0B")),
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform
        });
        
        warningStack.Children.Add(_warningText);
        
        _warningPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(51, 245, 158, 11)), // #33F59E0B
            BorderBrush = new SolidColorBrush(Color.Parse("#F59E0B")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8),
            IsVisible = false,
            Margin = new Thickness(0, 12, 0, 0),
            Child = warningStack
        };
        
        return _warningPanel;
    }
    
    /// <summary>
    /// 异步加载策略列表
    /// </summary>
    private async Task LoadStrategiesAsync()
    {
        if (_strategyComboBox == null) return;
        
        try
        {
            _strategies = await _strategyService.GetMyStrategiesAsync();
            
            // 只显示已激活状态的策略，只有激活的策略才能进行实盘交易
            var activeStrategies = _strategies
                .Where(s => s.Status == "active")
                .OrderBy(s => s.Name)
                .ToList();
            
            _strategyComboBox.Items.Clear();
            _strategyComboBox.Items.Add(new ComboBoxItem { Content = "（请选择策略）" });
            
            foreach (var strategy in activeStrategies)
            {
                _strategyComboBox.Items.Add(new ComboBoxItem 
                { 
                    Content = $"{strategy.Name}",
                    Tag = strategy
                });
            }
            
            _strategyComboBox.SelectedIndex = 0;
            
            Console.WriteLine($"[CreateTradingInstanceDialog] 已加载 {activeStrategies.Count} 个激活策略");
            
            if (activeStrategies.Count == 0)
            {
                Console.WriteLine("[CreateTradingInstanceDialog] 警告：没有已激活的策略，请先激活策略");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateTradingInstanceDialog] 加载策略失败: {ex.Message}");
            ShowWarning($"加载策略失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 策略选择变化事件
    /// </summary>
    private async void OnStrategySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_strategyComboBox == null || _strategyVersionComboBox == null) return;
        
        if (_strategyComboBox.SelectedIndex <= 0)
        {
            _selectedStrategy = null;
            _strategyVersionComboBox.Items.Clear();
            _strategyVersionComboBox.Items.Add(new ComboBoxItem { Content = "（请先选择策略）" });
            _strategyVersionComboBox.SelectedIndex = 0;
            _strategyVersionComboBox.IsEnabled = false;
            return;
        }
        
        var selectedItem = _strategyComboBox.SelectedItem as ComboBoxItem;
        if (selectedItem?.Tag is StrategyInfo strategy)
        {
            _selectedStrategy = strategy;
            await LoadStrategyVersionsAsync(strategy.Id);
        }
    }
    
    /// <summary>
    /// 加载策略版本列表
    /// </summary>
    private async Task LoadStrategyVersionsAsync(string strategyId)
    {
        if (_strategyVersionComboBox == null) return;
        
        try
        {
            _strategyVersions = await _strategyService.GetStrategyVersionsAsync(strategyId);
            
            // 只显示激活状态的版本
            var activeVersions = _strategyVersions
                .Where(v => v.Status == "active")
                .OrderByDescending(v => v.VersionString)
                .ToList();
            
            _strategyVersionComboBox.Items.Clear();
            
            if (activeVersions.Count == 0)
            {
                _strategyVersionComboBox.Items.Add(new ComboBoxItem { Content = "（无可用版本）" });
                _strategyVersionComboBox.SelectedIndex = 0;
                _strategyVersionComboBox.IsEnabled = false;
                ShowWarning("该策略没有激活的版本，请先激活策略版本");
                return;
            }
            
            foreach (var version in activeVersions)
            {
                var displayText = $"{version.VersionString}";
                if (!string.IsNullOrEmpty(version.ChangeDescription))
                {
                    displayText += $" - {version.ChangeDescription}";
                }
                
                _strategyVersionComboBox.Items.Add(new ComboBoxItem
                {
                    Content = displayText,
                    Tag = version
                });
            }
            
            _strategyVersionComboBox.SelectedIndex = 0; // 默认选择第一个（最新版本）
            _strategyVersionComboBox.IsEnabled = true;
            
            Console.WriteLine($"[CreateTradingInstanceDialog] 已加载 {activeVersions.Count} 个策略版本");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateTradingInstanceDialog] 加载策略版本失败: {ex.Message}");
            ShowWarning($"加载策略版本失败: {ex.Message}");
            
            _strategyVersionComboBox.Items.Clear();
            _strategyVersionComboBox.Items.Add(new ComboBoxItem { Content = "（加载失败）" });
            _strategyVersionComboBox.SelectedIndex = 0;
            _strategyVersionComboBox.IsEnabled = false;
        }
    }
    
    
    /// <summary>
    /// 添加按钮（按正确顺序添加，避免反射访问私有字段）
    /// </summary>
    private void AddButtons()
    {
        // 按从左到右的顺序添加按钮
        
        // 取消按钮
        AddButton("取消", OnCancel, isPrimary: false);
        
        // 上一步按钮
        _backButton = AddButton("上一步", OnBack, isPrimary: false);
        _backButton.IsVisible = false;
        
        // 下一步按钮
        _nextButton = AddButton("下一步", OnNext, isPrimary: true);
        
        // 创建按钮
        _createButton = AddButton("创建实例", OnCreate, isPrimary: true);
        _createButton.IsVisible = false;
    }
    
    /// <summary>
    /// 更新步骤UI
    /// </summary>
    private void UpdateStepUI()
    {
        // 更新内容可见性
        if (_step1Content != null) _step1Content.IsVisible = _currentStep == 1;
        if (_step2Content != null) _step2Content.IsVisible = _currentStep == 2;
        if (_step3Content != null) _step3Content.IsVisible = _currentStep == 3;
        
        // 更新步骤指示器
        UpdateStepIndicator(_step1Indicator, _step1Text, _currentStep >= 1, _currentStep == 1);
        UpdateStepIndicator(_step2Indicator, _step2Text, _currentStep >= 2, _currentStep == 2);
        UpdateStepIndicator(_step3Indicator, _step3Text, _currentStep >= 3, _currentStep == 3);
        
        // 更新按钮可见性
        if (_backButton != null) _backButton.IsVisible = _currentStep > 1;
        if (_nextButton != null) _nextButton.IsVisible = _currentStep < TotalSteps;
        if (_createButton != null) _createButton.IsVisible = _currentStep == TotalSteps;
        
        // 隐藏警告
        if (_warningPanel != null) _warningPanel.IsVisible = false;
    }
    
    /// <summary>
    /// 更新步骤指示器样式
    /// </summary>
    private void UpdateStepIndicator(Border? indicator, TextBlock? text, bool completed, bool active)
    {
        if (indicator == null || text == null) return;
        
        if (active)
        {
            indicator.Background = new SolidColorBrush(Color.Parse("#F0B90B"));
            text.Foreground = new SolidColorBrush(Color.Parse("#F0B90B"));
            text.FontWeight = FontWeight.SemiBold;
            
            // 更新指示器内的文字颜色
            if (indicator.Child is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(Color.Parse("#1E2329"));
                tb.FontWeight = FontWeight.Bold;
            }
        }
        else if (completed)
        {
            indicator.Background = new SolidColorBrush(Color.Parse("#F0B90B"));
            text.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            text.FontWeight = FontWeight.Normal;
            
            if (indicator.Child is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(Color.Parse("#1E2329"));
                tb.FontWeight = FontWeight.Normal;
            }
        }
        else
        {
            indicator.Background = new SolidColorBrush(Color.Parse("#2B3139"));
            text.Foreground = new SolidColorBrush(Color.Parse("#8B8D91"));
            text.FontWeight = FontWeight.Normal;
            
            if (indicator.Child is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(Color.Parse("#8B8D91"));
                tb.FontWeight = FontWeight.Normal;
            }
        }
    }
    
    /// <summary>
    /// 验证当前步骤
    /// </summary>
    private bool ValidateCurrentStep()
    {
        switch (_currentStep)
        {
            case 1:
                // 实例名称可选，会自动生成
                
                // 验证策略
                if (_strategyComboBox?.SelectedIndex <= 0)
                {
                    ShowWarning("请选择策略");
                    return false;
                }
                
                // 验证策略版本
                if (_strategyVersionComboBox?.SelectedIndex < 0 || _strategyVersionComboBox?.IsEnabled == false)
                {
                    ShowWarning("请选择策略版本");
                    return false;
                }
                
                // 验证交易所
                if (_exchangeComboBox?.SelectedIndex < 0)
                {
                    ShowWarning("请选择交易所");
                    return false;
                }
                
                // 验证交易对
                if (_symbolComboBox?.SelectedIndex < 0)
                {
                    ShowWarning("请选择交易对");
                    return false;
                }
                
                // 验证时间周期
                if (_timeframeComboBox?.SelectedIndex < 0)
                {
                    ShowWarning("请选择时间周期");
                    return false;
                }
                break;
                
            case 2:
                // 验证初始资金
                if (!decimal.TryParse(_initialCapitalTextBox?.Text, out decimal initialCapital) || initialCapital <= 0)
                {
                    ShowWarning("请输入有效的初始资金（> 0）");
                    return false;
                }
                
                // 验证仓位比例
                if (!decimal.TryParse(_positionSizePercentTextBox?.Text, out decimal positionSize) || positionSize <= 0 || positionSize > 100)
                {
                    ShowWarning("请输入有效的仓位比例（1-100%）");
                    return false;
                }
                
                // 验证杠杆倍率
                if (_leverageComboBox?.SelectedIndex < 0)
                {
                    ShowWarning("请选择杠杆倍率");
                    return false;
                }
                
                // 验证仓位模式
                if (_marginModeComboBox?.SelectedIndex < 0)
                {
                    ShowWarning("请选择仓位模式");
                    return false;
                }
                
                // 验证最大止损比例
                if (!decimal.TryParse(_maxStopLossPercentTextBox?.Text, out decimal maxStopLoss) || maxStopLoss <= 0 || maxStopLoss > 100)
                {
                    ShowWarning("请输入有效的最大止损比例（1-100%）");
                    return false;
                }
                
                // 验证最小止盈比例
                if (!decimal.TryParse(_minTakeProfitPercentTextBox?.Text, out decimal minTakeProfit) || minTakeProfit <= 0 || minTakeProfit > 100)
                {
                    ShowWarning("请输入有效的最小止盈比例（1-100%）");
                    return false;
                }
                
                // 验证止盈比例应该大于止损比例
                if (minTakeProfit <= maxStopLoss)
                {
                    ShowWarning("最小止盈比例应该大于最大止损比例");
                    return false;
                }
                break;
                
            case 3:
                // 验证交易频率
                if (!int.TryParse(_maxDailyTradesTextBox?.Text, out int maxDailyTrades) || maxDailyTrades <= 0)
                {
                    ShowWarning("请输入有效的每日最大交易次数（> 0）");
                    return false;
                }
                
                // 验证最大持仓数
                if (!int.TryParse(_maxPositionsTextBox?.Text, out int maxPositions) || maxPositions <= 0)
                {
                    ShowWarning("请输入有效的最大持仓数（> 0）");
                    return false;
                }
                
                // 验证最大回撤
                if (!decimal.TryParse(_maxDrawdownTextBox?.Text, out decimal maxDrawdown) || maxDrawdown <= 0 || maxDrawdown > 100)
                {
                    ShowWarning("请输入有效的最大回撤（1-100%）");
                    return false;
                }
                
                // 验证熔断机制
                if (!int.TryParse(_maxConsecutiveLossesTextBox?.Text, out int maxConsecutiveLosses) || maxConsecutiveLosses <= 0)
                {
                    ShowWarning("请输入有效的连续亏损次数（> 0）");
                    return false;
                }
                break;
        }
        
        return true;
    }
    
    /// <summary>
    /// 显示警告
    /// </summary>
    private void ShowWarning(string message)
    {
        if (_warningPanel != null && _warningText != null)
        {
            _warningText.Text = message;
            _warningPanel.IsVisible = true;
        }
    }
    
    /// <summary>
    /// 上一步
    /// </summary>
    private void OnBack()
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateStepUI();
        }
    }
    
    /// <summary>
    /// 下一步
    /// </summary>
    private void OnNext()
    {
        if (!ValidateCurrentStep()) return;
        
        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            UpdateStepUI();
        }
    }
    
    /// <summary>
    /// 取消
    /// </summary>
    private void OnCancel()
    {
        Close((TradingInstanceConfig?)null);
    }
    
    /// <summary>
    /// 重写关闭按钮点击事件，返回 null 而不是 false
    /// </summary>
    protected override void OnCloseButtonClick()
    {
        Close((TradingInstanceConfig?)null);
    }
    
    /// <summary>
    /// 创建实例
    /// </summary>
    private async void OnCreate()
    {
        if (!ValidateCurrentStep()) return;
        
        try
        {
            // 提取策略信息
            var strategyItem = _strategyComboBox?.SelectedItem as ComboBoxItem;
            var strategy = strategyItem?.Tag as StrategyInfo;
            if (strategy == null)
            {
                ShowWarning("请选择策略");
                return;
            }
            
            // 提取策略版本
            var versionItem = _strategyVersionComboBox?.SelectedItem as ComboBoxItem;
            var version = versionItem?.Tag as VersionListItem;
            if (version == null)
            {
                ShowWarning("请选择策略版本");
                return;
            }
            
            // 加载策略代码
            string strategyCode = string.Empty;
            if (!string.IsNullOrEmpty(strategy.Id) && !string.IsNullOrEmpty(version.VersionString))
            {
                var strategyVersion = await _strategyService.GetStrategyVersionAsync(strategy.Id, version.VersionString);
                strategyCode = strategyVersion?.Dsl ?? string.Empty;
            }
            
            // 提取时间周期
            var timeframe = _timeframeComboBox?.SelectedItem is ComboBoxItem tfItem
                ? tfItem.Content?.ToString()?.Split(' ')[0] ?? "5m"
                : "5m";
            
            // 提取交易所
            var exchange = _exchangeComboBox?.SelectedItem is ComboBoxItem exItem
                ? exItem.Content?.ToString()?.Split(' ')[0] ?? "Binance"
                : "Binance";
            
            // 提取交易对
            var symbol = _symbolComboBox?.SelectedItem is ComboBoxItem symItem
                ? symItem.Content?.ToString() ?? "BTCUSDT"
                : "BTCUSDT";
            
            // 提取实例名称（如果为空则自动生成）
            var instanceName = _instanceNameTextBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(instanceName))
            {
                instanceName = $"{exchange}-{symbol}-{strategy.Name}";
            }
            
            // 提取杠杆倍率
            var leverageText = _leverageComboBox?.SelectedItem is ComboBoxItem levItem
                ? levItem.Content?.ToString()?.Replace("倍", "") ?? "10"
                : "10";
            var leverage = decimal.Parse(leverageText);
            
            // 提取仓位模式
            var marginMode = _marginModeComboBox?.SelectedItem is ComboBoxItem modeItem
                ? modeItem.Content?.ToString()?.Contains("逐仓") == true ? "Isolated" : "Cross"
                : "Isolated";
            
            // 提取模拟交易设置
            var useTestnet = _useTestnetCheckBox?.IsChecked ?? true;
            
            // 创建配置
            var config = new TradingInstanceConfig
            {
                Name = instanceName,
                Exchange = exchange,
                Symbol = symbol,
                Timeframe = timeframe,
                StrategyName = strategy.Name,
                StrategyCode = strategyCode,
                StrategyVersion = version.VersionString,
                RiskConfig = new RiskConfig
                {
                    // 资金配置
                    InitialCapital = decimal.Parse(_initialCapitalTextBox?.Text ?? "10000"),
                    PositionSizePercent = decimal.Parse(_positionSizePercentTextBox?.Text ?? "10") / 100m,
                    
                    // 交易参数
                    MaxStopLossPercent = decimal.Parse(_maxStopLossPercentTextBox?.Text ?? "20") / 100m,
                    MinTakeProfitPercent = decimal.Parse(_minTakeProfitPercentTextBox?.Text ?? "30") / 100m,
                    MarginMode = marginMode,
                    Leverage = leverage,
                    
                    // 风控参数
                    MaxOpenPositions = int.Parse(_maxPositionsTextBox?.Text ?? "3"),
                    MaxDrawdown = decimal.Parse(_maxDrawdownTextBox?.Text ?? "15") / 100m,
                    MaxDailyTrades = int.Parse(_maxDailyTradesTextBox?.Text ?? "10"),
                    MaxConsecutiveLosses = int.Parse(_maxConsecutiveLossesTextBox?.Text ?? "5")
                },
                CapitalConfig = new CapitalConfig
                {
                    InitialCapital = decimal.Parse(_initialCapitalTextBox?.Text ?? "10000")
                },
                ExchangeConfig = new ExchangeConfig
                {
                    ExchangeName = exchange,
                    UseTestnet = useTestnet
                }
            };
            
            Console.WriteLine($"[CreateTradingInstanceDialog] 实例配置已创建:");
            Console.WriteLine($"   名称: {config.Name}");
            Console.WriteLine($"   交易所: {config.Exchange}");
            Console.WriteLine($"   交易对: {config.Symbol}");
            Console.WriteLine($"   时间周期: {config.Timeframe}");
            Console.WriteLine($"   策略: {config.StrategyName} ({config.StrategyVersion})");
            Console.WriteLine($"   初始资金: {config.RiskConfig.InitialCapital} USDT");
            Console.WriteLine($"   仓位比例: {config.RiskConfig.PositionSizePercent * 100}%");
            Console.WriteLine($"   杠杆倍率: {leverage}x");
            Console.WriteLine($"   仓位模式: {config.RiskConfig.MarginMode}");
            Console.WriteLine($"   最大止损: {config.RiskConfig.MaxStopLossPercent * 100}%");
            Console.WriteLine($"   最小止盈: {config.RiskConfig.MinTakeProfitPercent * 100}%");
            Console.WriteLine($"   模拟交易: {useTestnet}");
            
            Close(config);
        }
        catch (Exception ex)
        {
            ShowWarning($"创建失败: {ex.Message}");
            Console.WriteLine($"[CreateTradingInstanceDialog] 创建实例失败: {ex.Message}");
            Console.WriteLine($"[CreateTradingInstanceDialog] 堆栈跟踪: {ex.StackTrace}");
        }
    }
}

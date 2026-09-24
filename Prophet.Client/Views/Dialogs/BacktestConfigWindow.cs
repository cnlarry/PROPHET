using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Controls;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Views.Dialogs;

public class BacktestConfigWindow : ModernDialog
{
    private BacktestConfigDialogViewModel? _viewModel;
    private TextBlock? _validationTextBlock;
    
    public BacktestConfigWindow()
    {
        Title = "创建回测任务";
        Width = 680;
        Height = 630;
        
        _viewModel = new BacktestConfigDialogViewModel();
        DataContext = _viewModel;
        
        BuildContent();
        
        // 异步加载策略列表
        _ = _viewModel.InitializeAsync();
        
        // 重写基类的 ESC 键行为，确保返回正确类型
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                Close((BacktestConfigRequest?)null);
                e.Handled = true;
            }
        };
    }
    
    private void BuildContent()
    {
        if (_viewModel == null) return;
        
        // 创建内容控件
        var contentPanel = new StackPanel
        {
            Spacing = 16
        };
        
        // 策略和版本选择（两列布局）
        var strategyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        strategyGrid.Children.Add(CreateFieldPanel("选择策略", CreateStrategyComboBox()));
        var versionPanel = CreateFieldPanel("策略版本", CreateVersionComboBox());
        versionPanel.SetValue(Grid.ColumnProperty, 1);
        strategyGrid.Children.Add(versionPanel);
        contentPanel.Children.Add(strategyGrid);
        
        // 日期选择（两列布局）
        var dateGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        dateGrid.Children.Add(CreateFieldPanel("开始日期", CreateStartDatePicker()));
        var endDatePanel = CreateFieldPanel("结束日期", CreateEndDatePicker());
        endDatePanel.SetValue(Grid.ColumnProperty, 1);
        dateGrid.Children.Add(endDatePanel);
        contentPanel.Children.Add(dateGrid);

        // 标的选择（InstrumentKey）
        contentPanel.Children.Add(CreateFieldPanel("选择标的（InstrumentKey）", CreateSymbolKeyComboBox()));
        
        // 初始资金、仓位比例、杠杆倍率、采样频率（四列布局）
        var capitalGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 16
        };
        capitalGrid.Children.Add(CreateFieldPanel("初始资金 (USDT)", CreateInitialCapitalInput()));
        var positionSizePanel = CreateFieldPanel("仓位比例", CreatePositionSizePercentInput());
        positionSizePanel.SetValue(Grid.ColumnProperty, 1);
        capitalGrid.Children.Add(positionSizePanel);
        var leveragePanel = CreateFieldPanel("杠杆倍率", CreateLeverageInput());
        leveragePanel.SetValue(Grid.ColumnProperty, 2);
        capitalGrid.Children.Add(leveragePanel);
        var signalPanel = CreateFieldPanel("采样频率", CreateSignalIntervalComboBox());
        signalPanel.SetValue(Grid.ColumnProperty, 3);
        capitalGrid.Children.Add(signalPanel);
        contentPanel.Children.Add(capitalGrid);
        
        // 手续费率、滑点率、默认止损比例、默认止盈比例（四列布局）
        var feeAndTpslGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 16
        };
        feeAndTpslGrid.Children.Add(CreateFieldPanel("手续费率 (Taker)", CreateFeeRateInput()));
        var slippagePanel = CreateFieldPanel("滑点率", CreateSlippageRateInput());
        slippagePanel.SetValue(Grid.ColumnProperty, 1);
        feeAndTpslGrid.Children.Add(slippagePanel);
        var stopLossPanel = CreateFieldPanel("默认止损比例", CreateDefaultStopLossInput());
        stopLossPanel.SetValue(Grid.ColumnProperty, 2);
        feeAndTpslGrid.Children.Add(stopLossPanel);
        var takeProfitPanel = CreateFieldPanel("默认止盈比例", CreateDefaultTakeProfitInput());
        takeProfitPanel.SetValue(Grid.ColumnProperty, 3);
        feeAndTpslGrid.Children.Add(takeProfitPanel);
        contentPanel.Children.Add(feeAndTpslGrid);
        
        // P1.3 & P2.6: 滑点模式和仓位计算方法（两列布局）
        var methodGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        methodGrid.Children.Add(CreateFieldPanel("滑点模式", CreateSlippageModeComboBox()));
        var positionMethodPanel = CreateFieldPanel("仓位计算", CreatePositionSizeMethodComboBox());
        positionMethodPanel.SetValue(Grid.ColumnProperty, 1);
        methodGrid.Children.Add(positionMethodPanel);
        contentPanel.Children.Add(methodGrid);
        
        // P2.6: ATR周期、ATR止损倍数、每笔交易风险、最大Kelly比例（四列布局）
        var atrAndRiskGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 16
        };
        atrAndRiskGrid.Children.Add(CreateFieldPanel("ATR周期", CreateATRPeriodInput()));
        var atrMultPanel = CreateFieldPanel("ATR止损倍数", CreateATRMultiplierInput());
        atrMultPanel.SetValue(Grid.ColumnProperty, 1);
        atrAndRiskGrid.Children.Add(atrMultPanel);
        var riskPanel = CreateFieldPanel("每笔交易风险 (%)", CreateRiskPercentInput());
        riskPanel.SetValue(Grid.ColumnProperty, 2);
        atrAndRiskGrid.Children.Add(riskPanel);
        var kellyPanel = CreateFieldPanel("最大Kelly比例", CreateMaxKellyFractionInput());
        kellyPanel.SetValue(Grid.ColumnProperty, 3);
        atrAndRiskGrid.Children.Add(kellyPanel);
        contentPanel.Children.Add(atrAndRiskGrid);
        
        // P2.8: 移动止损/追踪止盈开关（两列布局）
        var trailingGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        trailingGrid.Children.Add(CreateFieldPanel("启用移动止损", CreateTrailingStopToggle()));
        var trailingTPPanel = CreateFieldPanel("启用追踪止盈", CreateTrailingTakeProfitToggle());
        trailingTPPanel.SetValue(Grid.ColumnProperty, 1);
        trailingGrid.Children.Add(trailingTPPanel);
        contentPanel.Children.Add(trailingGrid);
        
        // 验证消息
        _validationTextBlock = new TextBlock
        {
            Foreground = Avalonia.Media.Brushes.Red,
            FontSize = 12,
            IsVisible = false
        };
        _validationTextBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.ValidationMessage)));
        _validationTextBlock.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.HasValidationMessage)));
        contentPanel.Children.Add(_validationTextBlock);
        
        // 设置内容
        SetContent(contentPanel);
        
        // 添加按钮
        AddButton("取消", OnCancelClicked);
        AddButton("开始回测", OnStartClicked, isPrimary: true);
    }
    
    private StackPanel CreateFieldPanel(string label, Control input)
    {
        var panel = new StackPanel
        {
            Spacing = 6
        };
        
        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8B8D91"))
        };
        
        panel.Children.Add(labelText);
        panel.Children.Add(input);
        
        return panel;
    }
    
    private ComboBox CreateStrategyComboBox()
    {
        var comboBox = new ComboBox
        {
            PlaceholderText = "请选择策略",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.Strategies)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SelectedStrategy)));
        comboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Prophet.Client.Models.StrategyInfo>(
            (item, _) => item == null 
                ? new TextBlock { Text = "" } 
                : new TextBlock { Text = $"{item.Name ?? ""}" });
        return comboBox;
    }
    
    private ComboBox CreateVersionComboBox()
    {
        var comboBox = new ComboBox
        {
            PlaceholderText = "请选择版本",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.StrategyVersions)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SelectedVersion)));
        comboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Prophet.Client.Models.VersionListItem>(
            (item, _) => item == null ? new TextBlock { Text = "" } : new TextBlock { Text = item.VersionString ?? "" });
        return comboBox;
    }

    private ComboBox CreateSymbolKeyComboBox()
    {
        var comboBox = new ComboBox
        {
            PlaceholderText = "请选择标的（例如 BTCUSDT-OKX-SWAP）",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SymbolKeys)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SelectedSymbolKey)));

        return comboBox;
    }
    
    private DatePicker CreateStartDatePicker()
    {
        var datePicker = new DatePicker();
        datePicker.Bind(DatePicker.SelectedDateProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.StartDate)));
        return datePicker;
    }
    
    private DatePicker CreateEndDatePicker()
    {
        var datePicker = new DatePicker();
        datePicker.Bind(DatePicker.SelectedDateProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.EndDate)));
        return datePicker;
    }
    
    private NumericUpDown CreateInitialCapitalInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 100,
            Maximum = 10000000,
            Increment = 100,
            FormatString = "N2"
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.InitialCapital)));
        return input;
    }
    
    private NumericUpDown CreateLeverageInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 125,
            Increment = 1,
            FormatString = "N0",
            Value = 10  // 默认10倍
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.Leverage)));
        return input;
    }
    
    private NumericUpDown CreatePositionSizePercentInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.01m,  // 最小1%
            Maximum = 1,      // 最大100%
            Increment = 0.01m,
            FormatString = "P0",
            Value = 0.05m  // 默认5%
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.PositionSizePercent)));
        return input;
    }
    
    private ComboBox CreateSignalIntervalComboBox()
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SignalSamplingIntervals)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SelectedSignalInterval)));
        comboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SignalIntervalOption>(
            (item, _) => item == null ? new TextBlock { Text = "" } : new TextBlock { Text = item.DisplayName ?? "" });
        return comboBox;
    }
    
    private NumericUpDown CreateFeeRateInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 0.01m,  // 最大1%
            Increment = 0.0001m,
            FormatString = "N4"
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.FeeRate)));
        return input;
    }
    
    private NumericUpDown CreateSlippageRateInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 0.01m,  // 最大1%
            Increment = 0.0001m,
            FormatString = "N4"
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SlippageRate)));
        return input;
    }
    
    // P0.1: 默认止盈止损百分比配置输入
    private NumericUpDown CreateDefaultTakeProfitInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.001m,  // 最小0.1%
            Maximum = 1,       // 最大100%
            Increment = 0.01m,
            FormatString = "P0",
            Value = 0.05m  // 默认5%
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.DefaultTakeProfitPercent)));
        return input;
    }
    
    private NumericUpDown CreateDefaultStopLossInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.001m,  // 最小0.1%
            Maximum = 1,       // 最大100%
            Increment = 0.01m,
            FormatString = "P0",
            Value = 0.02m  // 默认2%
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.DefaultStopLossPercent)));
        return input;
    }
    
    // P1.3: 滑点模式选择
    private ComboBox CreateSlippageModeComboBox()
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Items.Add("FixedBps");
        comboBox.Items.Add("FixedPrice");
        comboBox.Items.Add("PctOfSpread");
        comboBox.Items.Add("MarketImpact");
        comboBox.SelectedIndex = 0;
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.SlippageMode)));
        return comboBox;
    }
    
    // P2.6: 仓位计算方法选择
    private ComboBox CreatePositionSizeMethodComboBox()
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Items.Add("ATR");
        comboBox.Items.Add("Kelly");
        comboBox.Items.Add("Fixed");
        comboBox.SelectedIndex = 2; // 默认选择Fixed（对应原来的fixed）
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.PositionSizeMethod)));
        return comboBox;
    }
    
    // P2.6: ATR参数输入
    private NumericUpDown CreateATRPeriodInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 5,   // ATR周期最小5
            Maximum = 100,
            Increment = 1,
            FormatString = "N0",
            Value = 14
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.ATRPeriod)));
        return input;
    }
    
    private NumericUpDown CreateATRMultiplierInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.1m,
            Maximum = 10,
            Increment = 0.5m,
            FormatString = "N1",
            Value = 2m
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.ATRMultiplier)));
        return input;
    }
    
    private NumericUpDown CreateRiskPercentInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.001m,
            Maximum = 0.1m,
            Increment = 0.005m,
            FormatString = "P1",
            Value = 0.01m
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.RiskPercentPerTrade)));
        return input;
    }
    
    // P2.6: Kelly参数输入
    private NumericUpDown CreateMaxKellyFractionInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.05m,
            Maximum = 1m,
            Increment = 0.05m,
            FormatString = "P0",
            Value = 0.25m
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.MaxKellyFraction)));
        return input;
    }
    
    // P2.8: 移动止损开关
    private CheckBox CreateTrailingStopToggle()
    {
        var checkBox = new CheckBox
        {
            Content = "动态更新止损价格"
        };
        checkBox.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.EnableTrailingStop)));
        return checkBox;
    }
    
    // P2.8: 追踪止盈开关
    private CheckBox CreateTrailingTakeProfitToggle()
    {
        var checkBox = new CheckBox
        {
            Content = "动态更新止盈价格"
        };
        checkBox.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding(nameof(BacktestConfigDialogViewModel.EnableTrailingTakeProfit)));
        return checkBox;
    }
    
    private void OnCancelClicked()
    {
        Close((BacktestConfigRequest?)null);
    }
    
    private void OnStartClicked()
    {
        Console.WriteLine("🔘 点击开始回测按钮");
        
        if (_viewModel == null)
        {
            Console.WriteLine("❌ ViewModel 为 null");
            Close((BacktestConfigRequest?)null);
            return;
        }
        
        Console.WriteLine($"📝 尝试构建回测请求...");
        Console.WriteLine($"   选中策略: {_viewModel.SelectedStrategy?.Name ?? "未选择"}");
        Console.WriteLine($"   选中版本: {_viewModel.SelectedVersion?.VersionString ?? "未选择"}");
        
        if (_viewModel.TryBuildRequest(out BacktestConfigRequest? request) && request != null)
        {
            // 自动保存配置（如果失败也不影响回测）
            try
            {
                _viewModel.SaveCurrentConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 保存回测配置时出错: {ex.Message}");
            }
            
            Console.WriteLine("✅ 回测请求创建成功，关闭窗口");
            Close(request);
        }
        else
        {
            Console.WriteLine($"❌ 回测请求创建失败");
            Console.WriteLine($"   验证消息: {_viewModel.ValidationMessage}");
            
            if (!_viewModel.HasValidationMessage)
            {
                _viewModel.ValidationMessage = "请完善参数配置";
            }
        }
    }
    
    /// <summary>
    /// 重写基类的关闭按钮点击事件，返回正确的类型
    /// </summary>
    protected override void OnCloseButtonClick()
    {
        Close((BacktestConfigRequest?)null);
    }
}


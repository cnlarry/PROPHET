using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Controls;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Views.Dialogs;

/// <summary>
/// 分步回测配置向导窗口（三步式）
/// </summary>
public class StepWizardBacktestConfigWindow : ModernDialog
{
    private StepWizardBacktestConfigViewModel? _viewModel;
    private TextBlock? _validationTextBlock;
    
    // 步骤容器
    private StackPanel? _step1Content;
    private StackPanel? _step2Content;
    private StackPanel? _step3Content;
    
    // 导航按钮
    private Button? _prevButton;
    private Button? _nextButton;
    private Button? _startButton;
    
    // 进度指示器
    private Grid? _progressIndicator;
    
    public StepWizardBacktestConfigWindow()
    {
        Title = "创建回测任务";
        Width = 720;
        Height = 680;
        
        _viewModel = new StepWizardBacktestConfigViewModel();
        _viewModel.CurrentStepChanged += OnCurrentStepChanged;
        DataContext = _viewModel;
        
        BuildContent();
        
        // 异步加载策略列表
        _ = _viewModel.InitializeAsync();
        
        // ESC 键关闭
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
        
        var mainPanel = new StackPanel
        {
            Spacing = 20
        };
        
        // 进度指示器
        _progressIndicator = CreateProgressIndicator();
        mainPanel.Children.Add(_progressIndicator);
        
        // 步骤容器
        var stepsContainer = new Grid();
        
        _step1Content = CreateStep1Content();
        _step2Content = CreateStep2Content();
        _step3Content = CreateStep3Content();
        
        _step1Content.IsVisible = true;
        _step2Content.IsVisible = false;
        _step3Content.IsVisible = false;
        
        stepsContainer.Children.Add(_step1Content);
        stepsContainer.Children.Add(_step2Content);
        stepsContainer.Children.Add(_step3Content);
        
        mainPanel.Children.Add(stepsContainer);
        
        // 验证消息
        _validationTextBlock = new TextBlock
        {
            Foreground = Brushes.Red,
            FontSize = 12,
            IsVisible = false,
            Margin = new Thickness(0, 10, 0, 0)
        };
        _validationTextBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.ValidationMessage)));
        _validationTextBlock.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.HasValidationMessage)));
        mainPanel.Children.Add(_validationTextBlock);
        
        SetContent(mainPanel);
        
        // 添加导航按钮
        _prevButton = AddButton("上一步", OnPrevClicked);
        _nextButton = AddButton("下一步", OnNextClicked, isPrimary: true);
        _startButton = AddButton("开始回测", OnStartClicked, isPrimary: true);
        
        AddButton("取消", OnCancelClicked);
        
        UpdateButtonStates();
    }
    
    /// <summary>
    /// 创建进度指示器
    /// </summary>
    private Grid CreateProgressIndicator()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        
        // 步骤1
        var step1 = CreateStepIndicator(1, "策略与标的", true);
        step1.SetValue(Grid.ColumnProperty, 0);
        grid.Children.Add(step1);
        
        // 连接线1
        var line1 = CreateConnectionLine();
        line1.SetValue(Grid.ColumnProperty, 1);
        grid.Children.Add(line1);
        
        // 步骤2
        var step2 = CreateStepIndicator(2, "资金与风控", false);
        step2.SetValue(Grid.ColumnProperty, 2);
        grid.Children.Add(step2);
        
        // 连接线2
        var line2 = CreateConnectionLine();
        line2.SetValue(Grid.ColumnProperty, 3);
        grid.Children.Add(line2);
        
        // 步骤3
        var step3 = CreateStepIndicator(3, "高级参数", false);
        step3.SetValue(Grid.ColumnProperty, 4);
        grid.Children.Add(step3);
        
        return grid;
    }
    
    private StackPanel CreateStepIndicator(int stepNumber, string stepName, bool isActive)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        
        // 步骤圆圈
        var circle = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = isActive 
                ? new SolidColorBrush(Color.Parse("#F0B90B"))
                : new SolidColorBrush(Color.Parse("#2B3139")),
            BorderBrush = new SolidColorBrush(Color.Parse("#474D57")),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = stepNumber.ToString(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = isActive 
                    ? Brushes.Black
                    : new SolidColorBrush(Color.Parse("#EAECEF")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        circle.Name = $"StepCircle{stepNumber}";
        
        // 步骤名称
        var nameText = new TextBlock
        {
            Text = stepName,
            FontSize = 13,
            FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = isActive
                ? new SolidColorBrush(Color.Parse("#EAECEF"))
                : new SolidColorBrush(Color.Parse("#8B8D91")),
            VerticalAlignment = VerticalAlignment.Center
        };
        nameText.Name = $"StepName{stepNumber}";
        
        panel.Children.Add(circle);
        panel.Children.Add(nameText);
        
        return panel;
    }
    
    private Border CreateConnectionLine()
    {
        return new Border
        {
            Height = 2,
            Margin = new Thickness(8, 0, 8, 0),
            Background = new SolidColorBrush(Color.Parse("#474D57")),
            VerticalAlignment = VerticalAlignment.Center
        };
    }
    
    /// <summary>
    /// 更新进度指示器状态
    /// </summary>
    private void UpdateProgressIndicator(int currentStep)
    {
        if (_progressIndicator == null) return;
        
        for (int i = 1; i <= 3; i++)
        {
            var circle = _progressIndicator.FindControl<Border>($"StepCircle{i}");
            var nameText = _progressIndicator.FindControl<TextBlock>($"StepName{i}");
            
            bool isActive = i == currentStep;
            bool isCompleted = i < currentStep;
            
            if (circle != null)
            {
                circle.Background = isCompleted || isActive
                    ? new SolidColorBrush(Color.Parse("#F0B90B"))
                    : new SolidColorBrush(Color.Parse("#2B3139"));
                
                if (circle.Child is TextBlock circleText)
                {
                    circleText.Foreground = isCompleted || isActive
                        ? Brushes.Black
                        : new SolidColorBrush(Color.Parse("#EAECEF"));
                    
                    // 已完成步骤显示勾
                    if (isCompleted)
                    {
                        circleText.Text = "✓";
                    }
                    else
                    {
                        circleText.Text = i.ToString();
                    }
                }
            }
            
            if (nameText != null)
            {
                nameText.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal;
                nameText.Foreground = isActive || isCompleted
                    ? new SolidColorBrush(Color.Parse("#EAECEF"))
                    : new SolidColorBrush(Color.Parse("#8B8D91"));
            }
        }
    }
    
    /// <summary>
    /// 创建第一步内容：策略与标的
    /// </summary>
    private StackPanel CreateStep1Content()
    {
        var panel = new StackPanel { Spacing = 16 };
        
        // 标题
        panel.Children.Add(CreateSectionTitle("第一步：策略与标的", "选择要回测的策略、版本和交易标的"));
        
        // 策略和版本选择（两列）
        var strategyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        strategyGrid.Children.Add(CreateFieldPanel("选择策略", CreateStrategyComboBox()));
        var versionPanel = CreateFieldPanel("策略版本", CreateVersionComboBox());
        versionPanel.SetValue(Grid.ColumnProperty, 1);
        strategyGrid.Children.Add(versionPanel);
        panel.Children.Add(strategyGrid);
        
        // 标的选择
        panel.Children.Add(CreateFieldPanel("选择标的（InstrumentKey）", CreateSymbolKeyComboBox()));
        
        // 日期选择（两列）
        var dateGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        dateGrid.Children.Add(CreateFieldPanel("开始日期", CreateStartDatePicker()));
        var endDatePanel = CreateFieldPanel("结束日期", CreateEndDatePicker());
        endDatePanel.SetValue(Grid.ColumnProperty, 1);
        dateGrid.Children.Add(endDatePanel);
        panel.Children.Add(dateGrid);
        
        // 提示信息
        panel.Children.Add(CreateHintText("💡 提示：结束日期不能选择今天（历史数据未生成），建议选择至少1天的时间跨度。"));
        
        return panel;
    }
    
    /// <summary>
    /// 创建第二步内容：资金与风控
    /// </summary>
    private StackPanel CreateStep2Content()
    {
        var panel = new StackPanel { Spacing = 16 };
        
        // 标题
        panel.Children.Add(CreateSectionTitle("第二步：资金与风控", "配置初始资金、仓位、杠杆和风险控制参数"));
        
        // 资金配置（四列）
        var capitalGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 16
        };
        capitalGrid.Children.Add(CreateFieldPanel("初始资金 (USDT)", CreateInitialCapitalInput()));
        var positionPanel = CreateFieldPanel("仓位比例", CreatePositionSizePercentInput());
        positionPanel.SetValue(Grid.ColumnProperty, 1);
        capitalGrid.Children.Add(positionPanel);
        var leveragePanel = CreateFieldPanel("杠杆倍率", CreateLeverageInput());
        leveragePanel.SetValue(Grid.ColumnProperty, 2);
        capitalGrid.Children.Add(leveragePanel);
        var signalPanel = CreateFieldPanel("采样频率", CreateSignalIntervalComboBox());
        signalPanel.SetValue(Grid.ColumnProperty, 3);
        capitalGrid.Children.Add(signalPanel);
        panel.Children.Add(capitalGrid);
        
        panel.Children.Add(CreateHintText("💡 实际开仓价值 = 初始资金 × 仓位比例 × 杠杆倍率"));
        
        // 费用配置（两列）
        var feeGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        feeGrid.Children.Add(CreateFieldPanel("手续费率 (Taker)", CreateFeeRateInput()));
        var slippagePanel = CreateFieldPanel("滑点率", CreateSlippageRateInput());
        slippagePanel.SetValue(Grid.ColumnProperty, 1);
        feeGrid.Children.Add(slippagePanel);
        panel.Children.Add(feeGrid);
        
        // 止盈止损配置（两列）
        var tpslGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        tpslGrid.Children.Add(CreateFieldPanel("默认止损比例", CreateDefaultStopLossInput()));
        var tpPanel = CreateFieldPanel("默认止盈比例", CreateDefaultTakeProfitInput());
        tpPanel.SetValue(Grid.ColumnProperty, 1);
        tpslGrid.Children.Add(tpPanel);
        panel.Children.Add(tpslGrid);
        
        panel.Children.Add(CreateHintText("💡 止盈止损比例基于保证金计算，例如：10倍杠杆下，40%止盈 = 价格涨4%"));
        
        return panel;
    }
    
    /// <summary>
    /// 创建第三步内容：高级参数
    /// </summary>
    private StackPanel CreateStep3Content()
    {
        var panel = new StackPanel { Spacing = 16 };
        
        // 标题
        panel.Children.Add(CreateSectionTitle("第三步：高级参数（可选）", "精细化调优，大多数情况下使用默认值即可"));
        
        // 滑点和仓位计算方法（两列）
        var methodGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        methodGrid.Children.Add(CreateFieldPanel("滑点模式", CreateSlippageModeComboBox()));
        var positionMethodPanel = CreateFieldPanel("仓位计算", CreatePositionSizeMethodComboBox());
        positionMethodPanel.SetValue(Grid.ColumnProperty, 1);
        methodGrid.Children.Add(positionMethodPanel);
        panel.Children.Add(methodGrid);
        
        // ATR和风险参数（四列）
        var atrGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 16
        };
        atrGrid.Children.Add(CreateFieldPanel("ATR周期", CreateATRPeriodInput()));
        var atrMultPanel = CreateFieldPanel("ATR止损倍数", CreateATRMultiplierInput());
        atrMultPanel.SetValue(Grid.ColumnProperty, 1);
        atrGrid.Children.Add(atrMultPanel);
        var riskPanel = CreateFieldPanel("每笔交易风险 (%)", CreateRiskPercentInput());
        riskPanel.SetValue(Grid.ColumnProperty, 2);
        atrGrid.Children.Add(riskPanel);
        var kellyPanel = CreateFieldPanel("最大Kelly比例", CreateMaxKellyFractionInput());
        kellyPanel.SetValue(Grid.ColumnProperty, 3);
        atrGrid.Children.Add(kellyPanel);
        panel.Children.Add(atrGrid);
        
        // 移动止损/追踪止盈（两列）
        var trailingGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        trailingGrid.Children.Add(CreateFieldPanel("启用移动止损", CreateTrailingStopToggle()));
        var trailingTPPanel = CreateFieldPanel("启用追踪止盈", CreateTrailingTakeProfitToggle());
        trailingTPPanel.SetValue(Grid.ColumnProperty, 1);
        trailingGrid.Children.Add(trailingTPPanel);
        panel.Children.Add(trailingGrid);
        
        panel.Children.Add(CreateHintText("💡 高级参数仅在特定场景下需要调整，新手用户建议保持默认值。"));
        
        return panel;
    }
    
    private StackPanel CreateSectionTitle(string title, string subtitle)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#EAECEF"))
        });
        
        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#8B8D91"))
        });
        
        return panel;
    }
    
    private TextBlock CreateHintText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#F0B90B")),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, -4, 0, 0)
        };
    }
    
    private StackPanel CreateFieldPanel(string label, Control input)
    {
        var panel = new StackPanel { Spacing = 6 };
        
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#8B8D91"))
        });
        
        panel.Children.Add(input);
        
        return panel;
    }
    
    #region 控件创建方法
    
    private ComboBox CreateStrategyComboBox()
    {
        var comboBox = new ComboBox
        {
            PlaceholderText = "请选择策略",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.Strategies)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SelectedStrategy)));
        comboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Prophet.Client.Models.StrategyInfo>(
            (item, _) => item == null ? new TextBlock { Text = "" } : new TextBlock { Text = $"{item.Name ?? ""}" });
        return comboBox;
    }
    
    private ComboBox CreateVersionComboBox()
    {
        var comboBox = new ComboBox
        {
            PlaceholderText = "请选择版本",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.StrategyVersions)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SelectedVersion)));
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
        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SymbolKeys)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SelectedSymbolKey)));
        return comboBox;
    }
    
    private DatePicker CreateStartDatePicker()
    {
        var datePicker = new DatePicker();
        datePicker.Bind(DatePicker.SelectedDateProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.StartDate)));
        return datePicker;
    }
    
    private DatePicker CreateEndDatePicker()
    {
        var datePicker = new DatePicker();
        datePicker.Bind(DatePicker.SelectedDateProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.EndDate)));
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
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.InitialCapital)));
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
            Value = 10
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.Leverage)));
        return input;
    }
    
    private NumericUpDown CreatePositionSizePercentInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.01m,
            Maximum = 1,
            Increment = 0.01m,
            FormatString = "P0",
            Value = 0.05m
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.PositionSizePercent)));
        return input;
    }
    
    private ComboBox CreateSignalIntervalComboBox()
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SignalSamplingIntervals)));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SelectedSignalInterval)));
        comboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SignalIntervalOption>(
            (item, _) => item == null ? new TextBlock { Text = "" } : new TextBlock { Text = item.DisplayName ?? "" });
        return comboBox;
    }
    
    private NumericUpDown CreateFeeRateInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 0.01m,
            Increment = 0.0001m,
            FormatString = "N4"
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.FeeRate)));
        return input;
    }
    
    private NumericUpDown CreateSlippageRateInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 0.01m,
            Increment = 0.0001m,
            FormatString = "N4"
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SlippageRate)));
        return input;
    }
    
    private NumericUpDown CreateDefaultTakeProfitInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.001m,
            Maximum = 1,
            Increment = 0.01m,
            FormatString = "P0",
            Value = 0.05m
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.DefaultTakeProfitPercent)));
        return input;
    }
    
    private NumericUpDown CreateDefaultStopLossInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 0.001m,
            Maximum = 1,
            Increment = 0.01m,
            FormatString = "P0",
            Value = 0.02m
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.DefaultStopLossPercent)));
        return input;
    }
    
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
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.SlippageMode)));
        return comboBox;
    }
    
    private ComboBox CreatePositionSizeMethodComboBox()
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        comboBox.Items.Add("ATR");
        comboBox.Items.Add("Kelly");
        comboBox.Items.Add("Fixed");
        comboBox.SelectedIndex = 2;
        comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.PositionSizeMethod)));
        return comboBox;
    }
    
    private NumericUpDown CreateATRPeriodInput()
    {
        var input = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 100,
            Increment = 1,
            FormatString = "N0",
            Value = 14
        };
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.ATRPeriod)));
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
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.ATRMultiplier)));
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
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.RiskPercentPerTrade)));
        return input;
    }
    
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
        input.Bind(NumericUpDown.ValueProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.MaxKellyFraction)));
        return input;
    }
    
    private CheckBox CreateTrailingStopToggle()
    {
        var checkBox = new CheckBox
        {
            Content = "动态更新止损价格"
        };
        checkBox.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.EnableTrailingStop)));
        return checkBox;
    }
    
    private CheckBox CreateTrailingTakeProfitToggle()
    {
        var checkBox = new CheckBox
        {
            Content = "动态更新止盈价格"
        };
        checkBox.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding(nameof(StepWizardBacktestConfigViewModel.EnableTrailingTakeProfit)));
        return checkBox;
    }
    
    #endregion
    
    #region 事件处理
    
    private void OnCurrentStepChanged(object? sender, EventArgs e)
    {
        UpdateStepVisibility();
        UpdateProgressIndicator(_viewModel?.CurrentStep ?? 1);
        UpdateButtonStates();
    }
    
    private void UpdateStepVisibility()
    {
        if (_viewModel == null || _step1Content == null || _step2Content == null || _step3Content == null)
            return;
        
        _step1Content.IsVisible = _viewModel.CurrentStep == 1;
        _step2Content.IsVisible = _viewModel.CurrentStep == 2;
        _step3Content.IsVisible = _viewModel.CurrentStep == 3;
    }
    
    private void UpdateButtonStates()
    {
        if (_viewModel == null || _prevButton == null || _nextButton == null || _startButton == null)
            return;
        
        _prevButton.IsVisible = _viewModel.CurrentStep > 1;
        _nextButton.IsVisible = _viewModel.CurrentStep < 3;
        _startButton.IsVisible = _viewModel.CurrentStep == 3;
    }
    
    private void OnPrevClicked()
    {
        _viewModel?.GoToPreviousStep();
    }
    
    private void OnNextClicked()
    {
        _viewModel?.GoToNextStep();
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
        
        if (_viewModel.TryBuildRequest(out BacktestConfigRequest? request) && request != null)
        {
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
            Console.WriteLine($"❌ 回测请求创建失败: {_viewModel.ValidationMessage}");
        }
    }
    
    private void OnCancelClicked()
    {
        Close((BacktestConfigRequest?)null);
    }
    
    protected override void OnCloseButtonClick()
    {
        Close((BacktestConfigRequest?)null);
    }
    
    #endregion
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Prophet.Client.Services;
using Prophet.Client.Services.Editor.Validation;
using Prophet.Client.Services.Strategy;
using Prophet.Client.Core;
using Prophet.Client.Views.Dialogs;
using Prophet.Client.Views.ContextMenus;
using Prophet.Client.ViewModels;
using Prophet.Client.Models;
using Prophet.Client.Controls;

namespace Prophet.Client.Views;

public partial class StrategyView : UserControl, IStrategyEditorHost
{
    private TextEditor? _currentEditor;
    private StrategyListViewModel? _viewModel;
    private readonly LocalStrategyService _strategyService = ServiceContainer.GetService<LocalStrategyService>();
    
    // 策略Tab映射：策略ID -> TabItem
    private readonly Dictionary<string, TabItem> _strategyTabs = new();
    
    // Tab集合（用于动态添加/删除Tab）
    private readonly ObservableCollection<TabItem> _tabItems = new();
    
    
    /// <summary>
    /// Tab中存储的策略数据
    /// </summary>
    private class TabStrategyData
    {
        public StrategyInfo Strategy { get; set; } = null!;
        public string? Remarks { get; set; }
        public TextEditor? Editor { get; set; }
        public CodeEditorHelper? EditorHelper { get; set; } // 保存CodeEditorHelper引用，防止被GC
        public string OriginalDsl { get; set; } = string.Empty; // 原始DSL，用于检测修改
        public bool IsModified { get; set; } = false; // 是否已修改
        public AiAssistantViewModel? AiSession { get; set; } // AI会话（每个Tab独立的会话）
        public EditorDiagnosticsSummary Diagnostics { get; set; } = EditorDiagnosticsSummary.Empty; // 实时诊断信息（状态栏显示）
        public List<EditorDiagnosticItem> DiagnosticItems { get; set; } = new(); // 诊断明细（诊断面板）
        
        // 保存事件处理器引用，用于解绑
        public EventHandler? TextChangedHandler { get; set; }
        public EventHandler<EditorDiagnosticsSummary>? DiagnosticsUpdatedHandler { get; set; }
    }
    
    public StrategyView()
    {
        InitializeComponent();
        SetupKeyBindings();
        
        // 关键：在构造函数中直接初始化（参考 Demo 和 SimpleEditorTest）
        InitializeEditors();
        
        // 初始化ViewModel
        InitializeViewModel();
        
        
        // 组件加载完成后加载策略列表
        Loaded += OnViewLoaded;
    }
    
    private void InitializeViewModel()
    {
        try
        {
            // 离线版：从 DI 获取 ViewModel（保证切页/多入口状态一致）
            _viewModel = ServiceContainer.GetService<StrategyListViewModel>();
            DataContext = _viewModel;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"初始化失败: {ex.Message}");
        }
    }

    private async void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        // 加载策略列表
        if (_viewModel != null)
        {
            await _viewModel.LoadStrategiesAsync();
        }
    }

    /// <summary>
    /// 策略选中事件处理（从左侧列表点击策略时触发）
    /// </summary>
    private void OnStrategySelected(StrategyInfo? strategy)
    {
        if (strategy == null)
            return;
        
        // 检查是否已有该策略的Tab，如果有则切换，如果没有则创建
        if (_strategyTabs.ContainsKey(strategy.Id))
        {
            // 切换到已存在的Tab
            var tabControl = this.FindControl<TabControl>("EditorTabControl");
            if (tabControl != null)
            {
                tabControl.SelectedItem = _strategyTabs[strategy.Id];
            }
        }
        else
        {
            // 创建新Tab（从已保存的策略）
            OpenEditorTab(strategy);
        }
    }

    private void SetupKeyBindings()
    {
        // 设置快捷键
        this.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+S - 保存
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            OnSaveStrategy(this, new RoutedEventArgs());
            e.Handled = true;
        }
        // Ctrl+Z - 撤销
        else if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
        {
            OnUndo(this, new RoutedEventArgs());
            e.Handled = true;
        }
        // Ctrl+Y - 重做
        else if (e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
        {
            OnRedo(this, new RoutedEventArgs());
            e.Handled = true;
        }
        // Shift+Alt+F - 格式化代码
        else if (e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Shift | KeyModifiers.Alt))
        {
            OnFormatCode(this, new RoutedEventArgs());
            e.Handled = true;
        }
        // Ctrl+N - 新建
        else if (e.Key == Key.N && e.KeyModifiers == KeyModifiers.Control)
        {
            OnNewStrategy(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void InitializeEditors()
    {
        // 初始化第一个编辑器（参考 SimpleEditorTest 的成功配置）
        // 编辑器将动态创建，此方法暂时保留为空
        // 当用户创建或打开策略时，会动态创建TextEditor实例
        
        // 绑定TabControl的ItemsSource到ObservableCollection
        var tabControl = this.FindControl<TabControl>("EditorTabControl");
        if (tabControl != null)
        {
            tabControl.ItemsSource = _tabItems;
        }
    }
    
    /// <summary>
    /// 获取示例代码1 - 展示DSL语法
    /// </summary>
    private string GetSampleCode1()
    {
        return @"// 趋势追踪策略 - 使用 Prophet DSL 5.0
// 演示指标、数据函数和信号生成

// 多重条件信号 - 使用 ALL 函数
ALL {
    $(5m).MACD().trend = BULLISH;
    $(5m).RSI().value < 70;
    $(5m).ADX().trend = STRONG;
    KLINE(5m).close(0) > $(5m).EMA().value;
} = BUY;

// 多重条件信号 - 使用 ANY 函数
ANY {
    $(5m).MACD().crossover_type = DEATH_CROSS;
    $(5m).RSI().overbought = true;
    KLINE(5m).close(0) < $(5m).BBANDS().lower;
} = SELL;

// 加权评分信号 - 使用 WEIGHTED 函数
WEIGHTED(0.6) {
    WEIGHT($(15m).MACD().trend = BULLISH) = 0.4;
    WEIGHT($(15m).RSI().level = OVERSOLD) = 0.3;
    WEIGHT(@CURRENT_PRICE > $(1h).EMA().value) = 0.3;
} = BUY(@CURRENT_PRICE + $(5m).ATR().value * 2.0, @CURRENT_PRICE - $(5m).ATR().value * 1.0);

// 提示：
// - 输入 $. 触发指标补全
// - 输入 @ 触发环境变量补全
// - Ctrl+Space 手动触发补全
";
    }
    
    /// <summary>
    /// 获取示例代码2
    /// </summary>
    private string GetSampleCode2()
    {
        return @"// 网格交易策略 - 使用 Prophet DSL 5.0
// 演示更多高级用法

// 波动率过滤
ALL {
    $(5m).ATR().volatility = MEDIUM;
    $(5m).BBANDS().width > 0.02;
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20);
} = BUY;

// 时间过滤示例
ALL {
    @CURRENT_HOUR >= 9;
    @CURRENT_HOUR <= 16;
    @CURRENT_DAY_OF_WEEK != 6;
    @CURRENT_DAY_OF_WEEK != 7;
    $(15m).MACD().trend = BULLISH;
} = BUY;

// 价格突破
ALL {
    KLINE(5m).close(0) > HIGHEST(5m).high(20);
    $(5m).RSI().value > 50;
    KLINE(5m).volume(0) > AVERAGE(5m).volume(20) * 1.5;
} = BUY;
";
    }

    private static int _newStrategyCounter = 0;

    private void OnNewStrategy(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        try
        {
            // 检查Tab数量限制
            if (_tabItems.Count >= 8)
            {
                return;
            }
            
            _newStrategyCounter++;
            
            // 直接创建临时Tab，无需弹窗
            var tempStrategy = new StrategyInfo
            {
                Id = $"temp_{Guid.NewGuid():N}",
                Name = $"新策略{_newStrategyCounter}",
                Dsl = @"// 新策略
ALL {
    $(5m).MACD().trend = BULLISH;
    $(5m).RSI().value < 70;
} = BUY;",
                Status = "draft",
                Version = "未保存",
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            OpenEditorTab(tempStrategy);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"初始化失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 创建新的编辑器Tab
    /// </summary>
    public void OpenEditorTab(StrategyInfo strategy, string? remarks = null)
    {
        try
        {
            var tabControl = this.FindControl<TabControl>("EditorTabControl");
            if (tabControl == null)
            {
                return;
            }

            // 检查是否已存在该策略的Tab
            if (_strategyTabs.ContainsKey(strategy.Id))
            {
                // 切换到已存在的Tab
                var existingTab = _strategyTabs[strategy.Id];
                tabControl.SelectedItem = existingTab;
                return;
            }

            // 创建新的TextEditor
            var editor = new TextEditor
            {
                FontFamily = new FontFamily("Cascadia Code,Consolas,Courier New"),
                FontSize = 14,
                Background = Brushes.Transparent,
                ShowLineNumbers = true,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Padding = new Thickness(0)
            };

            // 设置TextArea背景色
            editor.TextArea.Background = new SolidColorBrush(Color.Parse("#1E2329"));
            editor.TextArea.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            editor.TextArea.TextView.Margin = new Thickness(0);

                // 设置编辑器选项
                editor.Options.ConvertTabsToSpaces = true;
                editor.Options.IndentationSize = 4;
                editor.Options.EnableRectangularSelection = true;
                editor.Options.EnableVirtualSpace = false;

                // 加载DSL代码
                editor.Document = new TextDocument(strategy.Dsl);

            // 集成编辑器增强功能（语法高亮、自动补全、代码片段）
            var editorHelper = new CodeEditorHelper(editor);
            // 创建编辑器容器（无边框，边框由TabControl模板的Content容器提供）
            var editorBorder = new Border
            {
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                Child = new Grid { Children = { editor } }
            };

                // 创建Tab数据
                var tabData = new TabStrategyData
                {
                    Strategy = strategy,
                    Remarks = remarks,
                    Editor = editor,
                    EditorHelper = editorHelper, // 保存引用防止GC
                    OriginalDsl = strategy.Dsl,
                    IsModified = strategy.Id.StartsWith("temp_") // 新策略默认标记为已修改
                };
                
                // 创建TabItem Header（文字居中 + 关闭按钮靠右）
                // 使用Grid叠加布局：文字层居中，按钮容器层靠右
                var headerGrid = new Grid
                {
                    MinWidth = 80,
                    MaxWidth = 150,
                    Height = 35,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };
                
                // 文字层：左对齐显示，预留右侧关闭按钮空间（28px）
                var headerText = new TextBlock
                {
                    Text = GetTabHeaderText(strategy, tabData.IsModified),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 28, 3), // 右侧预留28px给关闭按钮
                    MaxWidth = 122 // MaxWidth(150) - 右侧按钮空间(28) = 122
                };
                
                // 关闭按钮容器：靠右对齐的独立容器
                var closeButtonContainer = new Border
                {
                    Width = 24,
                    Height = 35,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, // 靠右对齐
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, // 垂直居中
                    Background = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 0, 3) // 由于选项卡下留白-3px，所以需要减去3px
                };
                
                // 关闭按钮图标
                var closeIconPath = new Avalonia.Controls.Shapes.Path
                {
                    Data = (StreamGeometry?)Application.Current?.FindResource("IconClose"),
                    Fill = Brushes.Gray,
                    Stretch = Stretch.Uniform
                };
                
                var closeIconViewbox = new Viewbox
                {
                    Width = 10,
                    Height = 10,
                    Child = closeIconPath
                };
                
                // 关闭按钮
                var closeButton = new Button
                {
                    Content = closeIconViewbox,
                    Width = 20,
                    Height = 20,
                    Padding = new Thickness(0),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                
                closeButtonContainer.Child = closeButton;
                
                headerGrid.Children.Add(headerText);
                headerGrid.Children.Add(closeButtonContainer);
                
                // 创建TabItem（内容直接是编辑器，状态栏使用全局的）
                var tabItem = new TabItem
                {
                    Header = headerGrid,
                    Content = editorBorder,
                    Tag = tabData
                };
                
                // 设置ToolTip显示完整策略名称
                Avalonia.Controls.ToolTip.SetTip(tabItem, GetTabTooltipText(strategy));
                
                // 设置关闭按钮事件（在tabItem创建后）
                closeButton.Click += (s, e) => OnCloseTab(tabItem, tabData);
                
                // 监听文本变化（在tabItem创建后）- 保存处理器引用以便解绑
                tabData.TextChangedHandler = (s, e) => OnEditorTextChanged(tabItem, tabData);
                editor.TextChanged += tabData.TextChangedHandler;

                // 监听诊断变化（用于状态栏展示）- 保存处理器引用以便解绑
                if (tabData.EditorHelper != null)
                {
                    tabData.DiagnosticsUpdatedHandler = (_, summary) =>
                    {
                        tabData.Diagnostics = summary;
                        tabData.DiagnosticItems = tabData.EditorHelper.GetDiagnosticsSnapshot().ToList();
                        if (EditorTabControl?.SelectedItem == tabItem)
                        {
                            UpdateGlobalStatusBar(tabData);
                        }
                    };
                    tabData.EditorHelper.DiagnosticsUpdated += tabData.DiagnosticsUpdatedHandler;

                    // 触发一次初始校验，确保状态栏与波浪线立即更新
                    tabData.EditorHelper.ValidateCode();
                    tabData.DiagnosticItems = tabData.EditorHelper.GetDiagnosticsSnapshot().ToList();
                }

            // 添加到TabControl
            _tabItems.Add(tabItem);
            
            // 保存映射
            _strategyTabs[strategy.Id] = tabItem;

            // 切换到新Tab
            if (tabControl != null)
            {
                tabControl.SelectedItem = tabItem;
            }
            
            // 更新当前编辑器
            _currentEditor = editor;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    private async void OnDeleteStrategy(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null)  // 离线版：只检查 _viewModel
        {
            return;
        }

        try
        {
            // 获取当前Tab的策略数据
            var tabControl = this.FindControl<TabControl>("EditorTabControl");
            if (tabControl?.SelectedItem is not TabItem selectedTab)
            {
                Console.WriteLine("❌ 没有选中的Tab");
                return;
            }

            if (selectedTab.Tag is not TabStrategyData tabData)
            {
                Console.WriteLine("❌ Tab数据无效");
                return;
            }

            var strategy = tabData.Strategy;
            
            // 临时策略直接关闭Tab
            if (strategy.Id.StartsWith("temp_"))
            {
                if (tabControl.Items is System.Collections.IList items)
                {
                    items.Remove(selectedTab);
                }
                _strategyTabs.Remove(strategy.Id);
                return;
            }

            // 确认对话框
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null)
            {
                return;
            }

            bool confirmed = false;
            var dialog = ModernDialogPresets.Confirm(
                "确认删除策略",
                $"确定要删除策略「{strategy.Name}」吗？\n\n此操作不可撤销！",
                result => confirmed = result
            );

            await dialog.ShowDialog(window);

            if (!confirmed)
            {
                return;
            }

        // 离线版：从本地数据库删除
        var success = await _strategyService.DeleteStrategyAsync(strategy.Id);

            if (success)
            {
                
                // 关闭Tab
                if (tabControl.Items is System.Collections.IList items)
                {
                    items.Remove(selectedTab);
                }
                _strategyTabs.Remove(strategy.Id);
                
                // 刷新策略列表
                await _viewModel.LoadStrategiesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"初始化失败: {ex.Message}");
        }
    }
    
    private async void OnSaveStrategy(object? sender, RoutedEventArgs e)
    {
        if (_currentEditor == null || _viewModel == null)  // 离线版：只检查必要的对象
        {
            Console.WriteLine("❌ 编辑器、API客户端或视图模型未初始化");
            return;
        }

        try
        {
            var tabControl = this.FindControl<TabControl>("EditorTabControl");
            if (tabControl?.SelectedItem is not TabItem selectedTab)
            {
                Console.WriteLine("❌ 没有选中的Tab");
                return;
            }

            if (selectedTab.Tag is not TabStrategyData tabData)
            {
                Console.WriteLine("❌ Tab数据无效");
                return;
            }

            var strategy = tabData.Strategy;
            var dsl = tabData.Editor?.Text ?? _currentEditor.Text;

            Console.WriteLine($"🔄 准备保存策略: {strategy.Name}");
            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // 1. 验证DSL语法
            Console.WriteLine($"🔍 开始验证DSL语法...");
            var validator = ServiceContainer.GetService<DSLValidator>();
            var validationResult = validator.Validate(dsl);
            
            // 输出验证结果到终端
            Console.WriteLine($"📊 验证结果统计:");
            Console.WriteLine($"   - 总行数: {dsl.Split('\n').Length}");
            Console.WriteLine($"   - 错误数: {validationResult.Errors.Count}");
            Console.WriteLine($"   - 警告数: {validationResult.Warnings.Count}");
            Console.WriteLine($"   - 验证状态: {(validationResult.IsValid ? "✅ 通过" : "❌ 失败")}");
            
            // 输出所有错误信息
            if (validationResult.Errors.Count > 0)
            {
                Console.WriteLine($"\n❌ 发现 {validationResult.Errors.Count} 个语法错误:");
                foreach (var error in validationResult.Errors)
                {
                    Console.WriteLine($"   第 {error.Line} 行，列 {error.Column}: {error.Message}");
                    if (error.Length > 0)
                    {
                        Console.WriteLine($"      错误长度: {error.Length} 个字符");
                    }
                }
            }
            
            // 输出所有警告信息
            if (validationResult.Warnings.Count > 0)
            {
                Console.WriteLine($"\n⚠️  发现 {validationResult.Warnings.Count} 个警告:");
                foreach (var warning in validationResult.Warnings)
                {
                    Console.WriteLine($"   第 {warning.Line} 行，列 {warning.Column}: {warning.Message}");
                }
            }
            
            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            if (!validationResult.IsValid)
            {
                Console.WriteLine($"❌ 策略代码存在语法错误，无法保存");
                Console.WriteLine($"   错误数量: {validationResult.Errors.Count}");
                
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window != null)
                {
                    // 格式化错误信息，包含行号和列号，并翻译错误类型
                    var errorMessages = string.Join("\n", validationResult.Errors.Take(10).Select(e => 
                    {
                        // 翻译错误类型
                        string errorType = e.Message.StartsWith("[PARSER]") ? "语法错误" :
                                         e.Message.StartsWith("[LEXER]") ? "词法错误" :
                                         e.Message.StartsWith("[DSL]") ? "DSL错误" :
                                         e.Message.StartsWith("[VALIDATION]") ? "验证错误" :
                                         e.Message.StartsWith("[EXCEPTION]") ? "异常错误" : "错误";
                        
                        // 提取错误描述（移除类型前缀）
                        string description = e.Message;
                        var typeMatch = System.Text.RegularExpressions.Regex.Match(description, @"^\[(\w+)\]\s*(.+)");
                        if (typeMatch.Success)
                        {
                            description = typeMatch.Groups[2].Value.Trim();
                        }
                        
                        // 格式化位置信息
                        if (e.Line > 0 && e.Column > 0)
                        {
                            return $"• 第{e.Line}行, 第{e.Column}列 [{errorType}]: {description}";
                        }
                        else if (e.Line > 0)
                        {
                            return $"• 第{e.Line}行 [{errorType}]: {description}";
                        }
                        else
                        {
                            return $"• [{errorType}]: {description}";
                        }
                    }));
                    
                    if (validationResult.Errors.Count > 10)
                    {
                        errorMessages += $"\n... 还有 {validationResult.Errors.Count - 10} 个错误";
                    }
                    
                    // 根据错误消息长度动态调整对话框高度
                    var lineCount = errorMessages.Split('\n').Length;
                    var dialogHeight = Math.Max(300, Math.Min(700, 350 + lineCount * 22));
                    
                    var errorDialog = new ModernDialog
                    {
                        Title = "DSL语法错误",
                        Width = 600,
                        Height = dialogHeight
                    };
                    
                    var titleText = new TextBlock
                    {
                        Text = "⚠️ DSL语法错误",
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#F59E0B"))
                    };
                    
                    // 提示文本
                    var promptText = new TextBlock
                    {
                        Text = "策略代码存在语法错误，请修复后再保存：",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    
                    // 可复制的错误内容文本框（带圆角边框和浅色背景）
                    var errorTextBox = new TextBox
                    {
                        Text = errorMessages,
                        TextWrapping = TextWrapping.Wrap,
                        IsReadOnly = true,
                        IsEnabled = true,
                        Background = Brushes.Transparent, // 透明背景，由外层Border提供
                        Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
                        BorderThickness = new Thickness(0), // 移除TextBox的边框，使用外层Border
                        Padding = new Thickness(12, 10),
                        FontFamily = new FontFamily("Consolas, Courier New, monospace"), // 等宽字体，便于阅读
                        FontSize = 13,
                        AcceptsReturn = true,
                        AcceptsTab = false,
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Ibeam), // 文本光标
                        Classes = { "error-text-box" } // 添加类名用于样式定位
                    };
                    
                    // 保存初始样式值
                    var initialBackground = Brushes.Transparent;
                    var initialBorderBrush = Brushes.Transparent;
                    var initialBorderThickness = new Thickness(0);
                    
                    // 使用 PropertyChanged 事件持续监控并重置样式，确保焦点状态下样式不变
                    errorTextBox.PropertyChanged += (s, e) =>
                    {
                        if (s is TextBox tb)
                        {
                            // 如果背景、边框或边框厚度被改变，立即重置为初始值
                            if (e.Property == TextBox.BackgroundProperty && tb.Background != initialBackground)
                            {
                                tb.Background = initialBackground;
                            }
                            else if (e.Property == TextBox.BorderBrushProperty && tb.BorderBrush != initialBorderBrush)
                            {
                                tb.BorderBrush = initialBorderBrush;
                            }
                            else if (e.Property == TextBox.BorderThicknessProperty && tb.BorderThickness != initialBorderThickness)
                            {
                                tb.BorderThickness = initialBorderThickness;
                            }
                        }
                    };
                    
                    // 处理焦点事件，确保焦点状态下样式不变
                    errorTextBox.GotFocus += (s, e) =>
                    {
                        if (s is TextBox tb)
                        {
                            // 延迟重置，确保在样式系统应用焦点样式后重置
                            Dispatcher.UIThread.Post(() =>
                            {
                                tb.Background = initialBackground;
                                tb.BorderBrush = initialBorderBrush;
                                tb.BorderThickness = initialBorderThickness;
                            }, DispatcherPriority.Render);
                        }
                    };
                    
                    errorTextBox.LostFocus += (s, e) =>
                    {
                        if (s is TextBox tb)
                        {
                            tb.Background = initialBackground;
                            tb.BorderBrush = initialBorderBrush;
                            tb.BorderThickness = initialBorderThickness;
                        }
                    };
                    
                    // 使用ScrollViewer包装TextBox以支持滚动
                    var scrollViewer = new ScrollViewer
                    {
                        Content = errorTextBox,
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                        MaxHeight = 400 // 限制最大高度，超出部分可滚动
                    };
                    
                    // 使用Border包装ScrollViewer以实现圆角边框和浅色背景
                    var errorContainer = new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#2A2D35")), // 浅色背景
                        BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")), // 边框颜色
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6), // 圆角
                        Padding = new Thickness(0),
                        ClipToBounds = true, // 确保内容不会超出圆角边界
                        Child = scrollViewer
                    };
                    
                    errorDialog.SetContent(titleText, promptText, errorContainer);
                    errorDialog.AddButton("确定", () => errorDialog.Close());
                    
                    await errorDialog.ShowDialog(window);
                }
                return;
            }
            
            // 验证通过
            Console.WriteLine($"✅ DSL语法验证通过，准备保存策略...");

            // 2. 检查是否是新策略（临时ID以temp_开头）
            if (strategy.Id.StartsWith("temp_"))
            {
                await SaveNewStrategy(selectedTab, tabData, dsl);
            }
            else
            {
                // 保存已存在的策略（创建新版本）
                await SaveExistingStrategy(selectedTab, tabData, dsl);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 保存策略失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window != null)
            {
                var errorDialog = ModernDialogPresets.Warning(
                    "保存失败",
                    $"保存策略时发生异常：{ex.Message}"
                );
                await errorDialog.ShowDialog(window);
            }
        }
    }

    /// <summary>
    /// 保存新策略
    /// </summary>
    private async Task SaveNewStrategy(TabItem selectedTab, TabStrategyData tabData, string dsl)
    {
        var strategy = tabData.Strategy;
        
        // 使用ModernDialog弹窗输入策略信息
        string? strategyName = null;
        string? remarks = null;
        
        var dialog = ModernDialogPresets.StrategyForm((name, sym, rem) =>
        {
            strategyName = name;
            remarks = rem;
        });
        
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
        {
            Console.WriteLine("❌ 无法找到父窗口");
            return;
        }
        
        var result = await dialog.ShowDialog<bool>(window);

        if (!result || string.IsNullOrEmpty(strategyName))
        {
            Console.WriteLine("⚠️ 用户取消了保存");
            return;
        }

        Console.WriteLine($"🔄 创建新策略: {strategyName}");

        // 离线版：使用本地服务创建策略
        var createdStrategy = new StrategyInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = strategyName,
            Dsl = dsl,
            Remarks = remarks,
            Status = "draft",
            UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };
        
        await _strategyService.CreateStrategyAsync(createdStrategy);

        if (createdStrategy != null)
        {
            Console.WriteLine($"✅ 策略「{strategyName}」创建成功");
            
            tabData.Strategy = createdStrategy;
            tabData.Remarks = null;
            tabData.OriginalDsl = dsl; // 更新原始DSL
            tabData.IsModified = false; // 标记为未修改
            
            // 更新Tab标题
            if (selectedTab.Header is Grid headerGrid && headerGrid.Children[0] is TextBlock headerText)
            {
                headerText.Text = GetTabHeaderText(createdStrategy, false);
            }
            
            // 更新策略Tab映射
            _strategyTabs.Remove(strategy.Id);
            _strategyTabs[createdStrategy.Id] = selectedTab;
            
            // 刷新策略列表
            if (_viewModel != null)
            {
                await _viewModel.LoadStrategiesAsync();
            }
            
            // 显示成功提示
            var successDialog = ModernDialogPresets.Info(
                "保存成功",
                $"策略「{strategyName}」已成功创建！"
            );
            await successDialog.ShowDialog(window);
        }
        else
        {
            Console.WriteLine($"❌ 创建策略失败");
            
            var errorDialog = ModernDialogPresets.Warning(
                "保存失败",
                "无法创建策略，请检查网络连接或稍后重试。"
            );
            await errorDialog.ShowDialog(window);
        }
    }

    /// <summary>
    /// 保存已存在的策略（创建新版本）
    /// </summary>
    private async Task SaveExistingStrategy(TabItem selectedTab, TabStrategyData tabData, string dsl)
    {
        var strategy = tabData.Strategy;
        
        // 检查是否有修改
        if (!tabData.IsModified && dsl == tabData.OriginalDsl)
        {
            Console.WriteLine("⚠️ 策略代码未修改，无需保存");
            
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window != null)
            {
                var infoDialog = ModernDialogPresets.Info(
                    "无需保存",
                    "策略代码未修改。"
                );
                await infoDialog.ShowDialog(window);
            }
            return;
        }

        Console.WriteLine($"🔄 保存策略: {strategy.Name}");

        // 离线版：直接更新策略DSL
        strategy.Dsl = dsl;
        strategy.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var success = await _strategyService.UpdateStrategyAsync(strategy);

        if (success)
        {
            Console.WriteLine($"✅ 策略「{strategy.Name}」保存成功");
            
            // 更新策略信息
            strategy.Dsl = dsl;
            strategy.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            tabData.OriginalDsl = dsl; // 更新原始DSL
            tabData.IsModified = false; // 标记为未修改
            
            // 更新Tab标题（移除修改标记 *）
            if (selectedTab.Header is Grid headerGrid && headerGrid.Children[0] is TextBlock headerText)
            {
                headerText.Text = GetTabHeaderText(strategy, false);
            }
            
            // 刷新策略列表
            if (_viewModel != null)
            {
                await _viewModel.LoadStrategiesAsync();
            }
            
            Console.WriteLine($"✅ 策略已保存");
        }
        else
        {
            Console.WriteLine($"❌ 保存策略失败");
            
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window != null)
            {
                var errorDialog = ModernDialogPresets.Warning(
                    "保存失败",
                    "无法保存策略版本，请检查网络连接或稍后重试。"
                );
                await errorDialog.ShowDialog(window);
            }
        }
    }


    private void OnUndo(object? sender, RoutedEventArgs e)
    {
        if (_currentEditor != null && _currentEditor.CanUndo)
        {
            _currentEditor.Undo();
        }
    }

    private void OnRedo(object? sender, RoutedEventArgs e)
    {
        if (_currentEditor != null && _currentEditor.CanRedo)
        {
            _currentEditor.Redo();
        }
    }

    private void OnFormatCode(object? sender, RoutedEventArgs e)
    {
        if (_currentEditor == null)
        {
            return;
        }

        try
        {
            var tabControl = this.FindControl<TabControl>("EditorTabControl");
            if (tabControl?.SelectedItem is not TabItem selectedTab)
            {
                return;
            }

            if (selectedTab.Tag is not TabStrategyData tabData)
            {
                return;
            }

            // 获取当前代码
            var currentCode = _currentEditor.Text;
            
            // 格式化代码
            var formattedCode = DSLFormatter.Format(currentCode);
            
            // 如果格式化后的代码与当前代码不同，则替换
            if (currentCode != formattedCode)
            {
                var selectionStart = _currentEditor.SelectionStart;
                var selectionLength = _currentEditor.SelectionLength;
                
                _currentEditor.Text = formattedCode;
                
                // 恢复光标位置（尽量保持）
                if (selectionStart <= formattedCode.Length)
                {
                    _currentEditor.CaretOffset = selectionStart;
                }
                else
                {
                    _currentEditor.CaretOffset = formattedCode.Length;
                }
                
                // 标记为已修改
                tabData.IsModified = true;
                OnEditorTextChanged(selectedTab, tabData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 格式化失败: {ex.Message}");
        }
    }

    private void OnInsertSnippet(object? sender, RoutedEventArgs e)
    {
        // 获取当前选中的Tab
        if (EditorTabControl?.SelectedItem is TabItem selectedTab)
        {
            if (selectedTab.Tag is TabStrategyData tabData && tabData.EditorHelper != null)
            {
                tabData.EditorHelper.ShowSnippetPicker();
            }
        }
    }

    
    /// <summary>
    /// 打开 AI 会话弹窗
    /// </summary>
    private async void OnToggleAiAssistant(object? sender, RoutedEventArgs e)
    {
        try
        {
            // 获取当前选中的Tab
            var tabControl = this.FindControl<TabControl>("EditorTabControl");
            if (tabControl?.SelectedItem is not TabItem selectedTab)
            {
                Console.WriteLine("❌ 没有选中的策略Tab，无法打开AI助手");
                return;
            }
            
            if (selectedTab.Tag is not TabStrategyData tabData)
            {
                Console.WriteLine("❌ Tab数据无效");
                return;
            }
            
            // 如果该Tab还没有AI会话，创建一个新的
            if (tabData.AiSession == null)
            {
                tabData.AiSession = new AiAssistantViewModel();
                Console.WriteLine($"✅ 为策略「{tabData.Strategy.Name}」创建新的AI会话");
            }
            else
            {
                Console.WriteLine($"✅ 复用策略「{tabData.Strategy.Name}」的现有AI会话");
            }
            
            // 创建 AI 会话弹窗
            var aiChatDialog = new AiChatDialog();
            
            // 设置会话上下文
            aiChatDialog.SetSessionContext(
                sessionTitle: $"AI助手 - {tabData.Strategy.Name}",
                viewModel: tabData.AiSession,
                strategyView: this
            );
            
            // 获取父窗口
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                // 显示弹窗
                await aiChatDialog.ShowDialog(parentWindow);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"打开AI会话弹窗失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取当前编辑器文本（供AI助手使用）
    /// </summary>
    public string? GetCurrentEditorText()
    {
        return _currentEditor?.Text;
    }
    
    /// <summary>
    /// 在光标位置插入文本（供AI助手使用）
    /// </summary>
    public void InsertTextAtCursor(string text)
    {
        if (_currentEditor != null)
        {
            var offset = _currentEditor.CaretOffset;
            _currentEditor.Document.Insert(offset, text);
            
            // 移动光标到插入内容后
            _currentEditor.CaretOffset = offset + text.Length;
            
            // 聚焦到编辑器
            _currentEditor.Focus();
        }
    }
    
    /// <summary>
    /// 替换当前编辑器代码（供AI助手使用）
    /// </summary>
    public void ReplaceCurrentCode(string newCode)
    {
        if (_currentEditor != null)
        {
            _currentEditor.Document.Text = newCode;
            _currentEditor.Focus();
            
            // 标记当前Tab为已修改
            var tabControl = this.FindControl<TabControl>("EditorTabControl");
            if (tabControl?.SelectedItem is TabItem selectedTab && selectedTab.Tag != null)
            {
                var isModifiedProperty = selectedTab.Tag.GetType().GetProperty("IsModified");
                if (isModifiedProperty != null)
                {
                    isModifiedProperty.SetValue(selectedTab.Tag, true);
                }
            }
        }
    }
    
    /// <summary>
    /// 从AI生成的代码创建新策略Tab（供AI助手使用）
    /// </summary>
    /// <param name="strategyName">策略名称</param>
    /// <param name="dslCode">DSL代码</param>
    public void CreateStrategyFromAiCode(string strategyName, string dslCode)
    {
        try
        {
            // 检查Tab数量限制
            if (_tabItems.Count >= 8)
            {
                Console.WriteLine("⚠️ Tab数量已达上限（8个）");
                return;
            }
            
            // 创建临时策略对象
            var tempStrategy = new StrategyInfo
            {
                Id = $"temp_{Guid.NewGuid():N}",
                Name = strategyName,
                Dsl = dslCode,
                Status = "draft",
                Version = "未保存",
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 创建编辑器Tab
            OpenEditorTab(tempStrategy);
            
            Console.WriteLine($"✅ AI助手创建了新策略Tab: {strategyName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建策略Tab失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取当前策略信息（供AI助手使用）
    /// </summary>
    public (StrategyInfo? strategy, string? dsl) GetCurrentStrategyInfo()
    {
        var tabControl = this.FindControl<TabControl>("EditorTabControl");
        if (tabControl?.SelectedItem is not TabItem selectedTab)
            return (null, null);
        
        var tabData = selectedTab.Tag;
        if (tabData == null)
            return (null, null);
        
        var strategyProperty = tabData.GetType().GetProperty("Strategy");
        var editorProperty = tabData.GetType().GetProperty("Editor");
        
        if (strategyProperty?.GetValue(tabData) is StrategyInfo strategy &&
            editorProperty?.GetValue(tabData) is AvaloniaEdit.TextEditor editor)
        {
            return (strategy, editor.Text);
        }
        
        return (null, null);
    }

    /// <summary>
    /// 策略卡片点击事件（仅选中，不打开编辑器）
    /// </summary>
    private void OnStrategyCardClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is StrategyInfo strategy)
        {
            if (_viewModel != null)
            {
                _viewModel.SelectedStrategy = strategy;
            }
        }
    }
    
    /// <summary>
    /// 策略卡片双击事件（选中 + 打开编辑器）
    /// </summary>
    private void OnStrategyCardDoubleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is StrategyInfo strategy)
        {
            // 先设置选中状态
            if (_viewModel != null)
            {
                _viewModel.SelectedStrategy = strategy;
            }
            // 再打开编辑器
            OpenStrategyForEdit(strategy);
        }
    }
    
    /// <summary>
    /// 打开策略进行编辑
    /// </summary>
    private async void OpenStrategyForEdit(StrategyInfo strategy)
    {
        // 检查Tab数量限制
        if (_tabItems.Count >= 8)
        {
            return;
        }
        
        var tabControl = this.FindControl<TabControl>("EditorTabControl");
        
        // 检查是否已打开
        if (_strategyTabs.ContainsKey(strategy.Id))
        {
            // 切换到已存在的Tab
            if (tabControl != null)
            {
                tabControl.SelectedItem = _strategyTabs[strategy.Id];
            }
        }
        else
        {
            // 获取最新版本的DSL代码
            await LoadLatestVersionAndCreateTab(strategy);
        }
    }
    
    /// <summary>
    /// 加载最新版本的DSL代码并创建Tab
    /// </summary>
    private async Task LoadLatestVersionAndCreateTab(StrategyInfo strategy)
    {
        // 离线版：策略已在本地，无需额外检查，直接创建编辑器Tab
        await Task.CompletedTask; // 保持异步签名
        OpenEditorTab(strategy);
    }
    

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 根据选中的Tab更新当前编辑器
        if (EditorTabControl?.SelectedItem is TabItem selectedTab)
        {
            if (selectedTab.Tag is TabStrategyData tabData && tabData.Editor != null)
            {
                _currentEditor = tabData.Editor;
                
                // 更新全局状态栏
                UpdateGlobalStatusBar(tabData);
                
                // 更新选中的策略（如果不是临时策略）
                if (_viewModel != null)
                {
                    if (tabData.Strategy.Id.StartsWith("temp_"))
                    {
                        // 临时策略，清除选中状态
                        _viewModel.SelectedStrategy = null;
                        Console.WriteLine("临时策略，清除所有卡片选中状态");
                    }
                    else
                    {
                        // 已保存的策略，更新选中状态
                        var strategy = _viewModel.AllStrategies.FirstOrDefault(s => s.Id == tabData.Strategy.Id);
                        if (strategy != null)
                        {
                            _viewModel.SelectedStrategy = strategy;
                            Console.WriteLine($"更新卡片选中状态: {strategy.Name}");
                        }
                    }
                    
                    // 卡片选中状态通过IsSelected属性自动更新
                }
            }
        }
        else
        {
            // 没有Tab时，清空状态栏
            UpdateGlobalStatusBar(null);
        }
    }
    
    /// <summary>
    /// 更新全局状态栏
    /// </summary>
    private void UpdateGlobalStatusBar(TabStrategyData? tabData)
    {
        try
        {
            var leftText = this.FindControl<TextBlock>("StatusBarLeftText");
            var rightText = this.FindControl<TextBlock>("StatusBarRightText");
            var diagnosticsButtonText = this.FindControl<TextBlock>("DiagnosticsButtonText");
            
            if (leftText != null && rightText != null)
            {
                if (tabData != null)
                {
                    var strategy = tabData.Strategy;
                    var statusText = strategy.Id.StartsWith("temp_") 
                        ? $"{strategy.Name} | 未保存 | UTF-8"
                        : $"{strategy.Name} | ID: {strategy.Id} | 版本: {strategy.Version} | UTF-8";

                    var diagnosticsText = BuildDiagnosticsStatusText(tabData.Diagnostics);
                    leftText.Text = string.IsNullOrEmpty(diagnosticsText)
                        ? statusText
                        : $"{statusText} | {diagnosticsText}";
                    rightText.Text = "Prophet DSL";
                    
                    if (diagnosticsButtonText != null)
                    {
                        var totalIssues = tabData.Diagnostics.ErrorCount +
                                          tabData.Diagnostics.WarningCount +
                                          tabData.Diagnostics.InfoCount +
                                          tabData.Diagnostics.UndefinedSymbolCount;
                        diagnosticsButtonText.Text = totalIssues > 0 ? $"诊断({totalIssues})" : "诊断";
                    }
                }
                else
                {
                    leftText.Text = "就绪";
                    rightText.Text = "Prophet DSL";
                    if (diagnosticsButtonText != null)
                    {
                        diagnosticsButtonText.Text = "诊断";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"初始化失败: {ex.Message}");
        }
    }

    private async void OnShowDiagnostics(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (EditorTabControl?.SelectedItem is not TabItem selectedTab)
                return;

            if (selectedTab.Tag is not TabStrategyData tabData || tabData.Editor == null || tabData.EditorHelper == null)
                return;

            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null)
                return;

            var items = tabData.DiagnosticItems ?? new List<EditorDiagnosticItem>();
            var dialog = new DiagnosticsPanelDialog(tabData.Strategy.Name, items, tabData.Editor);
            await dialog.ShowDialog(window);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"打开诊断面板失败: {ex.Message}");
        }
    }

    private string BuildDiagnosticsStatusText(EditorDiagnosticsSummary summary)
    {
        if (summary.ErrorCount == 0 &&
            summary.WarningCount == 0 &&
            summary.InfoCount == 0 &&
            summary.UndefinedSymbolCount == 0)
        {
            return "✅ 语法检查通过";
        }

        var parts = new List<string>();
        if (summary.ErrorCount > 0) parts.Add($"错误 {summary.ErrorCount}");
        if (summary.WarningCount > 0) parts.Add($"警告 {summary.WarningCount}");
        if (summary.InfoCount > 0) parts.Add($"提示 {summary.InfoCount}");
        if (summary.UndefinedSymbolCount > 0) parts.Add($"未定义 {summary.UndefinedSymbolCount}");

        return string.Join(" | ", parts);
    }

    private void OnSearchBoxGotFocus(object? sender, RoutedEventArgs e)
    {
        // 获得焦点时改变边框颜色和粗细
        var border = this.FindControl<Border>("SearchBoxBorder");
        if (border != null)
        {
            border.BorderBrush = new SolidColorBrush(Color.Parse("#0B0E11"));
            border.BorderThickness = new Avalonia.Thickness(1);
        }
    }

    // 右键菜单事件处理
    private void OnEditStrategyFromCard(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            // 先设置选中状态
            if (_viewModel != null)
            {
                _viewModel.SelectedStrategy = strategy;
            }
            // 再打开编辑器
            OpenStrategyForEdit(strategy);
            Console.WriteLine($"右键菜单打开策略编辑器: {strategy.Name}");
        }
    }
    
    private async void OnEnableStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not StrategyInfo strategy)
            return;

        Console.WriteLine($"🔄 准备激活策略: {strategy.Name}");

        try
        {
            // 1. 获取策略的DSL代码
            var dslCode = strategy.Dsl;
            if (string.IsNullOrWhiteSpace(dslCode))
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」的DSL代码为空，无法激活");
                
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window != null)
                {
                    var errorDialog = ModernDialogPresets.Warning(
                        "无法激活策略",
                        "策略的DSL代码为空，请先编写策略代码。"
                    );
                    await errorDialog.ShowDialog(window);
                }
                return;
            }

            // 2. 验证DSL语法
            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"🔍 开始验证DSL语法（激活策略）...");
            var validator = ServiceContainer.GetService<DSLValidator>();
            var validationResult = validator.Validate(dslCode);
            
            // 输出验证结果到终端
            Console.WriteLine($"📊 验证结果统计:");
            Console.WriteLine($"   - 总行数: {dslCode.Split('\n').Length}");
            Console.WriteLine($"   - 错误数: {validationResult.Errors.Count}");
            Console.WriteLine($"   - 警告数: {validationResult.Warnings.Count}");
            Console.WriteLine($"   - 验证状态: {(validationResult.IsValid ? "✅ 通过" : "❌ 失败")}");
            
            // 输出所有错误信息
            if (validationResult.Errors.Count > 0)
            {
                Console.WriteLine($"\n❌ 发现 {validationResult.Errors.Count} 个语法错误:");
                foreach (var error in validationResult.Errors)
                {
                    Console.WriteLine($"   第 {error.Line} 行，列 {error.Column}: {error.Message}");
                    if (error.Length > 0)
                    {
                        Console.WriteLine($"      错误长度: {error.Length} 个字符");
                    }
                }
            }
            
            // 输出所有警告信息
            if (validationResult.Warnings.Count > 0)
            {
                Console.WriteLine($"\n⚠️  发现 {validationResult.Warnings.Count} 个警告:");
                foreach (var warning in validationResult.Warnings)
                {
                    Console.WriteLine($"   第 {warning.Line} 行，列 {warning.Column}: {warning.Message}");
                }
            }
            
            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            if (!validationResult.IsValid)
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」的DSL代码存在语法错误，无法激活");
                
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window != null)
                {
                    var errorMessages = string.Join("\n", validationResult.Errors.Take(5).Select(e => $"• 第{e.Line}行: {e.Message}"));
                    if (validationResult.Errors.Count > 5)
                    {
                        errorMessages += $"\n... 还有 {validationResult.Errors.Count - 5} 个错误";
                    }
                    
                    // 根据错误消息长度动态调整对话框高度
                    var lineCount = errorMessages.Split('\n').Length;
                    var dialogHeight = Math.Max(250, Math.Min(500, 180 + lineCount * 25));
                    
                    var errorDialog = new ModernDialog
                    {
                        Title = "DSL语法错误",
                        Width = 500,
                        Height = dialogHeight
                    };
                    
                    var titleText = new TextBlock
                    {
                        Text = "⚠️ DSL语法错误",
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#F59E0B"))
                    };
                    
                    var messageText = new TextBlock
                    {
                        Text = "策略代码存在语法错误，请修复后再激活：\n\n" + errorMessages,
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 22,
                        Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
                    };
                    
                    errorDialog.SetContent(titleText, messageText);
                    errorDialog.AddButton("确定", () => errorDialog.Close());
                    
                    await errorDialog.ShowDialog(window);
                }
                return;
            }
            
            // 验证通过
            Console.WriteLine($"✅ DSL语法验证通过，准备激活策略...");

            // 3. 创建激活对话框
            var activationDialog = new StrategyActivationDialog();
            activationDialog.Initialize(dslCode);

            // 创建ModernDialog包装
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            var dialogWindow = ModernDialogPresets.CustomContent(activationDialog, 800, 600);
            
            // 设置标题（如果可能）
            if (dialogWindow is ModernDialog modernDialog)
            {
                modernDialog.Title = "激活策略";
            }

            // 4. 显示对话框并等待用户操作
            var result = await dialogWindow.ShowDialog<bool?>(parentWindow!);

            if (result != true)
            {
                Console.WriteLine($"⚠️ 用户取消了激活策略「{strategy.Name}」");
                return;
            }

            // 5. 获取配置好的指标参数
            var configuredIndicators = activationDialog.GetConfiguredIndicators();
            Console.WriteLine($"✅ 用户已配置 {configuredIndicators.Count} 个指标");

            // 6. 离线版：暂不支持激活策略
            Console.WriteLine($"⚠️ 离线版暂不支持激活策略功能");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 激活策略异常: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window != null)
            {
                var errorDialog = ModernDialogPresets.Warning(
                    "激活失败",
                    $"激活策略时发生异常：{ex.Message}"
                );
                await errorDialog.ShowDialog(window);
            }
        }
    }
    
    private async void OnDeprecateStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            Console.WriteLine($"废弃策略: {strategy.Name}");
            
            // 检查策略状态：只有激活状态才能废弃
            if (!strategy.CanDeprecate)
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」不能废弃");
                await ShowWarningAsync("无法废弃", "只有激活状态的策略才能废弃。");
                return;
            }
            
            // 显示废弃对话框
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;
            
            var dialog = new StrategyDeprecationDialog(strategy);
            var dialogWindow = new Window
            {
                Content = dialog,
                Title = "废弃策略",
                Width = 500,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            
            var result = await dialogWindow.ShowDialog<bool?>(window);
            
            if (result == true && !string.IsNullOrEmpty(dialog.Reason))
            {
                var success = await _viewModel?.DeprecateStrategyAsync(strategy, dialog.Reason)!;
                if (success)
                {
                    Console.WriteLine($"✅ 策略「{strategy.Name}」已废弃");
                    await ShowInfoAsync("废弃成功", $"策略「{strategy.Name}」已标记为废弃状态。");
                }
                else
                {
                    Console.WriteLine($"❌ 废弃策略失败");
                    await ShowErrorAsync("废弃失败", "废弃策略时发生错误，请稍后重试。");
                }
            }
        }
    }
    
    private async void OnArchiveStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            Console.WriteLine($"归档策略: {strategy.Name}");
            
            // 检查策略状态
            if (!strategy.CanArchive)
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」不能归档");
                await ShowWarningAsync("无法归档", "该策略当前状态不允许归档。");
                return;
            }
            
            // 显示归档对话框
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;
            
            var dialog = new StrategyArchiveDialog(strategy);
            var dialogWindow = new Window
            {
                Content = dialog,
                Title = "归档策略",
                Width = 520,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            
            var result = await dialogWindow.ShowDialog<bool?>(window);
            
            if (result == true && !string.IsNullOrEmpty(dialog.Reason))
            {
                var success = await _viewModel?.ArchiveStrategyAsync(strategy, dialog.Reason)!;
                if (success)
                {
                    Console.WriteLine($"✅ 策略「{strategy.Name}」已归档");
                    await ShowInfoAsync("归档成功", $"策略「{strategy.Name}」已归档。");
                }
                else
                {
                    Console.WriteLine($"❌ 归档策略失败");
                    await ShowErrorAsync("归档失败", "归档策略时发生错误，请稍后重试。");
                }
            }
        }
    }
    
    private async void OnRecoverStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            Console.WriteLine($"恢复策略: {strategy.Name}");
            
            // 确认恢复
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;
            
            bool confirmed = false;
            var confirmDialog = ModernDialogPresets.Confirm(
                "恢复策略",
                $"确认要恢复策略「{strategy.Name}」吗？\n\n恢复后策略将变为激活状态。",
                (result) => confirmed = result
            );
            
            await confirmDialog.ShowDialog(window);
            if (confirmed)
            {
                var success = await _viewModel?.RecoverStrategyAsync(strategy)!;
                if (success)
                {
                    Console.WriteLine($"✅ 策略「{strategy.Name}」已恢复");
                    await ShowInfoAsync("恢复成功", $"策略「{strategy.Name}」已恢复为激活状态。");
                }
                else
                {
                    Console.WriteLine($"❌ 恢复策略失败");
                    await ShowErrorAsync("恢复失败", "恢复策略时发生错误，请稍后重试。");
                }
            }
        }
    }
    
    private async void OnViewStrategyDetails(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            Console.WriteLine($"查看策略详情: {strategy.Name}");
            
            // 显示异常详情对话框
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;
            
            var dialog = new StrategyAnomalyDialog(strategy);
            var dialogWindow = new Window
            {
                Content = dialog,
                Title = "策略异常详情",
                Width = 550,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            
            await dialogWindow.ShowDialog(window);
            
            // 如果用户点击了"尝试修复"，则打开编辑器
            if (dialog.ShouldFix)
            {
                // 打开策略编辑（调用现有的编辑方法）
                Console.WriteLine("用户选择尝试修复，打开编辑器");
                // TODO: 调用编辑方法
            }
        }
    }
    
    private async void OnRenameStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            // 检查策略状态：只有草稿状态（enabled=false，即status="draft"）才能重命名
            if (strategy.Status != "draft")
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」不是草稿状态，无法重命名");
                
                // 显示警告对话框
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window == null) return;
                
                var warningDialog = ModernDialogPresets.Warning(
                    "无法重命名",
                    "只有草稿状态的策略才能重命名。\n\n请先将策略设置为草稿状态。"
                );
                
                await warningDialog.ShowDialog(window);
                return;
            }
            
            // 显示重命名对话框
            await ShowRenameDialogAsync(strategy);
        }
    }
    
    /// <summary>
    /// 显示重命名对话框
    /// </summary>
    private async Task ShowRenameDialogAsync(StrategyInfo strategy)
    {
        if (_viewModel == null)  // 离线版：只检查 _viewModel
        {
            return;
        }
        
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
        {
            return;
        }
        
        string? newName = null;
        var dialog = ModernDialogPresets.Input(
            "重命名策略",
            "输入新的策略名称...",
            strategy.Name,
            (name) => newName = name
        );
        
        await dialog.ShowDialog(window);
        bool result = !string.IsNullOrEmpty(newName);
        
        if (!result || string.IsNullOrEmpty(newName))
        {
            Console.WriteLine("❌ 策略名称不能为空或取消重命名");
            return;
        }
        
        if (newName == strategy.Name)
        {
            Console.WriteLine("⚠️ 策略名称未更改");
            return;
        }
        
        // 调用本地服务重命名
        Console.WriteLine($"🔄 开始重命名策略: ID={strategy.Id}, 旧名称={strategy.Name}, 新名称={newName}");
        
        // 更新策略名称
        strategy.Name = newName;
        var success = await _strategyService.UpdateStrategyAsync(strategy);
        
        if (success)
        {
            Console.WriteLine($"✅ 策略已重命名: {strategy.Name}");
            
            // 如果该策略有打开的Tab，更新Tab标题和ToolTip
            if (_strategyTabs.ContainsKey(strategy.Id))
            {
                var tabItem = _strategyTabs[strategy.Id];
                if (tabItem.Tag is TabStrategyData tabData)
                {
                    tabData.Strategy.Name = newName;
                    
                    // 更新Tab标题
                    if (tabItem.Header is Grid headerGrid && headerGrid.Children[0] is TextBlock headerText)
                    {
                        headerText.Text = GetTabHeaderText(tabData.Strategy, tabData.IsModified);
                    }
                    
                    // 更新ToolTip显示完整策略名称
                    Avalonia.Controls.ToolTip.SetTip(tabItem, GetTabTooltipText(tabData.Strategy));
                }
            }
            
            // 刷新策略列表
            await _viewModel.LoadStrategiesAsync();
        }
        else
        {
            Console.WriteLine($"❌ 重命名策略失败: ID={strategy.Id}, 旧名称={strategy.Name}, 新名称={newName}");
            
            // 显示错误提示
            var errorDialog = ModernDialogPresets.Warning(
                "重命名失败",
                $"无法重命名策略 \"{strategy.Name}\"。\n\n请检查网络连接或稍后重试。"
            );
            await errorDialog.ShowDialog(window);
        }
    }
    
    /// <summary>
    /// 编辑策略信息（交易对和备注）
    /// </summary>
    private async void OnEditStrategyInfo(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            // 检查策略状态：只有草稿状态才能编辑信息
            if (strategy.Status != "draft")
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」不是草稿状态，无法编辑信息");
                
                // 显示警告对话框
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window == null) return;
                
                var warningDialog = ModernDialogPresets.Warning(
                    "无法编辑",
                    "只有草稿状态的策略才能编辑信息。\n\n请先将策略设置为草稿状态。"
                );
                
                await warningDialog.ShowDialog(window);
                return;
            }
            
            // 显示编辑信息对话框
            await ShowEditStrategyInfoDialogAsync(strategy);
        }
    }
    
    /// <summary>
    /// 显示编辑策略信息对话框
    /// </summary>
    private async Task ShowEditStrategyInfoDialogAsync(StrategyInfo strategy)
    {
        if (_viewModel == null)  // 离线版：只检查 _viewModel
        {
            return;
        }
        
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
        {
            return;
        }
        
        string? newRemarks = null;
        
        var dialog = ModernDialogPresets.EditStrategyInfo(
            strategy.Name,
            "",
            strategy.Remarks,
            (symbol, remarks) =>
            {
                newRemarks = remarks;
            }
        );
        
        await dialog.ShowDialog(window);
        bool result = newRemarks != null;
        
        if (!result)
        {
            Console.WriteLine("⚠️ 取消编辑策略信息");
            return;
        }
        
        // 检查是否有修改
        if (newRemarks == strategy.Remarks)
        {
            Console.WriteLine("⚠️ 策略信息未更改");
            return;
        }
        
        // 调用API更新策略信息
        Console.WriteLine($"🔄 开始更新策略信息: ID={strategy.Id}");
        // 离线版：暂不支持更新策略信息（可以扩展 LocalStrategyService）
        Console.WriteLine($"⚠️ 离线版暂不支持更新策略信息");
        return;
    }
    
    /// <summary>
    /// 激活策略
    /// </summary>
    private async void OnActivateStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            // 检查策略状态：只有草稿状态才能激活
            if (strategy.Status != "draft")
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」不是草稿状态，无法激活");
                
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window == null) return;
                
                var warningDialog = ModernDialogPresets.Warning(
                    "无法激活",
                    "只有草稿状态的策略才能激活。"
                );
                
                await warningDialog.ShowDialog(window);
                return;
            }
            
            // 显示激活策略对话框
            await ShowActivateStrategyDialogAsync(strategy);
        }
    }
    
    /// <summary>
    /// 显示激活策略对话框
    /// </summary>
    private async Task ShowActivateStrategyDialogAsync(StrategyInfo strategy)
    {
        if (_viewModel == null)  // 离线版：只检查 _viewModel
        {
            return;
        }
        
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
        {
            return;
        }
        
        bool? makePublic = null;
        var dialog = ModernDialogPresets.ActivateStrategy(
            strategy.Name,
            (result) => makePublic = result
        );
        
        await dialog.ShowDialog(window);
        
        if (makePublic == null)
        {
            Console.WriteLine("❌ 取消激活策略");
            return;
        }
        
        // 离线版：暂不支持激活策略
        Console.WriteLine($"⚠️ 离线版暂不支持激活策略");
        return;
    }
    
    /// <summary>
    /// 共享策略
    /// </summary>
    private async void OnShareStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            // 检查策略状态：只有已激活且未公开的策略才能共享
            if (!strategy.Enabled || strategy.Public)
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」不满足共享条件");
                return;
            }
            
            // 显示共享确认对话框
            await ShowShareStrategyDialogAsync(strategy);
        }
    }
    
    /// <summary>
    /// 显示共享策略确认对话框
    /// </summary>
    private async Task ShowShareStrategyDialogAsync(StrategyInfo strategy)
    {
        if (_viewModel == null)  // 离线版：只检查 _viewModel
        {
            return;
        }
        
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
        {
            return;
        }
        
        bool confirmed = false;
        var dialog = ModernDialogPresets.ShareStrategy(
            strategy.Name,
            (result) => confirmed = result
        );
        
        await dialog.ShowDialog(window);
        
        if (!confirmed)
        {
            Console.WriteLine("❌ 取消共享策略");
            return;
        }
        
        // 调用API共享策略
        Console.WriteLine($"🔄 开始共享策略: ID={strategy.Id}, Name={strategy.Name}");
        // 离线版：暂不支持分享策略
        Console.WriteLine($"⚠️ 离线版暂不支持分享策略");
        return;
    }
    
    /// <summary>
    /// 取消共享策略
    /// </summary>
    private async void OnUnshareStrategy(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            // 检查策略状态：只有已激活且已公开的策略才能取消共享
            if (!strategy.Enabled || !strategy.Public)
            {
                Console.WriteLine($"❌ 策略「{strategy.Name}」不满足取消共享条件");
                return;
            }
            
            // 显示取消共享确认对话框
            await ShowUnshareStrategyDialogAsync(strategy);
        }
    }
    
    /// <summary>
    /// 显示取消共享策略确认对话框
    /// </summary>
    private async Task ShowUnshareStrategyDialogAsync(StrategyInfo strategy)
    {
        await Task.CompletedTask;  // 离线版：消除异步警告
        
        if (_viewModel == null)  // 离线版：只检查 _viewModel
        {
            return;
        }
        
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
        {
            return;
        }
        
        // 离线版：暂不支持订阅统计和取消分享
        Console.WriteLine($"⚠️ 离线版暂不支持取消共享策略");
        return;
    }
    
    private void OnDeleteStrategyFromCard(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            Console.WriteLine($"从卡片删除策略: {strategy.Name}");
            // TODO: 实现删除逻辑（与工具栏删除类似）
        }
    }
    
    private void OnShareStrategyFromCard(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is StrategyInfo strategy)
        {
            Console.WriteLine($"分享策略: {strategy.Name}");
            // TODO: 实现分享逻辑
        }
    }

    /// <summary>
    /// 获取Tab标题文本（带截断处理）
    /// </summary>
    private string GetTabHeaderText(StrategyInfo strategy, bool isModified)
    {
        var modifiedMark = isModified ? "*" : "";
        var name = strategy.Name;
        
        // Tab中最多显示8个字符（中文）
        const int maxLength = 8;
        
        if (name.Length > maxLength)
        {
            name = name.Substring(0, maxLength) + "...";
        }
        
        return $"{name} {modifiedMark}";
    }
    
    /// <summary>
    /// 获取Tab的完整提示文本（用于ToolTip）
    /// </summary>
    private string GetTabTooltipText(StrategyInfo strategy)
    {
        return strategy.Name;
    }
    
    /// <summary>
    /// 编辑器文本变化事件
    /// </summary>
    private void OnEditorTextChanged(TabItem tabItem, TabStrategyData tabData)
    {
        if (tabData.Editor == null) return;
        
        var currentDsl = tabData.Editor.Text;
        var wasModified = tabData.IsModified;
        tabData.IsModified = currentDsl != tabData.OriginalDsl;
        
        // 如果修改状态变化，更新Tab标题
        if (wasModified != tabData.IsModified && tabItem.Header is Grid headerGrid)
        {
            if (headerGrid.Children[0] is TextBlock headerText)
            {
                headerText.Text = GetTabHeaderText(tabData.Strategy, tabData.IsModified);
            }
        }
    }
    
    /// <summary>
    /// 关闭Tab
    /// </summary>
    private async void OnCloseTab(TabItem tabItem, TabStrategyData tabData)
    {
        // 检查是否有未保存的修改
        if (tabData.IsModified)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;
            
            // 创建自定义三按钮对话框
            var dialog = new ModernDialog
            {
                Title = "未保存的更改",
                Width = 400,
                Height = 200
            };
            
            var messageText = new TextBlock
            {
                Text = $"策略「{tabData.Strategy.Name}」有未保存的修改\n\n是否保存？",
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
            };
            
            dialog.SetContent(messageText);
            
            // 结果跟踪变量
            string? result = null;
            
            // 取消按钮
            dialog.AddButton("取消", () =>
            {
                result = "cancel";
                dialog.Close(false);
            });
            
            // 不保存按钮
            dialog.AddButton("不保存", () =>
            {
                result = "nosave";
                dialog.Close(false);
            });
            
            // 保存按钮（主按钮）
            dialog.AddButton("保存", () =>
            {
                result = "save";
                dialog.Close(true);
            }, isPrimary: true);
            
            await dialog.ShowDialog(window);
            
            if (result == "cancel")
            {
                Console.WriteLine("用户取消了关闭");
                return;
            }
            
            if (result == "save")
            {
                // TODO: 触发保存逻辑
                Console.WriteLine("保存并关闭");
                // 暂时直接关闭
            }
        }
        
        // 释放编辑器资源（避免内存泄漏）
        try
        {
            // 解绑 TextChanged 事件
            if (tabData.Editor != null && tabData.TextChangedHandler != null)
            {
                tabData.Editor.TextChanged -= tabData.TextChangedHandler;
                tabData.TextChangedHandler = null;
            }

            // 解绑 DiagnosticsUpdated 事件
            if (tabData.EditorHelper != null && tabData.DiagnosticsUpdatedHandler != null)
            {
                tabData.EditorHelper.DiagnosticsUpdated -= tabData.DiagnosticsUpdatedHandler;
                tabData.DiagnosticsUpdatedHandler = null;
            }

            // 释放 EditorHelper（会自动释放 ValidationManager 和 HoverTooltipManager）
            tabData.EditorHelper?.Dispose();
            tabData.EditorHelper = null;
            
            Console.WriteLine($"✅ 已释放编辑器资源: {tabData.Strategy.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 释放编辑器资源时出错: {ex.Message}");
        }
        
        // 关闭Tab
        _tabItems.Remove(tabItem);
        _strategyTabs.Remove(tabData.Strategy.Id);
        Console.WriteLine($"✅ 关闭Tab: {tabData.Strategy.Name}");
    }

    private void OnSearchBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        // 失去焦点时恢复默认边框（使用动态资源的边框颜色）
        var border = this.FindControl<Border>("SearchBoxBorder");
        if (border != null)
        {
            // 恢复到主题的默认边框颜色
            var borderBrush = Application.Current?.FindResource("BorderBrush") as SolidColorBrush;
            border.BorderBrush = borderBrush ?? new SolidColorBrush(Color.Parse("#2D2D2D"));
            border.BorderThickness = new Avalonia.Thickness(1);
        }
    }

    // ==================== 模板相关功能 ====================

    /// <summary>
    /// 从模板创建策略（快速入口）
    /// </summary>
    private void OnCreateFromTemplate(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string templateId)
        {
            CreateStrategyFromTemplate(templateId);
        }
    }
    
    /// <summary>
    /// 从模板ID创建策略（内部方法）
    /// </summary>
    private void CreateStrategyFromTemplate(string templateId)
    {
        try
        {
            // 检查Tab数量限制
            if (_tabItems.Count >= 8)
            {
                Console.WriteLine("⚠️ Tab数量已达上限（8个）");
                return;
            }

            // 从模板服务获取模板
            var templateService = ServiceContainer.GetService<TemplateService>();
            var template = templateService.GetTemplateById(templateId);
            if (template == null)
            {
                Console.WriteLine($"❌ 未找到模板: {templateId}");
                return;
            }

            Console.WriteLine($"✅ 从模板创建策略: {template.Name}");

            // 生成策略名称（模板名 + 日期时间）
            var strategyName = $"{template.Name}_{DateTime.Now:MMddHHmm}";

            // 创建临时策略对象
            var tempStrategy = new StrategyInfo
            {
                Id = $"temp_{Guid.NewGuid():N}",
                Name = strategyName,
                Dsl = template.DslCode,
                Status = "draft",
                Version = "未保存",
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // 创建编辑器Tab
            OpenEditorTab(tempStrategy, template.Notes);

            Console.WriteLine($"✅ 已创建策略Tab: {strategyName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 从模板创建策略失败: {ex.Message}");
        }
    }

    // 用于存储当前打开的菜单Popup，以便在点击外部时关闭
    private Popup? _currentTemplateMenuPopup;

    /// <summary>
    /// 关闭模板菜单
    /// </summary>
    private void CloseTemplateMenu(Button button)
    {
        if (_currentTemplateMenuPopup != null)
        {
            _currentTemplateMenuPopup.Close();
            _currentTemplateMenuPopup = null;
        }
        
        button.Classes.Remove("active");
        button.Background = Brushes.Transparent;
        button.CornerRadius = new Avalonia.CornerRadius(4); // 恢复默认圆角
        Console.WriteLine("✅ 菜单已关闭，按钮状态已恢复");
    }

    /// <summary>
    /// 从模板新建按钮点击事件 - 使用Popup显示菜单（全直角设计）
    /// </summary>
    private void OnNewFromTemplateButtonClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button)
                return;

            // 如果菜单已经打开，则关闭它
            if (_currentTemplateMenuPopup != null && _currentTemplateMenuPopup.IsOpen)
            {
                CloseTemplateMenu(button);
                Console.WriteLine("✅ 再次点击按钮，菜单已关闭");
                return;
            }

            // 添加按钮激活状态并直接设置样式（上圆角，下直角）
            button.Classes.Add("active");
            button.Background = new SolidColorBrush(Color.Parse("#34393E"));
            button.CornerRadius = new Avalonia.CornerRadius(4, 4, 0, 0); // 上圆角，下直角
            button.BorderThickness = new Avalonia.Thickness(0);
            Console.WriteLine($"✅ 按钮激活状态已添加（上圆角，下直角）");

            // 创建菜单内容容器
            var menuPanel = new StackPanel
            {
                Background = Brushes.Transparent,
                MinWidth = 200
            };

            // 获取所有模板并创建一级菜单（不分类）
            var templateService = ServiceContainer.GetService<TemplateService>();
            var allTemplates = new List<(dynamic Template, string Category)>();
            
            foreach (var category in templateService.GetAllCategories())
            {
                var categoryTemplates = templateService.GetTemplatesByCategory(category);
                foreach (var template in categoryTemplates)
                {
                    allTemplates.Add((template, category));
                }
            }

            // 按评分排序并创建菜单项
            foreach (var (template, category) in allTemplates.OrderByDescending(t => t.Template.Rating))
            {
                // 创建按钮内容：图标 + 文字
                var buttonContent = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 10
                };

                // 添加分类图标
                var iconKey = GetCategoryIconKey(category);
                if (iconKey != null && Application.Current?.TryFindResource(iconKey, out var iconResource) == true)
                {
                    if (iconResource is StreamGeometry streamGeometry)
                    {
                        var iconPath = new Avalonia.Controls.Shapes.Path
                        {
                            Data = streamGeometry,
                            Fill = new SolidColorBrush(Color.Parse("#999999")), // 灰色图标
                            Stretch = Stretch.Uniform
                        };

                        var iconViewbox = new Viewbox
                        {
                            Width = 16,
                            Height = 16,
                            Child = iconPath,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };

                        buttonContent.Children.Add(iconViewbox);
                    }
                }

                // 添加模板名称
                var textBlock = new TextBlock
                {
                    Text = template.Name,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                buttonContent.Children.Add(textBlock);

                var templateButton = new Button
                {
                    Content = buttonContent,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Background = Brushes.Transparent,
                    BorderThickness = new Avalonia.Thickness(0),
                    Padding = new Avalonia.Thickness(12, 10, 12, 10),
                    Foreground = new SolidColorBrush(Color.Parse("#E6E6E6")),
                    FontSize = 13,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = template.Id
                };

                // 悬停效果
                templateButton.PointerEntered += (s, args) =>
                {
                    if (s is Button btn)
                    {
                        btn.Background = new SolidColorBrush(Color.Parse("#2B3139"));
                        // 悬停时图标变为币安黄
                        if (btn.Content is StackPanel sp && sp.Children[0] is Viewbox vb && vb.Child is Avalonia.Controls.Shapes.Path p)
                        {
                            p.Fill = new SolidColorBrush(Color.Parse("#F0B90B"));
                        }
                    }
                };
                templateButton.PointerExited += (s, args) =>
                {
                    if (s is Button btn)
                    {
                        btn.Background = Brushes.Transparent;
                        // 离开时图标恢复灰色
                        if (btn.Content is StackPanel sp && sp.Children[0] is Viewbox vb && vb.Child is Avalonia.Controls.Shapes.Path p)
                        {
                            p.Fill = new SolidColorBrush(Color.Parse("#999999"));
                        }
                    }
                };

                // 点击事件
                templateButton.Click += (s, args) =>
                {
                    // 直接调用创建策略方法，传递模板ID
                    if (template.Id != null)
                    {
                        CreateStrategyFromTemplate(template.Id);
                    }
                    CloseTemplateMenu(button);
                };

                menuPanel.Children.Add(templateButton);
            }

            // 创建菜单边框容器（全直角设计，与按钮贴合）
            var menuBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#34393E")),
                BorderThickness = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(0), // 全直角
                MinWidth = 200,
                MaxHeight = 400
            };

            // 创建滚动容器
            var scrollViewer = new ScrollViewer
            {
                Content = menuPanel,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
                MaxHeight = 400
            };

            menuBorder.Child = scrollViewer;

            // 创建Popup
            var popup = new Popup
            {
                Child = menuBorder,
                PlacementTarget = button,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                HorizontalOffset = 0,
                VerticalOffset = -1,
                IsLightDismissEnabled = true
            };

            // 监听Popup关闭事件
            popup.Closed += (s, args) =>
            {
                CloseTemplateMenu(button);
            };

            // 保存引用并打开Popup
            _currentTemplateMenuPopup = popup;
            popup.IsOpen = true;

            Console.WriteLine("✅ 模板菜单已显示（全直角设计）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建模板菜单失败: {ex.Message}");
        }
    }
    
    // 用于存储当前打开的代码片段菜单Popup
    private Popup? _currentSnippetMenuPopup;

    /// <summary>
    /// 关闭代码片段菜单
    /// </summary>
    private void CloseSnippetMenu(Button button)
    {
        if (_currentSnippetMenuPopup != null)
        {
            _currentSnippetMenuPopup.Close();
            _currentSnippetMenuPopup = null;
        }
        
        button.Classes.Remove("active");
        button.Background = Brushes.Transparent;
        button.CornerRadius = new Avalonia.CornerRadius(4); // 恢复默认圆角
        Console.WriteLine("✅ 代码片段菜单已关闭，按钮状态已恢复");
    }
    
    #region 对话框辅助方法
    
    /// <summary>
    /// 显示警告对话框
    /// </summary>
    private async Task ShowWarningAsync(string title, string message)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return;
        
        var dialog = ModernDialogPresets.Warning(title, message);
        await dialog.ShowDialog(window);
    }
    
    /// <summary>
    /// 显示信息对话框
    /// </summary>
    private async Task ShowInfoAsync(string title, string message)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return;
        
        var dialog = ModernDialogPresets.Info(title, message);
        await dialog.ShowDialog(window);
    }
    
    /// <summary>
    /// 显示错误对话框
    /// </summary>
    private async Task ShowErrorAsync(string title, string message)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return;
        
        var dialog = ModernDialogPresets.Error(title, message);
        await dialog.ShowDialog(window);
    }
    
    #endregion

    /// <summary>
    /// 插入代码片段按钮点击事件
    /// </summary>
    private void OnInsertSnippetButtonClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button)
                return;

            // 如果菜单已经打开，则关闭它
            if (_currentSnippetMenuPopup != null && _currentSnippetMenuPopup.IsOpen)
            {
                CloseSnippetMenu(button);
                Console.WriteLine("✅ 再次点击按钮，代码片段菜单已关闭");
                return;
            }

            // 添加按钮激活状态并直接设置样式（上圆角，下直角）
            button.Classes.Add("active");
            button.Background = new SolidColorBrush(Color.Parse("#34393E"));
            button.CornerRadius = new Avalonia.CornerRadius(4, 4, 0, 0); // 上圆角，下直角
            button.BorderThickness = new Avalonia.Thickness(0);
            Console.WriteLine($"✅ 代码片段按钮激活状态已添加（上圆角，下直角）");

            // 创建菜单内容容器
            var menuPanel = new StackPanel
            {
                Background = Brushes.Transparent,
                MinWidth = 200
            };

            // 临时占位符：3个代码片段
            var snippets = new[]
            {
                new { Name = "片段1", Description = "代码片段占位符1" },
                new { Name = "片段2", Description = "代码片段占位符2" },
                new { Name = "片段3", Description = "代码片段占位符3" }
            };

            foreach (var snippet in snippets)
            {
                var snippetButton = new Button
                {
                    Content = snippet.Name,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Background = Brushes.Transparent,
                    BorderThickness = new Avalonia.Thickness(0),
                    Padding = new Avalonia.Thickness(12, 10, 12, 10),
                    Foreground = new SolidColorBrush(Color.Parse("#E6E6E6")),
                    FontSize = 13,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = snippet.Name
                };

                // 悬停效果
                snippetButton.PointerEntered += (s, args) =>
                {
                    if (s is Button btn)
                    {
                        btn.Background = new SolidColorBrush(Color.Parse("#2B3139"));
                    }
                };
                snippetButton.PointerExited += (s, args) =>
                {
                    if (s is Button btn)
                    {
                        btn.Background = Brushes.Transparent;
                    }
                };

                // 点击事件
                snippetButton.Click += (s, args) =>
                {
                    Console.WriteLine($"✅ 点击了代码片段: {snippet.Name}");
                    // TODO: 实际插入代码片段的逻辑
                    CloseSnippetMenu(button);
                };

                menuPanel.Children.Add(snippetButton);
            }

            // 创建菜单边框容器（全直角设计，与按钮贴合）
            var menuBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#34393E")),
                BorderThickness = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(0), // 全直角
                MinWidth = 200,
                MaxHeight = 400
            };

            // 创建滚动容器
            var scrollViewer = new ScrollViewer
            {
                Content = menuPanel,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
                MaxHeight = 400
            };

            menuBorder.Child = scrollViewer;

            // 创建Popup
            var popup = new Popup
            {
                Child = menuBorder,
                PlacementTarget = button,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                HorizontalOffset = 0,
                VerticalOffset = -1,
                IsLightDismissEnabled = true
            };

            // 监听Popup关闭事件
            popup.Closed += (s, args) =>
            {
                CloseSnippetMenu(button);
            };

            // 保存引用并打开Popup
            _currentSnippetMenuPopup = popup;
            popup.IsOpen = true;

            Console.WriteLine("✅ 代码片段菜单已显示（全直角设计）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建代码片段菜单失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取分类对应的图标资源键
    /// </summary>
    private string? GetCategoryIconKey(string category)
    {
        return category switch
        {
            "mean_revert" => "IconMeanRegression",
            "momentum" => "IconMomentumBreakout",
            "pattern" => "IconPattern",
            "smc" => "IconSMC",
            "trend" => "IconTrendFollowing",
            "volatility" => "IconVolatility",
            "volume" => "IconVolume",
            _ => null
        };
    }
}

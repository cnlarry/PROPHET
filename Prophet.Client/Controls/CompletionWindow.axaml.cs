using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Prophet.Client.Core;
using Prophet.Client.Services;

namespace Prophet.Client.Controls;

public partial class CompletionWindow : Window
{
    private const int MaxVisibleItems = 10;
    private const double DefaultItemHeight = 24d;

    private readonly ObservableCollection<PopupItemViewModel> _viewModels = new();
    private List<CompletionItem> _items = new();
    private ListBox _completionList = null!;
    private ScrollViewer _completionScrollViewer = null!;
    private int _selectedIndex = -1;
    private bool _suppressSelectionChanged;
    private double _lastMeasuredItemHeight = DefaultItemHeight;
    private bool _isClosing = false;
    
    // 文档预览面板元素
    private Border? _docPanel;
    private Border? _separator;
    private TextBlock? _docTitle;
    private TextBlock? _docCategory;
    private TextBlock? _docType;
    private TextBlock? _docDescription;
    private TextBlock? _docDataType;
    private StackPanel? _docDataTypePanel;
    private StackPanel? _docFieldsPanel;
    private StackPanel? _docParametersPanel;
    private StackPanel? _docExamplePanel;
    private TextBlock? _docExample;
    private StackPanel? _docEnumValuesPanel;
    private ItemsControl? _docEnumValues;

    public event EventHandler<CompletionItemEventArgs>? CompletionConfirmed;
    public event EventHandler? CloseRequested;

    private Window? _parentWindow;
    private Control? _editorControl; // 保存编辑器控件引用，用于焦点管理

    // 添加无参构造函数以消除 XAML 警告
    public CompletionWindow() : this(null, null)
    {
    }

    public CompletionWindow(Window? parentWindow, Control? editorControl = null)
    {
        _parentWindow = parentWindow;
        _editorControl = editorControl;
        
        // 关键：在 InitializeComponent 之前设置属性
        this.Focusable = false;
        
        InitializeComponent();
        InitializeControls();
        
        // 不在构造函数中监听 Deactivated，而是在窗口显示后监听
        // 因为 Show() 可能会触发 Deactivated 事件
    }
    
    /// <summary>
    /// 父窗口失去焦点时关闭补全窗口
    /// </summary>
    private void OnParentWindowDeactivated(object? sender, EventArgs e)
    {
SafeClose();
    }
    
    /// <summary>
    /// 窗口本身失去焦点时也关闭（用于切换应用的场景）
    /// </summary>
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
SafeClose();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _completionList = this.FindControl<ListBox>("CompletionList")
            ?? throw new InvalidOperationException("无法找到 ListBox 'CompletionList'");
        
        _completionScrollViewer = this.FindControl<ScrollViewer>("CompletionScrollViewer")
            ?? throw new InvalidOperationException("无法找到 ScrollViewer 'CompletionScrollViewer'");
        
        // 获取文档预览面板元素的引用
        _docPanel = this.FindControl<Border>("DocPanel");
        _separator = this.FindControl<Border>("Separator");
        _docTitle = this.FindControl<TextBlock>("DocTitle");
        _docCategory = this.FindControl<TextBlock>("DocCategory");
        _docType = this.FindControl<TextBlock>("DocType");
        _docDescription = this.FindControl<TextBlock>("DocDescription");
        _docDataType = this.FindControl<TextBlock>("DocDataType");
        _docDataTypePanel = this.FindControl<StackPanel>("DocDataTypePanel");
        _docFieldsPanel = this.FindControl<StackPanel>("DocFieldsPanel");
        _docParametersPanel = this.FindControl<StackPanel>("DocParametersPanel");
        _docExamplePanel = this.FindControl<StackPanel>("DocExamplePanel");
        _docExample = this.FindControl<TextBlock>("DocExample");
        _docEnumValuesPanel = this.FindControl<StackPanel>("DocEnumValuesPanel");
        _docEnumValues = this.FindControl<ItemsControl>("DocEnumValues");
    }

    private void InitializeControls()
    {
        // ItemsSource 直接绑定到 CompletionItem 列表，使用 XAML 中定义的 ItemTemplate
        _completionList.ItemsSource = _viewModels;
        
        _completionList.SelectionChanged += OnSelectionChanged;
        _completionList.PointerPressed += OnListBoxPointerPressed;
        _completionList.DoubleTapped += OnListBoxDoubleTapped;

        // 使用 AddHandler 的 Tunneling 路由（在控件处理之前捕获）
        this.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: false);
        
        // 禁用 Tab 键盘导航（防止 Tab 键被焦点系统拦截）
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
        
        // 监听窗口获取焦点事件，立即将焦点返回给编辑器
        this.GotFocus += OnWindowGotFocus;
        
        // 监听窗口自身的 Deactivated 事件（用于切换应用时关闭）
        this.Deactivated += OnWindowDeactivated;
        
        // 监听 ListBox 的焦点获取事件，立即将焦点返回给编辑器
        _completionList.GotFocus += OnListBoxGotFocus;
    }
    
    /// <summary>
    /// 当窗口获得焦点时，立即将焦点返回给编辑器
    /// </summary>
    private void OnWindowGotFocus(object? sender, GotFocusEventArgs e)
    {
        // 立即将焦点返回给编辑器
        ReturnFocusToEditor();
    }
    
    /// <summary>
    /// 当 ListBox 获得焦点时，立即将焦点返回给编辑器
    /// </summary>
    private void OnListBoxGotFocus(object? sender, GotFocusEventArgs e)
    { 
        // 立即将焦点返回给编辑器
        ReturnFocusToEditor();
        e.Handled = true;
    }
    
    /// <summary>
    /// 将焦点返回给编辑器的 TextArea
    /// </summary>
    private void ReturnFocusToEditor()
    {
        if (_editorControl is AvaloniaEdit.TextEditor editor && editor.TextArea != null)
        {
            editor.TextArea.Focus();
        }
        else if (_editorControl != null)
        {
            _editorControl.Focus();
        }
    }

    /// <summary>
    /// 安全关闭窗口（防止重复关闭导致异常）
    /// </summary>
    public void SafeClose()
    {
        if (_isClosing)
        {
            return;
        }
        
        _isClosing = true;
        
        // 移除父窗口的事件监听
        if (_parentWindow != null)
        {
            _parentWindow.RemoveHandler(PointerPressedEvent, OnParentWindowPointerPressed);
            _parentWindow.Deactivated -= OnParentWindowDeactivated;
        }
        
        try
        {
            Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"关闭补全窗口失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示补全列表
    /// </summary>
    public void ShowCompletions(IEnumerable<CompletionItem> items, Point position)
    {
        _items = items.ToList();
        _viewModels.Clear();

        if (_items.Count == 0)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            SafeClose();
            return;
        }

        foreach (var item in _items)
        {
            _viewModels.Add(new PopupItemViewModel(item));
        }

        UpdateSelection(_items.Count > 0 ? 0 : -1);

        // 设置窗口位置
        Position = new PixelPoint((int)position.X, (int)position.Y);

        // 不在这里设置高度，由 AdjustWindowHeight 统一管理
        // 注释掉固定高度设置，避免与 AdjustWindowHeight 冲突

        // 显示窗口（不获取焦点，让编辑器保持焦点）
        Show();
        
        // 监听点击窗口外部事件
        if (_parentWindow != null)
        {
            _parentWindow.AddHandler(PointerPressedEvent, OnParentWindowPointerPressed, RoutingStrategies.Tunnel);
            
            // 延迟注册 Deactivated 事件，避免 Show() 立即触发关闭
            Dispatcher.UIThread.Post(() =>
            {
                if (_parentWindow != null && !_isClosing)
                {
                    _parentWindow.Deactivated += OnParentWindowDeactivated;
                }
            }, DispatcherPriority.Background);
        }
        
        // 不要调用 Activate()，让编辑器保持焦点，用户可以继续输入

        Dispatcher.UIThread.Post(() =>
        {
            _completionList.ApplyTemplate();
            _completionList.UpdateLayout();
        }, DispatcherPriority.Render);
    }
    
    /// <summary>
    /// 父窗口点击事件处理（点击补全窗口外部时关闭）
    /// </summary>
    private void OnParentWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 检查点击是否在补全窗口内
        var point = e.GetPosition(this);
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        
        if (!bounds.Contains(point))
        {
            // 点击在窗口外部，关闭补全窗口
            SafeClose();
        }
    }
    
    /// <summary>
    /// 更新补全项列表（用于动态过滤）
    /// </summary>
    public void UpdateCompletions(IEnumerable<CompletionItem> items)
    {
        _items = items.ToList();
        _viewModels.Clear();

        if (_items.Count == 0)
        {
            SafeClose();
            return;
        }

        foreach (var item in _items)
        {
            _viewModels.Add(new PopupItemViewModel(item));
        }

        // 保持第一项选中
        UpdateSelection(0);
    }

    /// <summary>
    /// 公共方法：处理按键（供 CodeEditorHelper 调用）
    /// </summary>
    public void HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Down:
                MoveSelection(1);
                break;
            case Key.Up:
                MoveSelection(-1);
                break;
            case Key.PageDown:
                MoveSelection(5);
                break;
            case Key.PageUp:
                MoveSelection(-5);
                break;
            case Key.Home:
                UpdateSelection(0);
                break;
            case Key.End:
                UpdateSelection(_viewModels.Count - 1);
                break;
            case Key.Enter:
            case Key.Tab:
                ConfirmSelection();
                break;
            case Key.Escape:
                SafeClose();
                break;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        HandleKey(e.Key);
        e.Handled = true;
    }

    private void MoveSelection(int delta)
    {
        if (_viewModels.Count == 0) return;

        var newIndex = _selectedIndex + delta;
        
        // 循环到两端
        if (newIndex < 0)
            newIndex = _viewModels.Count - 1;
        else if (newIndex >= _viewModels.Count)
            newIndex = 0;
        
        UpdateSelection(newIndex);
    }

    private void UpdateSelection(int newIndex)
    {
        if (_viewModels.Count == 0)
        {
            _selectedIndex = -1;
            UpdateDocumentationPreview(null);
            return;
        }

        if (newIndex < 0)
            newIndex = 0;
        if (newIndex >= _viewModels.Count)
            newIndex = _viewModels.Count - 1;

        for (var i = 0; i < _viewModels.Count; i++)
        {
            _viewModels[i].IsSelected = i == newIndex;
        }

        _selectedIndex = newIndex;

        _suppressSelectionChanged = true;
        _completionList.SelectedIndex = newIndex;
        _suppressSelectionChanged = false;

        ScrollToItem(newIndex);
        
        // 更新文档预览
        if (newIndex >= 0 && newIndex < _items.Count)
        {
            UpdateDocumentationPreview(_items[newIndex]);
        }
    }

    private void ScrollToItem(int index)
    {
        if (index < 0 || index >= _completionList.ItemCount) return;

        // 使用延迟确保容器已生成
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 使用 ListBox 的 ScrollIntoView
                if (index >= 0 && index < _viewModels.Count)
                {
                    _completionList.ScrollIntoView(_viewModels[index]);
                }
                
                // 额外调用容器的 BringIntoView 作为备份
                var container = _completionList.ContainerFromIndex(index) as Control;
                if (container != null)
                {
                    container.BringIntoView();
                }
            }
            catch
            {
                // 忽略滚动错误
            }
        }, DispatcherPriority.Background);
    }

    private void ConfirmSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
        {
            return;
        }

        var item = _items[_selectedIndex];
        CompletionConfirmed?.Invoke(this, new CompletionItemEventArgs(item));
        SafeClose();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged) return;

        var index = _completionList.SelectedIndex;
        if (index >= 0)
        {
            UpdateSelection(index);
        }
    }

    private void OnListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        if (TryGetItemIndexFromEvent(listBox, e.Source, out var index))
        {
            UpdateSelection(index);
        }
    }

    private void OnListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        if (TryGetItemIndexFromEvent(listBox, e.Source, out var index))
        {
            UpdateSelection(index);
            ConfirmSelection();
        }
    }

    private static bool TryGetItemIndexFromEvent(ListBox listBox, object? source, out int index)
    {
        index = -1;

        if (source is not Control control) return false;

        var current = control;
        while (current is not null && current is not ListBoxItem)
        {
            current = current.Parent as Control;
        }

        if (current is ListBoxItem item)
        {
            index = listBox.IndexFromContainer(item);
            return index >= 0;
        }

        return false;
    }

    private void AdjustListBoxHeight()
    {
        if (_completionList.ItemCount == 0)
        {
            _completionScrollViewer.Height = double.NaN;
            return;
        }

        double itemHeight = _lastMeasuredItemHeight;

        for (var i = 0; i < _completionList.ItemCount; i++)
        {
            if (_completionList.ContainerFromIndex(i) is Control ctrl && ctrl.Bounds.Height > 0)
            {
                itemHeight = ctrl.Bounds.Height;
                break;
            }
        }

        if (itemHeight <= 0)
            itemHeight = DefaultItemHeight;

        _lastMeasuredItemHeight = itemHeight;

        // 计算最大高度，确保不超过10项的高度
        var maxHeight = itemHeight * MaxVisibleItems;
        
        // 如果项目少于10个，设置为实际高度；否则固定为最大高度
        if (_completionList.ItemCount <= MaxVisibleItems)
        {
            _completionScrollViewer.Height = _completionList.ItemCount * itemHeight;
            _completionScrollViewer.MaxHeight = maxHeight;
        }
        else
        {
            // 超过10项时，固定高度为10项，强制显示滚动条
            _completionScrollViewer.Height = maxHeight;
            _completionScrollViewer.MaxHeight = maxHeight;
        }
        
        _completionScrollViewer.MinHeight = itemHeight;
        
        // 强制更新布局以确保 ScrollViewer 正确初始化
        _completionScrollViewer.InvalidateMeasure();
        _completionScrollViewer.UpdateLayout();
    }
    
    /// <summary>
    /// 更新文档预览面板
    /// </summary>
    private void UpdateDocumentationPreview(CompletionItem? item)
    {
        if (_docPanel == null || _separator == null) return;
        
        // 如果没有选中项，或者选中的是分类头部，隐藏文档面板
        if (item == null || item.Kind == CompletionKind.CategoryHeader)
        {
            _docPanel.IsVisible = false;
            _separator.IsVisible = false;
            return;
        }
        
        // 显示文档面板和分隔线
        _docPanel.IsVisible = true;
        _separator.IsVisible = true;
        
        // 更新标题
        if (_docTitle != null)
        {
            _docTitle.Text = item.Label;
        }
        
        // 更新类型标签
        if (_docType != null)
        {
            _docType.Text = item.Kind.ToString();
        }
        
        // 更新分类
        if (_docCategory != null)
        {
            _docCategory.Text = GetCategoryDisplayName(item.Category, item.Kind);
        }
        
        // 更新描述
        if (_docDescription != null)
        {
            _docDescription.Text = item.Description ?? "无描述";
        }
        
        // 更新数据类型
        if (_docDataTypePanel != null && _docDataType != null)
        {
            if (!string.IsNullOrEmpty(item.DataType))
            {
                _docDataTypePanel.IsVisible = true;
                _docDataType.Text = item.DataType;
            }
            else
            {
                _docDataTypePanel.IsVisible = false;
            }
        }
        
        // 更新枚举值
        if (_docEnumValuesPanel != null && _docEnumValues != null)
        {
            if (item.EnumValues != null && item.EnumValues.Count > 0)
            {
                _docEnumValuesPanel.IsVisible = true;
                _docEnumValues.ItemsSource = item.EnumValues;
            }
            else
            {
                _docEnumValuesPanel.IsVisible = false;
            }
        }
        
        // 从 IntelliSenseService 加载更详细的文档信息
        LoadDetailedDocumentation(item);
    }
    
    /// <summary>
    /// 从 IntelliSenseService 加载详细文档
    /// </summary>
    private void LoadDetailedDocumentation(CompletionItem item)
    {
        var intelliSense = ServiceContainer.GetService<IntelliSenseService>();
        
        // 隐藏所有详细信息面板
        if (_docFieldsPanel != null) _docFieldsPanel.IsVisible = false;
        if (_docParametersPanel != null) _docParametersPanel.IsVisible = false;
        if (_docExamplePanel != null) _docExamplePanel.IsVisible = false;
        
        // 根据类型加载不同的详细信息
        switch (item.Kind)
        {
            case CompletionKind.Indicator:
                LoadIndicatorDocumentation(item.Label);
                break;
            case CompletionKind.Function:
            case CompletionKind.SignalFunction:
                LoadFunctionDocumentation(item.Label);
                break;
            case CompletionKind.Field:
                LoadFieldDocumentation(item);
                break;
        }
    }
    
    /// <summary>
    /// 加载指标文档（包含字段列表）
    /// </summary>
    private void LoadIndicatorDocumentation(string indicatorName)
    {
        if (_docFieldsPanel == null || _docExamplePanel == null || _docExample == null) 
            return;
        
        var intelliSense = ServiceContainer.GetService<IntelliSenseService>();
        var fields = intelliSense.GetIndicatorFieldCompletions(indicatorName).ToList();
        
        // 清空面板
        _docFieldsPanel.Children.Clear();
        
        if (fields.Count > 0)
        {
            _docFieldsPanel.IsVisible = true;
            
            // 添加标题
            _docFieldsPanel.Children.Add(new TextBlock
            {
                Text = "字段",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#9CDCFE"))
            });
            
            // 动态创建字段列表
            foreach (var field in fields)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 2),
                    CornerRadius = new CornerRadius(3)
                };
                
                var stack = new StackPanel { Spacing = 2 };
                
                stack.Children.Add(new TextBlock
                {
                    Text = field.Label,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#DCDCAA"))
                });
                
                stack.Children.Add(new TextBlock
                {
                    Text = field.Description ?? "无描述",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.Parse("#858585")),
                    TextWrapping = TextWrapping.Wrap
                });
                
                border.Child = stack;
                _docFieldsPanel.Children.Add(border);
            }
        }
        
        // 显示示例
        _docExamplePanel.IsVisible = true;
        if (_docExample != null)
        {
            _docExample.Text = $"$(15m).{indicatorName}().{(fields.FirstOrDefault()?.Label ?? "value")}";
        }
    }
    
    /// <summary>
    /// 加载函数文档（包含参数列表）
    /// </summary>
    private void LoadFunctionDocumentation(string functionName)
    {
        if (_docParametersPanel == null || _docExamplePanel == null || _docExample == null) 
            return;
        
        // 清空面板
        _docParametersPanel.Children.Clear();
        
        // TODO: 从 IntelliSense.yaml 中加载函数参数信息
        // 目前先显示基本信息
        _docParametersPanel.IsVisible = false;
        
        // 显示示例
        _docExamplePanel.IsVisible = true;
        if (_docExample != null)
        {
            _docExample.Text = functionName switch
            {
                "ALL" => "ALL($(15m).RSI().oversold, $(15m).MACD().is_bullish)",
                "ANY" => "ANY($(15m).RSI().oversold, $(15m).Stochastic().oversold)",
                "CROSS" => "CROSS($(15m).MACD().macd, $(15m).MACD().signal, ABOVE)",
                _ => $"{functionName}(...)"
            };
        }
    }
    
    /// <summary>
    /// 加载字段文档
    /// </summary>
    private void LoadFieldDocumentation(CompletionItem item)
    {
        if (_docExamplePanel == null || _docExample == null) return;
        
        // 显示示例
        _docExamplePanel.IsVisible = true;
        if (_docExample != null)
        {
            _docExample.Text = item.InsertText ?? item.Label;
        }
    }
    
    /// <summary>
    /// 获取分类显示名称
    /// </summary>
    private string GetCategoryDisplayName(string? category, CompletionKind kind)
    {
        if (!string.IsNullOrEmpty(category))
        {
            return category switch
            {
                "momentum" => "动量指标",
                "trend" => "趋势指标",
                "volatility" => "波动率指标",
                "volume" => "成交量指标",
                "price" => "价格指标",
                "pattern" => "形态指标",
                "statistics" => "统计指标",
                "signal" => "信号函数",
                "data" => "数据函数",
                "math" => "数学函数",
                "smc" => "SMC函数",
                _ => category
            };
        }
        
        return kind switch
        {
            CompletionKind.Indicator => "指标",
            CompletionKind.Function => "函数",
            CompletionKind.SignalFunction => "信号函数",
            CompletionKind.Field => "字段",
            CompletionKind.Parameter => "参数",
            CompletionKind.EnvVariable => "环境变量",
            CompletionKind.Timeframe => "时间框架",
            CompletionKind.Signal => "信号",
            CompletionKind.Snippet => "代码片段",
            _ => "其他"
        };
    }
}

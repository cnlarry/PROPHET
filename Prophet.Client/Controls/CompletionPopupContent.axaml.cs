using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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

public partial class CompletionPopupContent : UserControl
{
    private const int MaxVisibleItems = 10;
    private const double DefaultItemHeight = 24d;

    private readonly ObservableCollection<PopupItemViewModel> _viewModels = new();
    private List<CompletionItem> _items = new();
    private ListBox _completionList = null!;
    private int _selectedIndex = -1;
    private bool _suppressSelectionChanged;
    private double _lastMeasuredItemHeight = DefaultItemHeight;
    
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

    public CompletionPopupContent()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _completionList = this.FindControl<ListBox>("CompletionList")
            ?? throw new InvalidOperationException("无法找到 ListBox 'CompletionList'");
        
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

        _completionList.ItemsSource = _viewModels;
        
        // 禁用虚拟化，使用普通 StackPanel
        // 虽然性能稍差，但能确保所有项目被渲染，ScrollViewer 能正确计算 Extent
        _completionList.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        });

        _completionList.Template = new FuncControlTemplate<ListBox>((listBox, scope) =>
        {
            scope ??= new NameScope();

            var itemsPresenter = new ItemsPresenter
            {
                Name = "PART_ItemsPresenter",
                // 强制设置对齐和尺寸
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            // 绑定 ItemsPanel
            itemsPresenter[!ItemsPresenter.ItemsPanelProperty] = listBox[!ItemsControl.ItemsPanelProperty];
            scope.Register(itemsPresenter.Name, itemsPresenter);

            var scrollViewer = new ScrollViewer
            {
                Name = "PART_ScrollViewer",
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                AllowAutoHide = false,
                Content = itemsPresenter,
                // 确保 ScrollViewer 不会自动扩展
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
                // 不设置 MaxHeight！让内容自由扩展，由外层 ListBox 的 Height 控制
            };
            scope.Register(scrollViewer.Name, scrollViewer);

            var border = new Border
            {
                Padding = new Thickness(0),
                Child = scrollViewer
            };

            border[!Border.BackgroundProperty] = listBox[!TemplatedControl.BackgroundProperty];
            border[!Border.BorderBrushProperty] = listBox[!TemplatedControl.BorderBrushProperty];
            border[!Border.BorderThicknessProperty] = listBox[!TemplatedControl.BorderThicknessProperty];

            NameScope.SetNameScope(border, scope);
            return border;
        });

        _completionList.TemplateApplied += (_, __) =>
        {
            // 模板应用后，强制初始化 ScrollViewer
            Dispatcher.UIThread.Post(() =>
            {
                var scrollViewer = FindScrollViewer(_completionList);
                if (scrollViewer != null)
                {
                    scrollViewer.InvalidateMeasure();
                    scrollViewer.InvalidateArrange();
                    scrollViewer.UpdateLayout();
                }
            }, DispatcherPriority.Loaded);
        };

        _completionList.ItemTemplate = new FuncDataTemplate<PopupItemViewModel>((vm, _) =>
        {
            var containerBorder = new Border
            {
                Padding = new Thickness(6, 3),
                MinHeight = 22,
                Focusable = false,
                CornerRadius = new CornerRadius(2)
            };
            containerBorder.Bind(Border.BackgroundProperty, new Binding(nameof(PopupItemViewModel.Background)));

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8
            };

            var typeText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0),
                TextWrapping = TextWrapping.NoWrap
            };
            typeText.Bind(TextBlock.TextProperty, new Binding(nameof(PopupItemViewModel.TypeLabel)));
            typeText.Bind(TextBlock.ForegroundProperty, new Binding(nameof(PopupItemViewModel.TypeLabelColor)));
            Grid.SetColumn(typeText, 0);
            grid.Children.Add(typeText);

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(stack, 1);

            var labelText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.NoWrap
            };
            labelText.Bind(TextBlock.TextProperty, new Binding(nameof(PopupItemViewModel.Label)));
            stack.Children.Add(labelText);

            var descText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#858585")),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 240,
                Margin = new Thickness(4, 0, 0, 0)
            };
            descText.Bind(TextBlock.TextProperty, new Binding(nameof(PopupItemViewModel.Description)));
            descText.Bind(Visual.IsVisibleProperty, new Binding(nameof(PopupItemViewModel.HasDescription)));
            stack.Children.Add(descText);

            grid.Children.Add(stack);

            var dataTypeText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#858585")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            dataTypeText.Bind(TextBlock.TextProperty, new Binding(nameof(PopupItemViewModel.DataTypeDisplay)));
            dataTypeText.Bind(Visual.IsVisibleProperty, new Binding(nameof(PopupItemViewModel.HasDataType)));
            Grid.SetColumn(dataTypeText, 2);
            grid.Children.Add(dataTypeText);
            containerBorder.Child = grid;

            containerBorder.PointerEntered += (_, __) => vm.IsPointerOver = true;
            containerBorder.PointerExited += (_, __) => vm.IsPointerOver = false;

            return containerBorder;
        });

        _completionList.SelectionChanged += OnSelectionChanged;
        _completionList.PointerPressed += OnListBoxPointerPressed;
        _completionList.DoubleTapped += OnListBoxDoubleTapped;

    }

    /// <summary>
    /// 显示补全列表
    /// </summary>
    public void ShowCompletions(IEnumerable<CompletionItem> items)
    {
        _items = items.ToList();
        _viewModels.Clear();

        if (_items.Count == 0)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        foreach (var item in _items)
        {
            _viewModels.Add(new PopupItemViewModel(item));
        }

        UpdateSelection(_items.Count > 0 ? 0 : -1);

        Dispatcher.UIThread.Post(() =>
        {
            _completionList.ApplyTemplate();
            _completionList.UpdateLayout();
            AdjustListBoxHeight();
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// 处理按键（由外部调用）
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
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_viewModels.Count == 0) return;

        var count = _viewModels.Count;
        var newIndex = _selectedIndex + delta;
        
        // 循环并跳过分类头部
        while (newIndex >= 0 && newIndex < count)
        {
            if (!_viewModels[newIndex].IsCategoryHeader)
            {
                UpdateSelection(newIndex);
                return;
            }
            
            // 继续移动，跳过分类头部
            newIndex += (delta > 0 ? 1 : -1);
        }
        
        // 如果到达边界，循环到另一端
        if (newIndex < 0)
        {
            // 从最后一个非头部项开始
            for (int i = count - 1; i >= 0; i--)
            {
                if (!_viewModels[i].IsCategoryHeader)
                {
                    UpdateSelection(i);
                    return;
                }
            }
        }
        else if (newIndex >= count)
        {
            // 从第一个非头部项开始
            for (int i = 0; i < count; i++)
            {
                if (!_viewModels[i].IsCategoryHeader)
                {
                    UpdateSelection(i);
                    return;
                }
            }
        }
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
    
    /// <summary>
    /// 查找 ListBox 内部的 ScrollViewer
    /// </summary>
    private ScrollViewer? FindScrollViewer(Control control)
    {
        if (control is ScrollViewer sv)
            return sv;
            
        var children = control.GetVisualChildren();
        foreach (var child in children)
        {
            if (child is Control ctrl)
            {
                var result = FindScrollViewer(ctrl);
                if (result != null)
                    return result;
            }
        }
        
        return null;
    }

    private void ConfirmSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
        {
            return;
        }

        var item = _items[_selectedIndex];
        CompletionConfirmed?.Invoke(this, new CompletionItemEventArgs(item));
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
            _completionList.Height = double.NaN;
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
            _completionList.Height = _completionList.ItemCount * itemHeight;
            _completionList.MaxHeight = maxHeight;
        }
        else
        {
            // 超过10项时，固定高度为10项，强制显示滚动条
            _completionList.Height = maxHeight;
            _completionList.MaxHeight = maxHeight;
        }
        
        _completionList.MinHeight = itemHeight;
        
        // 强制更新布局以确保 ScrollViewer 正确初始化
        _completionList.InvalidateMeasure();
        _completionList.UpdateLayout();
        
        // 再次强制更新 ScrollViewer - 延迟到下一个渲染周期
        var scrollViewer = FindScrollViewer(_completionList);
        if (scrollViewer != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                scrollViewer.InvalidateMeasure();
                scrollViewer.InvalidateArrange();
                scrollViewer.UpdateLayout();
            }, DispatcherPriority.Background);
        }
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


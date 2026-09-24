using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Prophet.Client.Controls;

/// <summary>
/// 现代化无边框对话框基础类
/// 提供核心的窗口样式、标题栏、内容区和按钮管理功能
/// </summary>
public class ModernDialog : Window
{
    // 静态字典，用于跟踪每个对话框的遮罩层
    private static readonly Dictionary<Window, Border> _overlays = new Dictionary<Window, Border>();
    
    private StackPanel? _contentPanel;
    private StackPanel? _buttonPanel;
    private TextBlock? _titleTextBlock;
    
    /// <summary>
    /// 内容面板间距
    /// </summary>
    public double ContentSpacing
    {
        get => _contentPanel?.Spacing ?? 16;
        set
        {
            if (_contentPanel != null)
                _contentPanel.Spacing = value;
        }
    }
    
    /// <summary>
    /// 按钮面板间距
    /// </summary>
    public double ButtonSpacing
    {
        get => _buttonPanel?.Spacing ?? 10;
        set
        {
            if (_buttonPanel != null)
                _buttonPanel.Spacing = value;
        }
    }
    
    public ModernDialog()
    {
        InitializeWindow();
        BuildWindowLayout();
        AttachEventHandlers();
    }
    
    /// <summary>
    /// 初始化窗口基本属性
    /// </summary>
    private void InitializeWindow()
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        
        // 设置默认尺寸
        Width = 400;
        Height = 200;
    }
    
    /// <summary>
    /// 构建窗口布局
    /// </summary>
    private void BuildWindowLayout()
    {
        // 创建完全透明的根面板
        var rootPanel = new Panel
        {
            Background = Brushes.Transparent,
            ClipToBounds = false
        };
        
        // 创建外层容器
        var outerContainer = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(15),
            ClipToBounds = false
        };
        
        // 创建阴影层
        var shadowLayers = CreateShadowLayers();
        
        // 创建主容器
        var mainBorder = CreateMainBorder();
        
        // 叠加所有层
        var layeredContainer = new Grid();
        foreach (var shadow in shadowLayers)
        {
            layeredContainer.Children.Add(shadow);
        }
        layeredContainer.Children.Add(mainBorder);
        
        outerContainer.Child = layeredContainer;
        rootPanel.Children.Add(outerContainer);
        Content = rootPanel;
    }
    
    /// <summary>
    /// 创建阴影层
    /// </summary>
    private List<Border> CreateShadowLayers()
    {
        return new List<Border>
        {
            new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 0)
            },
            new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(1, 1, -1, -1)
            },
            new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0)),
                CornerRadius = new CornerRadius(8.5),
                Margin = new Thickness(1.5, 1.5, -1.5, -1.5)
            }
        };
    }
    
    /// <summary>
    /// 创建主容器
    /// </summary>
    private Border CreateMainBorder()
    {
        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };
        
        // 标题栏
        var titleBar = CreateTitleBar();
        titleBar.SetValue(Grid.RowProperty, 0);
        mainGrid.Children.Add(titleBar);
        
        // 内容区域
        var contentScrollViewer = CreateContentArea();
        contentScrollViewer.SetValue(Grid.RowProperty, 1);
        mainGrid.Children.Add(contentScrollViewer);
        
        // 按钮区域
        var buttonContainer = CreateButtonArea();
        buttonContainer.SetValue(Grid.RowProperty, 2);
        mainGrid.Children.Add(buttonContainer);
        
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E2329")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            Margin = new Thickness(-3, -3, 3, 3),
            ClipToBounds = true,
            Child = mainGrid
        };
    }
    
    /// <summary>
    /// 创建标题栏
    /// </summary>
    private Border CreateTitleBar()
    {
        var titleBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2B3139")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(10,5,10,5)
        };
        
        var titleGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        
        // 弹窗标题文字
        _titleTextBlock = new TextBlock
        {
            [Grid.ColumnProperty] = 0,
            Text = Title,
            FontSize = 14, // 弹窗标题字体大小
            FontWeight = FontWeight.SemiBold, // 弹窗标题字体加粗
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"))
        };
        
        titleGrid.Children.Add(_titleTextBlock);
        
        // 关闭按钮
        var closeButton = CreateCloseButton();
        closeButton.SetValue(Grid.ColumnProperty, 1);
        titleGrid.Children.Add(closeButton);
        
        titleBar.Child = titleGrid;
        
        // 使标题栏可拖动
        titleBar.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };
        
        return titleBar;
    }
    
    /// <summary>
    /// 创建关闭按钮
    /// </summary>
    private Button CreateCloseButton()
    {
        var closeButton = new Button
        {
            Content = "✕",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            FontSize = 16,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.Parse("#8B8D91")),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        
        // 悬停效果
        closeButton.PointerEntered += (s, e) =>
        {
            closeButton.Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"));
        };
        
        closeButton.PointerExited += (s, e) =>
        {
            closeButton.Foreground = new SolidColorBrush(Color.Parse("#8B8D91"));
        };
        
        closeButton.Click += (s, e) => OnCloseButtonClick();
        
        return closeButton;
    }
    
    /// <summary>
    /// 创建内容区域
    /// </summary>
    private ScrollViewer CreateContentArea()
    {
        _contentPanel = new StackPanel
        {
            Spacing = 16
        };
        
        return new ScrollViewer
        {
            Padding = new Thickness(20),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _contentPanel
        };
    }
    
    /// <summary>
    /// 创建按钮区域
    /// </summary>
    private Border CreateButtonArea()
    {
        _buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = new SolidColorBrush(Color.Parse("#1E2329")),
            Padding = new Thickness(10, 10),
            CornerRadius = new CornerRadius(0, 0, 8, 8),
            Child = _buttonPanel
        };
    }
    
    /// <summary>
    /// 附加事件处理器
    /// </summary>
    private void AttachEventHandlers()
    {
        // 窗口打开时添加遮罩
        this.Opened += (s, e) =>
        {
            Background = Brushes.Transparent;
            if (Owner is Window owner)
            {
                AddOverlayToOwner(owner);
            }
        };
        
        // 窗口关闭时移除遮罩
        this.Closing += (s, e) =>
        {
            if (Owner is Window owner)
            {
                RemoveOverlayFromOwner(owner);
            }
        };
        
        // 监听标题变化
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(Title) && _titleTextBlock != null)
            {
                _titleTextBlock.Text = Title;
            }
            else if (e.Property == BackgroundProperty && Background != Brushes.Transparent)
            {
                Background = Brushes.Transparent;
            }
        };
        
        // ESC键关闭（子类可重写）
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape && !e.Handled)
            {
                OnCloseButtonClick();
                e.Handled = true;
            }
        };
    }
    
    /// <summary>
    /// 设置对话框内容
    /// </summary>
    public void SetContent(params Control[] controls)
    {
        if (_contentPanel == null) return;
        
        _contentPanel.Children.Clear();
        foreach (var control in controls)
        {
            _contentPanel.Children.Add(control);
        }
    }
    
    /// <summary>
    /// 添加内容控件
    /// </summary>
    public void AddContent(Control control)
    {
        _contentPanel?.Children.Add(control);
    }
    
    /// <summary>
    /// 清空内容
    /// </summary>
    public void ClearContent()
    {
        _contentPanel?.Children.Clear();
    }
    
    /// <summary>
    /// 添加按钮
    /// </summary>
    public Button AddButton(string text, Action? onClick = null, bool isPrimary = false)
    {
        if (_buttonPanel == null) 
            throw new InvalidOperationException("按钮面板未初始化");
        
        var button = CreateButton(text, isPrimary);
        
        if (onClick != null)
        {
            button.Click += (s, e) => onClick();
        }
        
        _buttonPanel.Children.Add(button);
        return button;
    }
    
    /// <summary>
    /// 创建按钮
    /// </summary>
    private Button CreateButton(string text, bool isPrimary)
    {
        var button = new Button
        {
            Content = text,
            Width = 100,
            Height = 36,
            FontSize = 13,
            Padding = new Thickness(16, 0),
            BorderThickness = isPrimary ? new Thickness(0) : new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        
        if (isPrimary)
        {
            ApplyPrimaryButtonStyle(button);
        }
        else
        {
            ApplySecondaryButtonStyle(button);
        }
        
        return button;
    }
    
    /// <summary>
    /// 应用主按钮样式
    /// </summary>
    private void ApplyPrimaryButtonStyle(Button button)
    {
        button.Background = new SolidColorBrush(Color.Parse("#F0B90B"));
        button.Foreground = Brushes.White;
    }
    
    /// <summary>
    /// 应用次要按钮样式
    /// </summary>
    private void ApplySecondaryButtonStyle(Button button)
    {
        button.Background = new SolidColorBrush(Color.Parse("#1E2329"));
        button.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
        button.BorderBrush = new SolidColorBrush(Color.Parse("#3C3E41"));
        
        button.PointerEntered += (s, e) =>
        {
            if (s is Button btn)
                btn.Background = new SolidColorBrush(Color.Parse("#34393E"));
        };
        
        button.PointerExited += (s, e) =>
        {
            if (s is Button btn)
                btn.Background = new SolidColorBrush(Color.Parse("#1E2329"));
        };
    }
    
    /// <summary>
    /// 清空所有按钮
    /// </summary>
    public void ClearButtons()
    {
        _buttonPanel?.Children.Clear();
    }
    
    /// <summary>
    /// 为父窗口添加半透明遮罩
    /// </summary>
    private static void AddOverlayToOwner(Window owner)
    {
        try
        {
            if (_overlays.ContainsKey(owner))
            {
                RemoveOverlayFromOwner(owner);
            }
            
            Border? overlay = null;
            
            if (owner.Content is Control content && content.Parent is Panel panel)
            {
                overlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                    ZIndex = 9999,
                    IsVisible = true
                };
                panel.Children.Insert(panel.Children.Count, overlay);
            }
            else if (owner.Content is Panel ownerPanel)
            {
                overlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                    ZIndex = 9999,
                    IsVisible = true
                };
                ownerPanel.Children.Insert(ownerPanel.Children.Count, overlay);
            }
            
            if (overlay != null)
            {
                _overlays[owner] = overlay;
            }
        }
        catch
        {
            // 静默忽略
        }
    }
    
    /// <summary>
    /// 从父窗口移除遮罩
    /// </summary>
    private static void RemoveOverlayFromOwner(Window owner)
    {
        try
        {
            if (!_overlays.TryGetValue(owner, out var overlay))
                return;
            
            if (overlay.Parent is Panel panel)
            {
                panel.Children.Remove(overlay);
            }
            
            _overlays.Remove(owner);
        }
        catch
        {
            // 静默忽略
        }
    }
    
    /// <summary>
    /// 处理关闭按钮点击（子类可重写以返回特定类型）
    /// </summary>
    protected virtual void OnCloseButtonClick()
    {
        Close(false);
    }
}

using System;
using Avalonia;
using Avalonia.Media;
using Prophet.Client.Services;

namespace Prophet.Client.Controls;

/// <summary>
/// 补全项确认事件参数
/// </summary>
public class CompletionItemEventArgs : EventArgs
{
    public CompletionItem Item { get; }

    public CompletionItemEventArgs(CompletionItem item)
    {
        Item = item;
    }
}

/// <summary>
/// 补全项 ViewModel（用于 XAML 绑定）
/// </summary>
public class PopupItemViewModel : AvaloniaObject
{
    private readonly CompletionItem _item;

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PopupItemViewModel, bool>(nameof(IsSelected));

    public static readonly StyledProperty<IBrush> BackgroundProperty =
        AvaloniaProperty.Register<PopupItemViewModel, IBrush>(nameof(Background),
            new SolidColorBrush(Colors.Transparent));

    public static readonly StyledProperty<bool> IsPointerOverProperty =
        AvaloniaProperty.Register<PopupItemViewModel, bool>(nameof(IsPointerOver));

    public PopupItemViewModel(CompletionItem item)
    {
        _item = item;
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set
        {
            SetValue(IsSelectedProperty, value);
            UpdateBackground();
        }
    }

    public IBrush Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public bool IsPointerOver
    {
        get => GetValue(IsPointerOverProperty);
        set
        {
            SetValue(IsPointerOverProperty, value);
            UpdateBackground();
        }
    }

    public string Label => _item.Label;
    public string Description => _item.Description?.Trim() ?? string.Empty;
    public bool HasDescription => !string.IsNullOrEmpty(Description);

    public string? DataType => _item.DataType;
    public bool HasDataType => !string.IsNullOrWhiteSpace(DataType);
    public string DataTypeDisplay => HasDataType ? $": {DataType}" : string.Empty;

    public string TypeLabel => _item.Kind switch
    {
        CompletionKind.Indicator => "【指标】",
        CompletionKind.Field => "【字段】",
        CompletionKind.EnvVariable => "【环境变量】",
        CompletionKind.Timeframe => "【时间框架】",
        CompletionKind.EnumValue => "【值】",
        CompletionKind.Parameter => "【参数】",
        CompletionKind.Function => "【函数】",
        CompletionKind.SignalFunction => "【信号函数】",
        CompletionKind.Signal => "【信号】",
        CompletionKind.Snippet => "【代码片段】",
        CompletionKind.CategoryHeader => "", // 分类头部不显示类型标签
        _ => string.Empty
    };

    /// <summary>
    /// 获取类型图标的资源键
    /// </summary>
    public string TypeIconKey => _item.Kind switch
    {   
        // 候补项行首的图标
        CompletionKind.Indicator => "IconIndicator",
        CompletionKind.Field => "IconField",
        CompletionKind.EnvVariable => "IconEnvVariable",
        CompletionKind.Timeframe => "IconTimeframe",
        CompletionKind.EnumValue => "IconValue",
        CompletionKind.Parameter => "IconParameter",
        CompletionKind.Function => "IconFunction",
        CompletionKind.SignalFunction => "IconSignalFunction",
        CompletionKind.MathFunction => "IconFunction", // 数学函数使用 IconFunction
        CompletionKind.TimeSeriesFunction => "IconTimeframe", // 时间序列函数使用 IconTimeframe
        CompletionKind.DataFunction => "IconFunction", // 数据函数使用 IconFunction
        CompletionKind.Signal => "IconSignal",
        CompletionKind.Snippet => "IconCode",
        _ => string.Empty
    };

    /// <summary>
    /// 是否显示图标（分类头部不显示）
    /// </summary>
    public bool HasTypeIcon => _item.Kind != CompletionKind.CategoryHeader && !string.IsNullOrEmpty(TypeIconKey);

    /// <summary>
    /// 获取类型图标的 Geometry
    /// </summary>
    public Geometry? TypeIconGeometry
    {
        get
        {
            if (!HasTypeIcon) return null;
            
            // 从应用程序资源中获取图标
            if (Application.Current?.Resources.TryGetResource(TypeIconKey, null, out var resource) == true)
            {
                return resource as Geometry;
            }
            
            return null;
        }
    }

    public IBrush TypeLabelColor => _item.Kind switch
    {
        CompletionKind.Indicator => new SolidColorBrush(Color.Parse("#4EC9B0")),
        CompletionKind.Field => new SolidColorBrush(Color.Parse("#9CDCFE")),
        CompletionKind.EnvVariable => new SolidColorBrush(Color.Parse("#4FC1FF")),
        CompletionKind.Timeframe => new SolidColorBrush(Color.Parse("#B5CEA8")),
        CompletionKind.EnumValue => new SolidColorBrush(Color.Parse("#CE9178")),
        CompletionKind.Parameter => new SolidColorBrush(Color.Parse("#9CDCFE")),
        CompletionKind.Function => new SolidColorBrush(Color.Parse("#DCDCAA")),
        CompletionKind.SignalFunction => new SolidColorBrush(Color.Parse("#C586C0")),
        CompletionKind.Signal => new SolidColorBrush(Color.Parse("#569CD6")),
        CompletionKind.Snippet => new SolidColorBrush(Color.Parse("#F0B90B")),
        CompletionKind.CategoryHeader => new SolidColorBrush(Color.Parse("#FFFFFF")), // 分类头部为白色
        _ => new SolidColorBrush(Color.Parse("#808080"))
    };
    
    public bool IsCategoryHeader => _item.Kind == CompletionKind.CategoryHeader;

    private void UpdateBackground()
    {
        // 分类头部固定背景，不响应选中和悬停
        if (IsCategoryHeader)
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30"));
            return;
        }
        
        if (IsSelected)
        {
            Background = new SolidColorBrush(Color.Parse("#094771"));
        }
        else if (IsPointerOver)
        {
            Background = new SolidColorBrush(Color.Parse("#2A2D2E"));
        }
        else
        {
            Background = new SolidColorBrush(Colors.Transparent);
        }
    }
}


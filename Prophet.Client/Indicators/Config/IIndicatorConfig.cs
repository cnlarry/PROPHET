using System.Collections.Generic;

namespace Prophet.Client.Indicators.Config;

/// <summary>
/// 渲染类型枚举
/// </summary>
public enum RenderType
{
    Line,       // 线条（MA、EMA、RSI等）
    Band,       // 带状（BOLL、Keltner等）
    Bar,        // 柱状（MACD柱、成交量等）
    Dot,        // 点状（SAR等）
    Histogram,  // 直方图
    Custom      // 自定义渲染
}

/// <summary>
/// 指标配置接口（主副图统一）
/// </summary>
public interface IIndicatorConfig
{
    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// 渲染类型
    /// </summary>
    RenderType RenderType { get; }
    
    /// <summary>
    /// 线条配置列表（用于线条类和带状类指标）
    /// </summary>
    List<LineConfig>? Lines { get; set; }
    
    /// <summary>
    /// 是否使用副图（true=副图，false=主图）
    /// </summary>
    bool UseSubChart { get; set; }
    
    /// <summary>
    /// 副图高度占比（0.0-1.0，仅副图有效）
    /// </summary>
    double SubChartHeightRatio { get; set; }
    
    /// <summary>
    /// 固定Y轴范围（null表示自动）
    /// </summary>
    (double min, double max)? FixedYRange { get; set; }
    
    /// <summary>
    /// 是否在副图显示分隔线
    /// </summary>
    bool ShowSeparator { get; set; }
}

/// <summary>
/// 指标配置基类（提供默认实现）
/// </summary>
public abstract class IndicatorConfigBase : IIndicatorConfig
{
    public bool IsEnabled { get; set; } = true;
    public abstract RenderType RenderType { get; }
    public List<LineConfig>? Lines { get; set; }
    public bool UseSubChart { get; set; } = false;
    public double SubChartHeightRatio { get; set; } = 0.25;
    public (double min, double max)? FixedYRange { get; set; } = null;
    public bool ShowSeparator { get; set; } = true;
}


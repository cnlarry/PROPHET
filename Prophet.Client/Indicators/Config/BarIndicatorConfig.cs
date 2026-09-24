namespace Prophet.Client.Indicators.Config;

/// <summary>
/// 柱状指标配置基类（MACD柱、成交量等）
/// </summary>
public class BarIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Bar;
    
    /// <summary>
    /// 值数据键
    /// </summary>
    public string ValueKey { get; set; } = "value";
    
    /// <summary>
    /// 柱宽占间距的比例（0.0-1.0）
    /// </summary>
    public double BarWidthRatio { get; set; } = 0.6;
    
    /// <summary>
    /// 正值颜色
    /// </summary>
    public string PositiveColor { get; set; } = "#FF0000";
    
    /// <summary>
    /// 负值颜色
    /// </summary>
    public string NegativeColor { get; set; } = "#00FF00";
    
    /// <summary>
    /// 透明度（0.0-1.0）
    /// </summary>
    public double Opacity { get; set; } = 0.6;
}

/// <summary>
/// 成交量配置
/// </summary>
public class VolumeIndicatorConfig : BarIndicatorConfig
{
    /// <summary>
    /// 是否显示MAVOL均线
    /// </summary>
    public bool ShowMA { get; set; } = true;
    
    /// <summary>
    /// MA线配置
    /// </summary>
    public System.Collections.Generic.List<LineConfig>? MALines { get; set; }
    
    public VolumeIndicatorConfig()
    {
        UseSubChart = true; // 副图
        SubChartHeightRatio = 0.25;
        ValueKey = "volume";
        PositiveColor = "#26A69A"; // 涨：绿色（与主图K线一致）
        NegativeColor = "#EF5350"; // 跌：红色（与主图K线一致）
        BarWidthRatio = 0.8;
        Opacity = 0.6;
        
        // 默认MAVOL配置
        MALines = new System.Collections.Generic.List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma5", Label = "MA5", Color = "#FFD700", PERIOD = 5 },
            new() { IsEnabled = true, Key = "ma10", Label = "MA10", Color = "#00FFFF", PERIOD = 10 },
            new() { IsEnabled = false, Key = "ma20", Label = "MA20", Color = "#FF00FF", PERIOD = 20 }
        };
    }
}


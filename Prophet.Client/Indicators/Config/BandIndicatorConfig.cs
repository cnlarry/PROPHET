using System.Collections.Generic;

namespace Prophet.Client.Indicators.Config;

/// <summary>
/// 带状指标配置基类（BOLL、Keltner等）
/// </summary>
public class BandIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Band;
    
    /// <summary>
    /// 上轨数据键
    /// </summary>
    public string UpperKey { get; set; } = "upper";
    
    /// <summary>
    /// 下轨数据键
    /// </summary>
    public string LowerKey { get; set; } = "lower";
    
    /// <summary>
    /// 中轨数据键
    /// </summary>
    public string MiddleKey { get; set; } = "middle";
    
    /// <summary>
    /// 是否显示填充区域
    /// </summary>
    public bool ShowFill { get; set; } = false;
    
    /// <summary>
    /// 填充颜色
    /// </summary>
    public string FillColor { get; set; } = "#CCCCCC";
    
    /// <summary>
    /// 填充透明度（0.0-1.0）
    /// </summary>
    public double FillOpacity { get; set; } = 0.1;
    
    /// <summary>
    /// 创建默认线条配置（上、中、下三条线）
    /// </summary>
    protected List<LineConfig> CreateDefaultBandLines(
        string upperColor = "#FF6B6B", 
        string middleColor = "#FFD93D", 
        string lowerColor = "#6BCB77")
    {
        return new List<LineConfig>
        {
            new() { IsEnabled = true, Key = UpperKey, Label = "Upper", Color = upperColor, Thickness = 1.0 },
            new() { IsEnabled = true, Key = MiddleKey, Label = "Middle", Color = middleColor, Thickness = 1.5 },
            new() { IsEnabled = true, Key = LowerKey, Label = "Lower", Color = lowerColor, Thickness = 1.0 }
        };
    }
}

/// <summary>
/// BOLL布林带配置
/// </summary>
public class BOLLIndicatorConfig : BandIndicatorConfig
{
    /// <summary>
    /// 计算周期
    /// </summary>
    public int PERIOD { get; set; } = 20;
    
    /// <summary>
    /// 标准差倍数
    /// </summary>
    public double StdDevMultiplier { get; set; } = 2.0;
    
    public BOLLIndicatorConfig()
    {
        UseSubChart = false; // 主图
        Lines = CreateDefaultBandLines("#FF6B6B", "#FFD93D", "#6BCB77");
        ShowFill = false;
        FillColor = "#FFD93D";
        FillOpacity = 0.1;
    }
}

/// <summary>
/// Keltner通道配置
/// </summary>
public class KeltnerIndicatorConfig : BandIndicatorConfig
{
    /// <summary>
    /// EMA周期
    /// </summary>
    public int PERIOD { get; set; } = 20;
    
    /// <summary>
    /// ATR周期
    /// </summary>
    public int ATRPeriod { get; set; } = 10;
    
    /// <summary>
    /// ATR倍数
    /// </summary>
    public double Multiplier { get; set; } = 2.0;
    
    public KeltnerIndicatorConfig()
    {
        UseSubChart = false; // 主图
        Lines = CreateDefaultBandLines("#FF6B6B", "#FFD700", "#6BCB77");
        ShowFill = false;
        FillColor = "#FFD700";
        FillOpacity = 0.1;
    }
}


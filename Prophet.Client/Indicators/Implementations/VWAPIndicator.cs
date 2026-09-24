using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// VWAP成交量加权平均价配置
/// </summary>
public class VWAPIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    /// <summary>
    /// 周期（用于显示，如14）
    /// </summary>
    public int PERIOD { get; set; } = 14;
    
    /// <summary>
    /// 重置周期（Intraday/Daily/Weekly）
    /// </summary>
    public string ResetPeriod { get; set; } = "Daily";
    
    public VWAPIndicatorConfig()
    {
        UseSubChart = false; // 主图
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "vwap", Label = "VWAP", Color = "#FFA500", Thickness = 1.8 }
        };
    }
}

/// <summary>
/// VWAP成交量加权平均价指标
/// </summary>
public class VWAPIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly LineRenderer _renderer = new();
    
    public VWAPIndicator(VWAPIndicatorConfig config) 
        : base("VWAP", "成交量加权平均价", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as VWAPIndicatorConfig;
        if (config != null)
        {
            return $"{Name} - 重置周期: {config.ResetPeriod}";
        }
        return base.GetDescription();
    }
}


using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// WMA加权移动平均线配置
/// </summary>
public class WMAIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public List<int> Periods
    {
        get
        {
            var periods = new List<int>();
            if (Lines != null)
            {
                foreach (var line in Lines)
                {
                    if (line.PERIOD > 0)
                        periods.Add(line.PERIOD);
                }
            }
            return periods;
        }
    }
    
    public WMAIndicatorConfig()
    {
        UseSubChart = false; // 主图
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "wma10", Label = "WMA10", Color = "#FFFFFF", PERIOD = 10, Thickness = 1.2 },
            new() { IsEnabled = true, Key = "wma20", Label = "WMA20", Color = "#FFD700", PERIOD = 20, Thickness = 1.2 },
            new() { IsEnabled = false, Key = "wma30", Label = "WMA30", Color = "#FF00FF", PERIOD = 30, Thickness = 1.2 }
        };
    }
}

/// <summary>
/// WMA加权移动平均线指标
/// </summary>
public class WMAIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly LineRenderer _renderer = new();
    
    public WMAIndicator(WMAIndicatorConfig config) 
        : base("WMA", "加权移动平均线", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as WMAIndicatorConfig;
        if (config != null)
        {
            var periods = string.Join(", ", config.Periods);
            return $"{Name} - 周期: {periods}";
        }
        return base.GetDescription();
    }
}


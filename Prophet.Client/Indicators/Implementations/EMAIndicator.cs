using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// EMA指数移动平均线配置
/// </summary>
public class EMAIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    /// <summary>
    /// EMA周期列表（从Lines中提取）
    /// </summary>
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
    
    public EMAIndicatorConfig()
    {
        UseSubChart = false; // 主图
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ema12", Label = "EMA12", Color = "#FFFFFF", PERIOD = 12, Thickness = 1.2 },
            new() { IsEnabled = true, Key = "ema26", Label = "EMA26", Color = "#FFD700", PERIOD = 26, Thickness = 1.2 },
            new() { IsEnabled = false, Key = "ema50", Label = "EMA50", Color = "#FF00FF", PERIOD = 50, Thickness = 1.2 }
        };
    }
}

/// <summary>
/// EMA指数移动平均线指标
/// </summary>
public class EMAIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly LineRenderer _renderer = new();
    
    public EMAIndicator(EMAIndicatorConfig config) 
        : base("EMA", "指数移动平均线", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as EMAIndicatorConfig;
        if (config != null)
        {
            var periods = string.Join(", ", config.Periods);
            return $"{Name} - 周期: {periods}";
        }
        return base.GetDescription();
    }
}


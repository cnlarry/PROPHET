using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// TEMA三重指数移动平均线配置
/// </summary>
public class TEMAIndicatorConfig : IndicatorConfigBase
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
    
    public TEMAIndicatorConfig()
    {
        UseSubChart = false; // 主图
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = false, Key = "tema12", Label = "TEMA12", Color = "#FFFFFF", PERIOD = 12, Thickness = 1.2 },
            new() { IsEnabled = false, Key = "tema26", Label = "TEMA26", Color = "#FFD700", PERIOD = 26, Thickness = 1.2 },
            new() { IsEnabled = false, Key = "tema50", Label = "TEMA50", Color = "#FF00FF", PERIOD = 50, Thickness = 1.2 }
        };
    }
}

public class TEMAIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly LineRenderer _renderer = new();
    
    public TEMAIndicator(TEMAIndicatorConfig config) : base("TEMA", "三重指数移动平均", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
    }
}


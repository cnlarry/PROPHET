using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// DEMA双重指数移动平均线配置
/// </summary>
public class DEMAIndicatorConfig : IndicatorConfigBase
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
    
    public DEMAIndicatorConfig()
    {
        UseSubChart = false; // 主图
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = false, Key = "dema12", Label = "DEMA12", Color = "#FFFFFF", PERIOD = 12, Thickness = 1.2 },
            new() { IsEnabled = false, Key = "dema26", Label = "DEMA26", Color = "#FFD700", PERIOD = 26, Thickness = 1.2 },
            new() { IsEnabled = false, Key = "dema50", Label = "DEMA50", Color = "#FF00FF", PERIOD = 50, Thickness = 1.2 }
        };
    }
}

public class DEMAIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly LineRenderer _renderer = new();
    
    public DEMAIndicator(DEMAIndicatorConfig config) : base("DEMA", "双重指数移动平均", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
    }
}


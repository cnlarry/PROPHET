using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// DMI趋向指标配置
/// </summary>
public class DMIIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 14;
    
    public DMIIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = (0, 100);
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "adx", Label = "ADX", Color = "#FFD700", Thickness = 1.5 },
            new() { IsEnabled = true, Key = "plus_di", Label = "+DI", Color = "#00FF00", Thickness = 1.5 },
            new() { IsEnabled = true, Key = "minus_di", Label = "-DI", Color = "#FF0000", Thickness = 1.5 }
        };
    }
}

public class DMIIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public DMIIndicator(DMIIndicatorConfig config) : base("DMI", "DMI", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
    }
}


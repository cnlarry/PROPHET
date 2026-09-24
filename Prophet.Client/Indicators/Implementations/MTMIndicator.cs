using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// MTM动量指标配置
/// </summary>
public class MTMIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 12;
    public bool ShowMA { get; set; } = false;
    public List<LineConfig>? MALines { get; set; }
    
    public MTMIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = null;
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "MTM", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma6", Label = "MA6", Color = "#FFD700", PERIOD = 6 }
        };
    }
}

public class MTMIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public MTMIndicator(MTMIndicatorConfig config) : base("MTM", "MTM", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as MTMIndicatorConfig;
        _renderer.Render(this, context);
        
        if (config?.ShowMA == true && config.MALines != null)
        {
            var originalLines = Config.Lines;
            Config.Lines = config.MALines;
            _renderer.Render(this, context);
            Config.Lines = originalLines;
        }
    }
}


using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// EMV简易波动配置
/// </summary>
public class EMVIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 14;
    public bool ShowMA { get; set; } = false;
    public List<LineConfig>? MALines { get; set; }
    
    public EMVIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = null;
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "EMV", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma9", Label = "MA9", Color = "#FFD700", PERIOD = 9 }
        };
    }
}

public class EMVIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Volume;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public EMVIndicator(EMVIndicatorConfig config) : base("EMV", "EMV", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as EMVIndicatorConfig;
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


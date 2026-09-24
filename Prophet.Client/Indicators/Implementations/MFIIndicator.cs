using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// MFI资金流量指标配置
/// </summary>
public class MFIIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 14;
    
    public bool ShowMA { get; set; } = false;
    public List<LineConfig>? MALines { get; set; }
    
    public MFIIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = (0, 100);
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "MFI", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma5", Label = "MA5", Color = "#FFD700", PERIOD = 5 }
        };
    }
}

public class MFIIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public MFIIndicator(MFIIndicatorConfig config) : base("MFI", "MFI", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as MFIIndicatorConfig;
        if (config == null) return;
        
        // 如果启用多周期对比，只绘制多周期线；否则绘制主MFI线
        if (config.ShowMA && config.MALines != null)
        {
            // 多周期模式：只绘制多周期线
            var originalLines = Config.Lines;
            Config.Lines = config.MALines;
            _renderer.Render(this, context);
            Config.Lines = originalLines;
        }
        else
        {
            // 单周期模式：绘制主MFI线
            _renderer.Render(this, context);
        }
    }
}


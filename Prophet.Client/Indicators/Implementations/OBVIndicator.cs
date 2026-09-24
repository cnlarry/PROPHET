using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// OBV能量潮指标配置
/// </summary>
public class OBVIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    /// <summary>
    /// 是否显示MA
    /// </summary>
    public bool ShowMA { get; set; } = false;
    
    /// <summary>
    /// MA线配置
    /// </summary>
    public List<LineConfig>? MALines { get; set; }
    
    public OBVIndicatorConfig()
    {
        UseSubChart = true; // 副图
        SubChartHeightRatio = 0.25;
        FixedYRange = null; // 自动范围
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "OBV", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma5", Label = "MA5", Color = "#FFD700", PERIOD = 5 },
            new() { IsEnabled = true, Key = "ma10", Label = "MA10", Color = "#00FFFF", PERIOD = 10 },
            new() { IsEnabled = false, Key = "ma20", Label = "MA20", Color = "#FF00FF", PERIOD = 20 }
        };
    }
}

/// <summary>
/// OBV能量潮指标
/// </summary>
public class OBVIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Volume;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public OBVIndicator(OBVIndicatorConfig config) 
        : base("OBV", "OBV", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as OBVIndicatorConfig;
        if (config == null) return;
        
        _renderer.Render(this, context);
        
        if (config.ShowMA && config.MALines != null)
        {
            var originalLines = Config.Lines;
            Config.Lines = config.MALines;
            _renderer.Render(this, context);
            Config.Lines = originalLines;
        }
    }
    
    public override string GetDescription()
    {
        return $"{Name} - 成交量能量潮";
    }
}


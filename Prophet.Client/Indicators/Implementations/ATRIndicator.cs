using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// ATR平均真实波幅配置
/// </summary>
public class ATRIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    /// <summary>
    /// ATR周期
    /// </summary>
    public int PERIOD { get; set; } = 14;
    
    /// <summary>
    /// 是否显示MA
    /// </summary>
    public bool ShowMA { get; set; } = false;
    
    /// <summary>
    /// MA线配置
    /// </summary>
    public List<LineConfig>? MALines { get; set; }
    
    public ATRIndicatorConfig()
    {
        UseSubChart = true; // 副图
        SubChartHeightRatio = 0.25;
        FixedYRange = null; // 自动范围
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "ATR", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma5", Label = "MA5", Color = "#FFD700", PERIOD = 5 },
            new() { IsEnabled = false, Key = "ma10", Label = "MA10", Color = "#00FFFF", PERIOD = 10 }
        };
    }
}

/// <summary>
/// ATR平均真实波幅指标
/// </summary>
public class ATRIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Volatility;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public ATRIndicator(ATRIndicatorConfig config) 
        : base("ATR", "ATR", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as ATRIndicatorConfig;
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
        var config = Config as ATRIndicatorConfig;
        if (config != null)
        {
            return $"{Name} - 周期: {config.PERIOD}";
        }
        return base.GetDescription();
    }
}


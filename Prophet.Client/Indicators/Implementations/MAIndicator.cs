using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// MA移动平均线配置
/// </summary>
public class MAIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    /// <summary>
    /// MA周期列表（从Lines中提取）
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
    
    /// <summary>
    /// 构造函数（默认配置）
    /// </summary>
    public MAIndicatorConfig()
    {
        UseSubChart = false; // 主图
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma7", Label = "MA7", Color = "#FFFFFF", PERIOD = 7, Thickness = 1.2 },
            new() { IsEnabled = true, Key = "ma25", Label = "MA25", Color = "#FFD700", PERIOD = 25, Thickness = 1.2 },
            new() { IsEnabled = true, Key = "ma99", Label = "MA99", Color = "#FF00FF", PERIOD = 99, Thickness = 1.2 }
        };
    }
}

/// <summary>
/// MA移动平均线指标
/// </summary>
public class MAIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly LineRenderer _renderer = new();
    
    public MAIndicator(MAIndicatorConfig config) 
        : base("MA", "移动平均线", config)
    {
    }
    
    public MAIndicator(string id, string name, MAIndicatorConfig config)
        : base(id, name, config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        // 使用通用LineRenderer
        _renderer.Render(this, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as MAIndicatorConfig;
        if (config != null)
        {
            var periods = string.Join(", ", config.Periods);
            return $"{Name} - 周期: {periods}";
        }
        return base.GetDescription();
    }
}


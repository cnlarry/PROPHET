using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// 成交量指标
/// </summary>
public class VolumeIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Volume;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly BarRenderer _renderer = new();
    
    public VolumeIndicator(VolumeIndicatorConfig config)
        : base("Volume", "Volume", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        // 使用BarRenderer（会自动处理MAVOL）
        _renderer.Render(this, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as VolumeIndicatorConfig;
        if (config != null && config.ShowMA)
        {
            return $"{Name} + MAVOL";
        }
        return Name;
    }
}


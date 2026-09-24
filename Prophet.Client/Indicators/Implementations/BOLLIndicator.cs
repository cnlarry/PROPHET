using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// BOLL布林带指标
/// </summary>
public class BOLLIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Volatility;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly BandRenderer _renderer = new();
    
    public BOLLIndicator(BOLLIndicatorConfig config)
        : base("BOLL", "布林带", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        // 使用BandRenderer
        _renderer.Render(this, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as BOLLIndicatorConfig;
        if (config != null)
        {
            return $"{Name} - 周期: {config.PERIOD}, 标准差倍数: {config.StdDevMultiplier}";
        }
        return base.GetDescription();
    }
}


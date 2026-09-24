using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

public class KeltnerIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Volatility;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    private readonly BandRenderer _renderer = new();
    
    public KeltnerIndicator(KeltnerIndicatorConfig config) : base("Keltner", "Keltner通道", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
    }
    
    public override string GetDescription()
    {
        var config = Config as KeltnerIndicatorConfig;
        if (config != null)
        {
            return $"{Name} - 周期: {config.PERIOD}, ATR周期: {config.ATRPeriod}";
        }
        return base.GetDescription();
    }
}


using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// StochRSI随机RSI配置
/// </summary>
public class StochRSIIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int RSIPeriod { get; set; } = 14;
    public int StochPeriod { get; set; } = 14;
    public int KPeriod { get; set; } = 3;
    public int DPeriod { get; set; } = 3;
    
    public StochRSIIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = (0, 100);
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "k", Label = "K", Color = "#FFFFFF", Thickness = 1.5 },
            new() { IsEnabled = true, Key = "d", Label = "D", Color = "#FFD700", Thickness = 1.5 }
        };
    }
}

public class StochRSIIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public StochRSIIndicator(StochRSIIndicatorConfig config) : base("StochRSI", "Stochastic RSI", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
        
        // 绘制参考线
        var levels = new[] { 20.0, 50.0, 80.0 };
        foreach (var level in levels)
        {
            var y = context.ValueToY(level);
            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(context.LeftMargin, y),
                EndPoint = new Avalonia.Point(context.LeftMargin + context.Width, y),
                Stroke = context.GridColor,
                StrokeThickness = 1,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 3 }
            };
            context.Canvas.Children.Add(line);
        }
    }
}


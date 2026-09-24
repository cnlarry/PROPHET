using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// Aroon阿隆指标配置
/// </summary>
public class AroonIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 25;
    
    public AroonIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = (0, 100);
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "aroon_down", Label = "Aroon Down", Color = "#FF0000", Thickness = 1.5 },
            new() { IsEnabled = true, Key = "aroon_up", Label = "Aroon Up", Color = "#00FF00", Thickness = 1.5 }
        };
    }
}

public class AroonIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public AroonIndicator(AroonIndicatorConfig config) : base("Aroon", "Aroon", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
        
        // 绘制参考线
        var levels = new[] { 30.0, 50.0, 70.0 };
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


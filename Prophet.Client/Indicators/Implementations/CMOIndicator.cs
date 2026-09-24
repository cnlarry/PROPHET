using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// CMO钱德动量摆动配置
/// </summary>
public class CMOIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 14;
    public bool ShowMA { get; set; } = false;
    public List<LineConfig>? MALines { get; set; }
    
    public CMOIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = (-100, 100);
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "CMO", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma9", Label = "MA9", Color = "#FFD700", PERIOD = 9 }
        };
    }
}

public class CMOIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public CMOIndicator(CMOIndicatorConfig config) : base("CMO", "CMO", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as CMOIndicatorConfig;
        _renderer.Render(this, context);
        
        // 绘制参考线
        var levels = new[] { -50.0, 0.0, 50.0 };
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
        
        if (config?.ShowMA == true && config.MALines != null)
        {
            var originalLines = Config.Lines;
            Config.Lines = config.MALines;
            _renderer.Render(this, context);
            Config.Lines = originalLines;
        }
    }
}


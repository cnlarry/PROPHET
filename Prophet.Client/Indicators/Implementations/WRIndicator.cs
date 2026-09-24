using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// WR威廉指标配置
/// </summary>
public class WRIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 14;
    public bool ShowMA { get; set; } = false;
    public List<LineConfig>? MALines { get; set; }
    
    public WRIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = (-100, 0);
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "WR", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma5", Label = "MA5", Color = "#FFD700", PERIOD = 5 }
        };
    }
}

public class WRIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public WRIndicator(WRIndicatorConfig config) : base("WR", "Williams %R", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as WRIndicatorConfig;
        if (config == null) return;
        
        // 如果启用多周期对比，只绘制多周期线；否则绘制主WR线
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
            // 单周期模式：绘制主WR线
            _renderer.Render(this, context);
        }
        
        // 绘制参考线
        var levels = new[] { -20.0, -50.0, -80.0 };
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


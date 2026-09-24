using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// ROC变动率配置
/// </summary>
public class ROCIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 12;
    public bool ShowMA { get; set; } = false;
    public List<LineConfig>? MALines { get; set; }
    
    public ROCIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = null;
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "ROC", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma6", Label = "MA6", Color = "#FFD700", PERIOD = 6 }
        };
    }
}

public class ROCIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public ROCIndicator(ROCIndicatorConfig config) : base("ROC", "ROC", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as ROCIndicatorConfig;
        _renderer.Render(this, context);
        
        // 绘制零轴
        var zeroY = context.ValueToY(0);
        var line = new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(context.LeftMargin, zeroY),
            EndPoint = new Avalonia.Point(context.LeftMargin + context.Width, zeroY),
            Stroke = context.GridColor,
            StrokeThickness = 1,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 3 }
        };
        context.Canvas.Children.Add(line);
        
        if (config?.ShowMA == true && config.MALines != null)
        {
            var originalLines = Config.Lines;
            Config.Lines = config.MALines;
            _renderer.Render(this, context);
            Config.Lines = originalLines;
        }
    }
}


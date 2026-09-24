using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// CMF蔡金资金流量配置
/// </summary>
public class CMFIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    public int PERIOD { get; set; } = 20;
    public bool ShowMA { get; set; } = false;
    public List<LineConfig>? MALines { get; set; }
    
    public CMFIndicatorConfig()
    {
        UseSubChart = true;
        SubChartHeightRatio = 0.25;
        FixedYRange = null;
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "CMF", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma5", Label = "MA5", Color = "#FFD700", PERIOD = 5 }
        };
    }
}

public class CMFIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Volume;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public CMFIndicator(CMFIndicatorConfig config) : base("CMF", "CMF", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as CMFIndicatorConfig;
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


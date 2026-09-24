using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// KDJ随机指标配置
/// </summary>
public class KDJIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    /// <summary>
    /// RSV周期
    /// </summary>
    public int PERIOD { get; set; } = 9;
    
    /// <summary>
    /// K平滑周期
    /// </summary>
    public int KPeriod { get; set; } = 3;
    
    /// <summary>
    /// D平滑周期
    /// </summary>
    public int DPeriod { get; set; } = 3;
    
    public KDJIndicatorConfig()
    {
        UseSubChart = true; // 副图
        SubChartHeightRatio = 0.25;
        FixedYRange = (0, 100); // KDJ固定0-100
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "k", Label = "K", Color = "#FFFFFF", Thickness = 1.5 },
            new() { IsEnabled = true, Key = "d", Label = "D", Color = "#FFD700", Thickness = 1.5 },
            new() { IsEnabled = true, Key = "j", Label = "J", Color = "#FF00FF", Thickness = 1.5 }
        };
    }
}

/// <summary>
/// KDJ随机指标
/// </summary>
public class KDJIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public KDJIndicator(KDJIndicatorConfig config) 
        : base("KDJ", "KDJ", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        _renderer.Render(this, context);
        
        // 绘制参考线（20, 50, 80）
        RenderReferenceLines(context);
    }
    
    private void RenderReferenceLines(IndicatorRenderContext context)
    {
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
    
    public override string GetDescription()
    {
        var config = Config as KDJIndicatorConfig;
        if (config != null)
        {
            return $"{Name} ({config.PERIOD}, {config.KPeriod}, {config.DPeriod})";
        }
        return base.GetDescription();
    }
}


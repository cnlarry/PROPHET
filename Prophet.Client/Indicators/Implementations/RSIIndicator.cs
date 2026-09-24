using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// RSI相对强弱指标配置
/// </summary>
public class RSIIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Line;
    
    /// <summary>
    /// RSI周期
    /// </summary>
    public int PERIOD { get; set; } = 14;
    
    /// <summary>
    /// 超买线
    /// </summary>
    public double OverboughtLevel { get; set; } = 70;
    
    /// <summary>
    /// 超卖线
    /// </summary>
    public double OversoldLevel { get; set; } = 30;
    
    /// <summary>
    /// 是否显示MA
    /// </summary>
    public bool ShowMA { get; set; } = false;
    
    /// <summary>
    /// MA线配置
    /// </summary>
    public List<LineConfig>? MALines { get; set; }
    
    public RSIIndicatorConfig()
    {
        UseSubChart = true; // 副图
        SubChartHeightRatio = 0.25;
        FixedYRange = (0, 100); // RSI固定0-100
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "value", Label = "RSI", Color = "#FFFFFF", Thickness = 1.5 }
        };
        
        // 默认MA配置
        MALines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "ma5", Label = "MA5", Color = "#FFD700", PERIOD = 5, Thickness = 1.2 },
            new() { IsEnabled = false, Key = "ma10", Label = "MA10", Color = "#00FFFF", PERIOD = 10, Thickness = 1.2 }
        };
    }
}

/// <summary>
/// RSI相对强弱指标
/// </summary>
public class RSIIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Momentum;
    public override IndicatorLocation Location => IndicatorLocation.SubChart;
    
    private readonly LineRenderer _renderer = new();
    
    public RSIIndicator(RSIIndicatorConfig config) 
        : base("RSI", "RSI", config)
    {
    }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as RSIIndicatorConfig;
        if (config == null) return;
        
        // 如果启用多周期对比，只绘制多周期线；否则绘制主RSI线
        if (config.ShowMA && config.MALines != null)
        {
            // 多周期模式：只绘制多周期线
            RenderMA(config, context);
        }
        else
        {
            // 单周期模式：绘制主RSI线
            _renderer.Render(this, context);
        }
        
        // 绘制超买超卖线（无论单周期还是多周期都需要）
        RenderLevels(config, context);
    }
    
    private void RenderLevels(RSIIndicatorConfig config, IndicatorRenderContext context)
    {
        // 超买线（70）
        var overboughtY = context.ValueToY(config.OverboughtLevel);
        var overboughtLine = new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(context.LeftMargin, overboughtY),
            EndPoint = new Avalonia.Point(context.LeftMargin + context.Width, overboughtY),
            Stroke = context.GridColor,
            StrokeThickness = 1,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 3 }
        };
        context.Canvas.Children.Add(overboughtLine);
        
        // 超卖线（30）
        var oversoldY = context.ValueToY(config.OversoldLevel);
        var oversoldLine = new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(context.LeftMargin, oversoldY),
            EndPoint = new Avalonia.Point(context.LeftMargin + context.Width, oversoldY),
            Stroke = context.GridColor,
            StrokeThickness = 1,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 3 }
        };
        context.Canvas.Children.Add(oversoldLine);
    }
    
    private void RenderMA(RSIIndicatorConfig config, IndicatorRenderContext context)
    {
        var originalLines = Config.Lines;
        Config.Lines = config.MALines;
        _renderer.Render(this, context);
        Config.Lines = originalLines;
    }
    
    public override string GetDescription()
    {
        var config = Config as RSIIndicatorConfig;
        if (config != null)
        {
            return $"{Name} - 周期: {config.PERIOD}";
        }
        return base.GetDescription();
    }
}


using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Implementations;

/// <summary>
/// SAR抛物线转向配置
/// </summary>
public class SARIndicatorConfig : IndicatorConfigBase
{
    public override RenderType RenderType => RenderType.Dot;
    
    public double Acceleration { get; set; } = 0.02;
    public double MaxAcceleration { get; set; } = 0.2;
    public double DotSize { get; set; } = 3.0;
    
    public SARIndicatorConfig()
    {
        UseSubChart = false; // 主图
        
        Lines = new List<LineConfig>
        {
            new() { IsEnabled = true, Key = "sar", Label = "SAR", Color = "#00FFFF", Thickness = 3.0 }
        };
    }
}

public class SARIndicator : IndicatorBase
{
    public override IndicatorType Type => IndicatorType.Trend;
    public override IndicatorLocation Location => IndicatorLocation.MainChart;
    
    public SARIndicator(SARIndicatorConfig config) : base("SAR", "抛物线转向", config) { }
    
    public override void Render(IndicatorRenderContext context)
    {
        var config = Config as SARIndicatorConfig;
        if (config == null) return;
        
        var dataDict = Data.ToDictionary(d => d.Time, d => d);
        var lineConfig = config.Lines?.FirstOrDefault();
        if (lineConfig == null || !lineConfig.IsEnabled) return;
        
        var key = lineConfig.Key ?? "sar";
        var color = Color.Parse(lineConfig.Color);
        var dotSize = config.DotSize;
        
        for (int i = 0; i < context.Candles.Count; i++)
        {
            var candle = context.Candles[i];
            
            if (dataDict.TryGetValue(candle.Time, out var dataPoint))
            {
                var value = dataPoint.GetValue(key);
                var trend = dataPoint.GetValue("trend");
                
                if (!double.IsNaN(value))
                {
                    var x = context.IndexToX(i);
                    var y = context.ValueToY(value);
                    
                    if (context.IsYInRange(y))
                    {
                        // 根据趋势着色
                        var dotColor = trend > 0 
                            ? new SolidColorBrush(Color.Parse("#00FF00"))
                            : new SolidColorBrush(Color.Parse("#FF0000"));
                        
                        var ellipse = new Ellipse
                        {
                            Width = dotSize,
                            Height = dotSize,
                            Fill = dotColor
                        };
                        
                        Canvas.SetLeft(ellipse, x - dotSize / 2);
                        Canvas.SetTop(ellipse, y - dotSize / 2);
                        context.Canvas.Children.Add(ellipse);
                    }
                }
            }
        }
    }
}


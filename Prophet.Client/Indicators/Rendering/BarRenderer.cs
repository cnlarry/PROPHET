using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;

namespace Prophet.Client.Indicators.Rendering;

/// <summary>
/// 柱状渲染器（MACD柱、成交量等）
/// </summary>
public class BarRenderer : IIndicatorRenderer
{
    public void Render(IIndicator indicator, IndicatorRenderContext context)
    {
        var config = indicator.Config as BarIndicatorConfig;
        if (config == null) return;
        
        // 🚀 注意：context.Candles 已经是可见的K线数据，不需要再切片
        // 渲染每根柱
        for (int i = 0; i < context.Candles.Count; i++)
        {
            var candle = context.Candles[i];
            var dataPoint = indicator.Data.FindByTime(candle.Time);
            
            if (dataPoint != null)
            {
                var value = dataPoint.GetValue(config.ValueKey);
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    RenderBar(i, value, config, context);
                }
            }
        }
        
        // 渲染零轴线（如果在范围内）
        RenderZeroLine(context);
        
        // 如果是成交量且启用MA，渲染MA线
        if (config is VolumeIndicatorConfig volumeConfig && volumeConfig.ShowMA)
        {
            RenderVolumeMA(indicator, volumeConfig, context);
        }
    }
    
    /// <summary>
    /// 渲染单根柱
    /// </summary>
    private void RenderBar(int index, double value, BarIndicatorConfig config, IndicatorRenderContext context)
    {
        var x = context.IndexToX(index);
        var barWidth = context.Spacing * config.BarWidthRatio;
        
        var zeroY = context.ValueToY(0);
        var valueY = context.ValueToY(value);
        var height = Math.Abs(valueY - zeroY);
        
        // 选择颜色：对于成交量，根据K线涨跌；对于其他柱状图（MACD），根据正负值
        var color = config.PositiveColor;
        if (config is VolumeIndicatorConfig && index < context.Candles.Count)
        {
            // 成交量根据K线涨跌着色
            var candle = context.Candles[index];
            color = candle.IsRising ? config.PositiveColor : config.NegativeColor;
        }
        else
        {
            // 其他指标根据正负值着色
            color = value >= 0 ? config.PositiveColor : config.NegativeColor;
        }
        
        var rect = new Rectangle
        {
            Width = barWidth,
            Height = height,
            Fill = new SolidColorBrush(Color.Parse(color)) { Opacity = config.Opacity }
        };
        
        Canvas.SetLeft(rect, x - barWidth / 2);
        Canvas.SetTop(rect, Math.Min(zeroY, valueY));
        context.Canvas.Children.Add(rect);
    }
    
    /// <summary>
    /// 渲染零轴线
    /// </summary>
    private void RenderZeroLine(IndicatorRenderContext context)
    {
        if (context.MinValue <= 0 && context.MaxValue >= 0)
        {
            var zeroY = context.ValueToY(0);
            
            var line = new Line
            {
                StartPoint = new Avalonia.Point(context.LeftMargin, zeroY),
                EndPoint = new Avalonia.Point(context.LeftMargin + context.Width, zeroY),
                Stroke = context.GridColor,
                StrokeThickness = 1,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 3 }
            };
            
            context.Canvas.Children.Add(line);
        }
    }
    
    /// <summary>
    /// 渲染成交量MA线
    /// </summary>
    private void RenderVolumeMA(IIndicator indicator, VolumeIndicatorConfig config, IndicatorRenderContext context)
    {
        if (config.MALines == null) return;
        
        var lineRenderer = new LineRenderer();
        
        // 临时修改Lines配置来渲染MA
        var originalLines = indicator.Config.Lines;
        indicator.Config.Lines = config.MALines;
        
        lineRenderer.Render(indicator, context);
        
        // 恢复原配置
        indicator.Config.Lines = originalLines;
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Core;

namespace Prophet.Client.Indicators.Rendering;

/// <summary>
/// 带状渲染器（BOLL、Keltner等）
/// </summary>
public class BandRenderer : IIndicatorRenderer
{
    private readonly LineRenderer _lineRenderer = new();
    
    public void Render(IIndicator indicator, IndicatorRenderContext context)
    {
        var config = indicator.Config as BandIndicatorConfig;
        if (config == null) return;
        
        // 先绘制填充区域（在线条下层）
        if (config.ShowFill)
        {
            RenderFill(indicator, config, context);
        }
        
        // 再绘制线条（在填充上层）
        _lineRenderer.Render(indicator, context);
    }
    
    /// <summary>
    /// 渲染填充区域
    /// </summary>
    private void RenderFill(IIndicator indicator, BandIndicatorConfig config, IndicatorRenderContext context)
    {
        var upperPoints = new List<Point>();
        var lowerPoints = new List<Point>();
        
        // 🚀 注意：context.Candles 已经是可见的K线数据，不需要再切片
        // 构建上下轨点集
        for (int i = 0; i < context.Candles.Count; i++)
        {
            var candle = context.Candles[i];
            var dataPoint = indicator.Data.FindByTime(candle.Time);
            
            if (dataPoint != null)
            {
                var upper = dataPoint.GetValue(config.UpperKey);
                var lower = dataPoint.GetValue(config.LowerKey);
                
                if (!double.IsNaN(upper) && !double.IsNaN(lower))
                {
                    var x = context.IndexToX(i);
                    var yUpper = context.ValueToY(upper);
                    var yLower = context.ValueToY(lower);
                    
                    // 🔧 修复：不要过滤Y超出范围的点，让Polygon自动裁剪
                    // 这样填充区域可以正确延伸，避免BOLL带填充不完整
                    upperPoints.Add(new Point(x, yUpper));
                    lowerPoints.Add(new Point(x, yLower));
                }
            }
        }
        
        // 构建多边形（上轨正向 + 下轨反向，形成闭合区域）
        if (upperPoints.Count > 1 && lowerPoints.Count > 1)
        {
            var polygonPoints = new List<Point>();
            polygonPoints.AddRange(upperPoints);
            polygonPoints.AddRange(lowerPoints.AsEnumerable().Reverse());
            
            var polygon = new Polygon
            {
                Points = new Avalonia.Collections.AvaloniaList<Point>(polygonPoints),
                Fill = new SolidColorBrush(Color.Parse(config.FillColor)) 
                { 
                    Opacity = config.FillOpacity 
                },
                Stroke = null
            };
            
            context.Canvas.Children.Add(polygon);
        }
    }
}


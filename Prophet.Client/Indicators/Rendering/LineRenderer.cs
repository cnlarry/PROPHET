using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Prophet.Client.Indicators.Core;
using Prophet.Client.Models;

namespace Prophet.Client.Indicators.Rendering;

/// <summary>
/// 线条渲染器（通用）
/// 适用于：MA、EMA、RSI等单线或多线指标
/// </summary>
public class LineRenderer : IIndicatorRenderer
{
    public void Render(IIndicator indicator, IndicatorRenderContext context)
    {
        if (indicator.Config.Lines == null || indicator.Config.Lines.Count == 0)
            return;
        
        // 渲染各条线
        foreach (var lineConfig in indicator.Config.Lines.Where(l => l.IsEnabled))
        {
            RenderLine(indicator, lineConfig, context);
        }
        
        // 🚀 标题渲染已移至 UpdateCursorInfo 统一处理，不再在这里绘制
    }
    
    /// <summary>
    /// 渲染单条线
    /// </summary>
    private void RenderLine(IIndicator indicator, Config.LineConfig lineConfig, IndicatorRenderContext context)
    {
        var points = new List<Point>();
        
        // 🚀 注意：context.Candles 已经是可见的K线数据，不需要再切片
        // 构建点集
        for (int i = 0; i < context.Candles.Count; i++)
        {
            var candle = context.Candles[i];
            var dataPoint = indicator.Data.FindByTime(candle.Time);
            
            if (dataPoint != null)
            {
                var value = dataPoint.GetValue(lineConfig.Key);
                
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    var x = context.IndexToX(i);
                    var y = context.ValueToY(value);
                    
                    // 🔧 修复：不要过滤Y超出范围的点，让Polyline自动裁剪
                    // 这样可以确保线条正确延伸到视口边缘，避免BOLL下轨等线条"短缺"
                    points.Add(new Point(x, y));
                }
            }
        }
        
        // 绘制Polyline
        if (points.Count > 1)
        {
            var polyline = new Polyline
            {
                Points = new Avalonia.Collections.AvaloniaList<Point>(points),
                Stroke = new SolidColorBrush(Color.Parse(lineConfig.Color)),
                StrokeThickness = lineConfig.Thickness
            };
            
            ApplyLineStyle(polyline, lineConfig.Style);
            context.Canvas.Children.Add(polyline);
        }
    }
    
    /// <summary>
    /// 渲染标题和当前值
    /// </summary>
    private void RenderTitle(IIndicator indicator, IndicatorRenderContext context)
    {
        if (context.HoverIndex < 0 || context.HoverIndex >= context.Candles.Count)
            return;
        
        var titleBlock = new TextBlock 
        { 
            FontSize = 10,
            Foreground = context.LabelColor
        };
        
        titleBlock.Inlines = new Avalonia.Controls.Documents.InlineCollection();
        
        // 指标名称
        titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
        {
            Text = indicator.Name,
            Foreground = context.LabelColor
        });
        
        // 获取当前值
        var currentCandle = context.Candles[context.HoverIndex];
        var currentDataPoint = indicator.Data.FindByTime(currentCandle.Time);
        
        if (currentDataPoint != null && indicator.Config.Lines != null)
        {
            foreach (var lineConfig in indicator.Config.Lines.Where(l => l.IsEnabled))
            {
                var value = currentDataPoint.GetValue(lineConfig.Key);
                
                if (!double.IsNaN(value))
                {
                    titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                    {
                        Text = $" {lineConfig.Label}:",
                        Foreground = context.LabelColor
                    });
                    
                    titleBlock.Inlines.Add(new Avalonia.Controls.Documents.Run
                    {
                        Text = $"{value:F2}",
                        Foreground = new SolidColorBrush(Color.Parse(lineConfig.Color))
                    });
                }
            }
        }
        
        Canvas.SetLeft(titleBlock, context.LeftMargin + 5);
        Canvas.SetTop(titleBlock, context.TopMargin + 5 + context.TitleYOffset);
        context.Canvas.Children.Add(titleBlock);
    }
    
    /// <summary>
    /// 应用线形样式
    /// </summary>
    private void ApplyLineStyle(Polyline polyline, LineStyle style)
    {
        switch (style)
        {
            case LineStyle.Solid:
                // 实线，不需要设置
                break;
            case LineStyle.Dotted:
                polyline.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 2 };
                break;
            case LineStyle.Dashed:
                polyline.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 5, 3 };
                break;
            case LineStyle.Wave:
                polyline.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 10, 2, 2, 2 };
                break;
        }
    }
}


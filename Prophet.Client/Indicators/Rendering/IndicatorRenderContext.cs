using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Prophet.Client.Models;

namespace Prophet.Client.Indicators.Rendering;

/// <summary>
/// 指标渲染上下文（包含所有渲染所需信息）
/// </summary>
public class IndicatorRenderContext
{
    /// <summary>
    /// 画布
    /// </summary>
    public Canvas Canvas { get; set; } = null!;
    
    /// <summary>
    /// K线数据（用于时间对齐）
    /// </summary>
    public List<Candlestick> Candles { get; set; } = new();
    
    /// <summary>
    /// 绘图区域 - 左边距
    /// </summary>
    public double LeftMargin { get; set; }
    
    /// <summary>
    /// 绘图区域 - 上边距
    /// </summary>
    public double TopMargin { get; set; }
    
    /// <summary>
    /// 绘图区域 - 宽度
    /// </summary>
    public double Width { get; set; }
    
    /// <summary>
    /// 绘图区域 - 高度
    /// </summary>
    public double Height { get; set; }
    
    /// <summary>
    /// K线间距
    /// </summary>
    public double Spacing { get; set; }
    
    /// <summary>
    /// 可见起始索引
    /// </summary>
    public int StartIndex { get; set; }
    
    /// <summary>
    /// 可见K线数量
    /// </summary>
    public int VisibleCount { get; set; }
    
    /// <summary>
    /// Y轴最小值
    /// </summary>
    public double MinValue { get; set; }
    
    /// <summary>
    /// Y轴最大值
    /// </summary>
    public double MaxValue { get; set; }
    
    /// <summary>
    /// Y轴值范围
    /// </summary>
    public double ValueRange => MaxValue - MinValue;
    
    /// <summary>
    /// 鼠标位置
    /// </summary>
    public Point MousePosition { get; set; }
    
    /// <summary>
    /// 当前悬停的K线索引
    /// </summary>
    public int HoverIndex { get; set; }
    
    /// <summary>
    /// 标题垂直偏移量（用于多个指标标题排列）
    /// </summary>
    public double TitleYOffset { get; set; } = 0;
    
    /// <summary>
    /// 标签颜色（用于文本）
    /// </summary>
    public IBrush LabelColor { get; set; } = new SolidColorBrush(Color.Parse("#999999"));
    
    /// <summary>
    /// 网格颜色
    /// </summary>
    public IBrush GridColor { get; set; } = new SolidColorBrush(Color.Parse("#2C2E33"));
    
    /// <summary>
    /// 将数值转换为Y坐标
    /// </summary>
    /// <param name="value">数值</param>
    /// <returns>Y坐标</returns>
    public double ValueToY(double value)
    {
        if (ValueRange < 1e-10) 
            return TopMargin + Height / 2;
        
        return TopMargin + (1 - (value - MinValue) / ValueRange) * Height;
    }
    
    /// <summary>
    /// 将Y坐标转换为数值
    /// </summary>
    /// <param name="y">Y坐标</param>
    /// <returns>数值</returns>
    public double YToValue(double y)
    {
        if (ValueRange < 1e-10)
            return MinValue;
        
        return MinValue + (1 - (y - TopMargin) / Height) * ValueRange;
    }
    
    /// <summary>
    /// 将索引转换为X坐标（K线中心点）
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>X坐标</returns>
    public double IndexToX(int index)
    {
        return LeftMargin + index * Spacing + Spacing / 2;
    }
    
    /// <summary>
    /// 将X坐标转换为索引
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <returns>索引</returns>
    public int XToIndex(double x)
    {
        return (int)((x - LeftMargin) / Spacing);
    }
    
    /// <summary>
    /// 检查Y坐标是否在有效范围内
    /// </summary>
    public bool IsYInRange(double y)
    {
        return y >= TopMargin && y <= TopMargin + Height;
    }
    
    /// <summary>
    /// 检查X坐标是否在有效范围内
    /// </summary>
    public bool IsXInRange(double x)
    {
        return x >= LeftMargin && x <= LeftMargin + Width;
    }
    
    /// <summary>
    /// 检查点是否在绘图区域内
    /// </summary>
    public bool IsPointInRange(Point point)
    {
        return IsXInRange(point.X) && IsYInRange(point.Y);
    }
}


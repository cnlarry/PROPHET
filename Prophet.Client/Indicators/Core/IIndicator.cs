using System;
using System.Collections.Generic;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Data;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Core;

/// <summary>
/// 指标类型枚举
/// </summary>
public enum IndicatorType
{
    Trend,          // 趋势类（MA、EMA、BOLL）
    Momentum,       // 动量类（RSI、KDJ、MACD）
    Volume,         // 成交量类（Volume、OBV）
    Volatility,     // 波动率类（ATR、BOLL）
    Custom          // 自定义
}

/// <summary>
/// 指标位置枚举
/// </summary>
public enum IndicatorLocation
{
    MainChart,      // 主图（覆盖在K线上）
    SubChart,       // 副图（独立区域）
    Overlay         // 叠加（可选主图/副图，如成交量）
}

/// <summary>
/// 指标接口（主副图统一）
/// </summary>
public interface IIndicator
{
    /// <summary>
    /// 指标唯一标识（如 "MA", "MACD", "RSI"）
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// 指标显示名称（如 "移动平均线", "MACD指标"）
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 指标类型
    /// </summary>
    IndicatorType Type { get; }
    
    /// <summary>
    /// 绘制位置
    /// </summary>
    IndicatorLocation Location { get; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// 配置对象
    /// </summary>
    IIndicatorConfig Config { get; }
    
    /// <summary>
    /// 数据序列
    /// </summary>
    IndicatorDataSeries Data { get; set; }
    
    /// <summary>
    /// 渲染指标（由渲染器调用或自定义实现）
    /// </summary>
    /// <param name="context">渲染上下文</param>
    void Render(IndicatorRenderContext context);
    
    /// <summary>
    /// 获取当前值（用于光标显示）
    /// </summary>
    /// <param name="dataIndex">数据索引</param>
    /// <returns>键值对字典（key: 数据键, value: 数值）</returns>
    Dictionary<string, double> GetCurrentValues(int dataIndex);
    
    /// <summary>
    /// 获取Y轴范围（用于自动缩放）
    /// </summary>
    /// <param name="startIndex">起始索引</param>
    /// <param name="count">数量</param>
    /// <returns>(最小值, 最大值)</returns>
    (double min, double max) GetYRange(int startIndex, int count);
    
    /// <summary>
    /// 清空数据
    /// </summary>
    void ClearData();
    
    /// <summary>
    /// 获取指标描述信息
    /// </summary>
    string GetDescription();
}


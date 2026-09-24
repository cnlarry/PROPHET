using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Indicators.Config;
using Prophet.Client.Indicators.Data;
using Prophet.Client.Indicators.Rendering;

namespace Prophet.Client.Indicators.Core;

/// <summary>
/// 指标基类（实现公共逻辑）
/// </summary>
public abstract class IndicatorBase : IIndicator
{
    public string Id { get; protected set; }
    public string Name { get; protected set; }
    public abstract IndicatorType Type { get; }
    public abstract IndicatorLocation Location { get; }
    public bool IsEnabled { get; set; }
    public IIndicatorConfig Config { get; protected set; }
    public IndicatorDataSeries Data { get; set; }
    
    protected IndicatorBase(string id, string name, IIndicatorConfig config)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Data = new IndicatorDataSeries();
        IsEnabled = true;
    }
    
    /// <summary>
    /// 渲染方法（子类可重写或使用默认渲染器）
    /// </summary>
    public abstract void Render(IndicatorRenderContext context);
    
    /// <summary>
    /// 获取当前值（默认实现）
    /// </summary>
    public virtual Dictionary<string, double> GetCurrentValues(int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= Data.Count)
            return new Dictionary<string, double>();
        
        return Data[dataIndex].GetAllValues();
    }
    
    /// <summary>
    /// 获取Y轴范围（默认实现）
    /// </summary>
    public virtual (double min, double max) GetYRange(int startIndex, int count)
    {
        var visibleData = Data.Skip(startIndex).Take(count).ToList();
        if (visibleData.Count == 0)
            return (0, 0);
        
        var allValues = visibleData
            .SelectMany(d => d.GetAllValues().Values)
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .ToList();
        
        if (allValues.Count == 0)
            return (0, 0);
        
        var min = allValues.Min();
        var max = allValues.Max();
        
        // 添加5%的边距
        var range = max - min;
        if (range < 1e-10) range = Math.Abs(max * 0.1);
        
        return (min - range * 0.05, max + range * 0.05);
    }
    
    /// <summary>
    /// 清空数据
    /// </summary>
    public virtual void ClearData()
    {
        Data.Clear();
    }
    
    /// <summary>
    /// 获取指标描述
    /// </summary>
    public virtual string GetDescription()
    {
        return $"{Name} ({Id})";
    }
    
    /// <summary>
    /// ToString重写
    /// </summary>
    public override string ToString()
    {
        return $"{Name} [{(IsEnabled ? "启用" : "禁用")}]";
    }
}


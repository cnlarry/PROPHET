using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Indicators.Data;

/// <summary>
/// 统一的指标数据点（主副图通用）
/// </summary>
public class IndicatorDataPoint
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Time { get; set; }
    
    /// <summary>
    /// 数据值存储
    /// </summary>
    private readonly Dictionary<string, double> _values = new();
    
    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="key">数据键</param>
    /// <returns>值，不存在则返回NaN</returns>
    public double GetValue(string key)
    {
        return _values.GetValueOrDefault(key, double.NaN);
    }
    
    /// <summary>
    /// 设置值
    /// </summary>
    /// <param name="key">数据键</param>
    /// <param name="value">值</param>
    public void SetValue(string key, double value)
    {
        _values[key] = value;
    }
    
    /// <summary>
    /// 获取所有值（副本）
    /// </summary>
    public Dictionary<string, double> GetAllValues()
    {
        return new Dictionary<string, double>(_values);
    }
    
    /// <summary>
    /// 获取所有键
    /// </summary>
    public IEnumerable<string> GetKeys()
    {
        return _values.Keys;
    }
    
    /// <summary>
    /// 检查是否包含指定键
    /// </summary>
    public bool ContainsKey(string key)
    {
        return _values.ContainsKey(key);
    }
    
    /// <summary>
    /// 移除指定键
    /// </summary>
    public bool RemoveKey(string key)
    {
        return _values.Remove(key);
    }
    
    /// <summary>
    /// 获取值的数量
    /// </summary>
    public int Count => _values.Count;
    
    /// <summary>
    /// 检查是否所有值都是NaN
    /// </summary>
    public bool IsAllNaN()
    {
        return _values.Values.All(v => double.IsNaN(v));
    }
    
    /// <summary>
    /// 检查是否有任何有效值
    /// </summary>
    public bool HasValidValue()
    {
        return _values.Values.Any(v => !double.IsNaN(v) && !double.IsInfinity(v));
    }
    
    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
        var values = string.Join(", ", _values.Select(kvp => $"{kvp.Key}={kvp.Value:F2}"));
        return $"[{Time:yyyy-MM-dd HH:mm}] {values}";
    }
}


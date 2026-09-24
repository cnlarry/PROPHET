using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Indicators.Data;

/// <summary>
/// 指标数据序列
/// </summary>
public class IndicatorDataSeries : List<IndicatorDataPoint>
{
    /// <summary>
    /// 根据时间查找数据点
    /// </summary>
    /// <param name="time">时间</param>
    /// <returns>数据点，不存在则返回null</returns>
    public IndicatorDataPoint? FindByTime(DateTime time)
    {
        return this.FirstOrDefault(d => d.Time == time);
    }
    
    /// <summary>
    /// 根据时间查找索引
    /// </summary>
    /// <param name="time">时间</param>
    /// <returns>索引，不存在则返回-1</returns>
    public int FindIndexByTime(DateTime time)
    {
        return this.FindIndex(d => d.Time == time);
    }
    
    /// <summary>
    /// 批量添加数据（单键）
    /// </summary>
    /// <param name="times">时间列表</param>
    /// <param name="key">数据键</param>
    /// <param name="values">值列表</param>
    public void AddRange(IEnumerable<DateTime> times, string key, IEnumerable<double> values)
    {
        var timeList = times.ToList();
        var valueList = values.ToList();
        
        for (int i = 0; i < Math.Min(timeList.Count, valueList.Count); i++)
        {
            var dp = FindByTime(timeList[i]);
            if (dp == null)
            {
                dp = new IndicatorDataPoint { Time = timeList[i] };
                Add(dp);
            }
            dp.SetValue(key, valueList[i]);
        }
    }
    
    /// <summary>
    /// 批量添加数据（多键）
    /// </summary>
    /// <param name="times">时间列表</param>
    /// <param name="dataDict">数据字典（键 → 值列表）</param>
    public void AddRange(IEnumerable<DateTime> times, Dictionary<string, IEnumerable<double>> dataDict)
    {
        var timeList = times.ToList();
        
        foreach (var kvp in dataDict)
        {
            var key = kvp.Key;
            var valueList = kvp.Value.ToList();
            
            for (int i = 0; i < Math.Min(timeList.Count, valueList.Count); i++)
            {
                var dp = FindByTime(timeList[i]);
                if (dp == null)
                {
                    dp = new IndicatorDataPoint { Time = timeList[i] };
                    Add(dp);
                }
                dp.SetValue(key, valueList[i]);
            }
        }
    }
    
    /// <summary>
    /// 获取指定键的所有值
    /// </summary>
    /// <param name="key">数据键</param>
    /// <returns>值列表</returns>
    public List<double> GetValues(string key)
    {
        return this.Select(dp => dp.GetValue(key)).ToList();
    }
    
    /// <summary>
    /// 获取指定时间范围的数据
    /// </summary>
    /// <param name="startTime">起始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <returns>数据序列</returns>
    public IndicatorDataSeries GetRange(DateTime startTime, DateTime endTime)
    {
        var result = new IndicatorDataSeries();
        result.AddRange(this.Where(dp => dp.Time >= startTime && dp.Time <= endTime));
        return result;
    }
    
    /// <summary>
    /// 按时间排序
    /// </summary>
    public void SortByTime()
    {
        this.Sort((a, b) => a.Time.CompareTo(b.Time));
    }
    
    /// <summary>
    /// 移除无效数据点（所有值都是NaN的点）
    /// </summary>
    /// <returns>移除的数量</returns>
    public int RemoveInvalidPoints()
    {
        return this.RemoveAll(dp => dp.IsAllNaN());
    }
    
    /// <summary>
    /// 获取指定键的统计信息
    /// </summary>
    /// <param name="key">数据键</param>
    /// <returns>(最小值, 最大值, 平均值)</returns>
    public (double min, double max, double avg) GetStatistics(string key)
    {
        var values = this.Select(dp => dp.GetValue(key))
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .ToList();
        
        if (values.Count == 0)
            return (double.NaN, double.NaN, double.NaN);
        
        return (values.Min(), values.Max(), values.Average());
    }
    
    /// <summary>
    /// 克隆数据序列
    /// </summary>
    public IndicatorDataSeries Clone()
    {
        var result = new IndicatorDataSeries();
        foreach (var dp in this)
        {
            var newDp = new IndicatorDataPoint { Time = dp.Time };
            foreach (var key in dp.GetKeys())
            {
                newDp.SetValue(key, dp.GetValue(key));
            }
            result.Add(newDp);
        }
        return result;
    }
}


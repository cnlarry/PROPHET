using Prophet.Client.Models;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Indicators;

/// <summary>
/// 副图指标数据适配器
/// 将通用的 SubChartDataPoint 转换为旧的数据模型（MACDData、RSIData等）
/// 用于兼容现有的UI渲染代码
/// </summary>
public static class SubChartDataAdapter
{
    /// <summary>
    /// SubChartDataPoint -> MACDData
    /// </summary>
    public static List<MACDData> ToMACDData(List<SubChartDataPoint> points)
    {
        if (points == null || points.Count == 0)
        {
            return new List<MACDData>();
        }
        
        return points.Select(p => new MACDData
        {
            Time = p.Time,
            DIF = p.GetValue("dif"),
            DEA = p.GetValue("dea"),
            Histogram = p.GetValue("histogram")  // 修复：使用正确的key "histogram"
        }).ToList();
    }
    
    /// <summary>
    /// SubChartDataPoint -> RSIData
    /// </summary>
    public static List<RSIData> ToRSIData(List<SubChartDataPoint> points)
    {
        if (points == null || points.Count == 0)
        {
            return new List<RSIData>();
        }
        
        return points.Select(p => new RSIData
        {
            Time = p.Time,
            Value = p.GetValue("value")
        }).ToList();
    }
    
    /// <summary>
    /// SubChartDataPoint -> ATRData
    /// </summary>
    public static List<ATRData> ToATRData(List<SubChartDataPoint> points)
    {
        if (points == null || points.Count == 0)
        {
            return new List<ATRData>();
        }
        
        return points.Select(p => new ATRData
        {
            Time = p.Time,
            Value = p.GetValue("value")
        }).ToList();
    }
    
    /// <summary>
    /// SubChartDataPoint -> VolumeData (未来可能需要)
    /// 注意：当前Volume可能没有专门的VolumeData类
    /// </summary>
    public static List<SubChartDataPoint> ToVolumeData(List<SubChartDataPoint> points)
    {
        // Volume数据可能已经是SubChartDataPoint格式
        // 这里直接返回，或者未来可以转换为专用的VolumeData类
        return points;
    }
    
    /// <summary>
    /// MACDData -> SubChartDataPoint (反向转换，用于兼容性)
    /// </summary>
    public static List<SubChartDataPoint> FromMACDData(List<MACDData> macdData)
    {
        if (macdData == null || macdData.Count == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        return macdData.Select(m => new SubChartDataPoint
        {
            Time = m.Time,
            Values = new()
            {
                ["dif"] = m.DIF,
                ["dea"] = m.DEA,
                ["histogram"] = m.Histogram  // 修复：使用正确的key "histogram"
            }
        }).ToList();
    }
    
    /// <summary>
    /// RSIData -> SubChartDataPoint (反向转换，用于兼容性)
    /// </summary>
    public static List<SubChartDataPoint> FromRSIData(List<RSIData> rsiData)
    {
        if (rsiData == null || rsiData.Count == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        return rsiData.Select(r => new SubChartDataPoint
        {
            Time = r.Time,
            Values = new() { ["value"] = r.Value }
        }).ToList();
    }
}


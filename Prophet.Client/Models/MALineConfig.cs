using System.Collections.Generic;

namespace Prophet.Client.Models;

/// <summary>
/// MA线条配置
/// </summary>
public class MALineConfig
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// 周期
    /// </summary>
    public int PERIOD { get; set; }
    
    /// <summary>
    /// 计算字段（Open/High/Low/Close）
    /// </summary>
    public PriceField Field { get; set; } = PriceField.Close;
    
    /// <summary>
    /// 线形样式
    /// </summary>
    public LineStyle Style { get; set; } = LineStyle.Solid;
    
    /// <summary>
    /// 颜色
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";
}

/// <summary>
/// 价格字段
/// </summary>
public enum PriceField
{
    Open,   // 开盘价
    High,   // 最高价
    Low,    // 最低价
    Close   // 收盘价
}

/// <summary>
/// 线形样式
/// </summary>
public enum LineStyle
{
    Solid,      // 实线
    Dotted,     // 点线
    Dashed,     // 虚线
    Wave        // 波浪线
}

/// <summary>
/// MA类型指标配置
/// </summary>
public class MATypeIndicatorConfig
{
    /// <summary>
    /// 是否启用整个指标
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// 8条MA线配置
    /// </summary>
    public List<MALineConfig> Lines { get; set; } = new();
    
    public MATypeIndicatorConfig()
    {
        // 默认启用MA指标
        IsEnabled = true;
        
        // 初始化8条线，前3条有默认值（7, 25, 99），后5条为0由用户设置
        Lines = new List<MALineConfig>
        {
            new MALineConfig { IsEnabled = true, PERIOD = 7, Color = "#FFFFFF" },      // MA1: 白色
            new MALineConfig { IsEnabled = true, PERIOD = 25, Color = "#FFD700" },     // MA2: 金色
            new MALineConfig { IsEnabled = true, PERIOD = 99, Color = "#FF00FF" },     // MA3: 紫色
            new MALineConfig { IsEnabled = false, PERIOD = 0, Color = "#00FFFF" },     // MA4: 青色
            new MALineConfig { IsEnabled = false, PERIOD = 0, Color = "#FFA500" },     // MA5: 橙色
            new MALineConfig { IsEnabled = false, PERIOD = 0, Color = "#00FF00" },     // MA6: 绿色
            new MALineConfig { IsEnabled = false, PERIOD = 0, Color = "#FF1493" },     // MA7: 粉色
            new MALineConfig { IsEnabled = false, PERIOD = 0, Color = "#1E90FF" }      // MA8: 蓝色
        };
    }
}


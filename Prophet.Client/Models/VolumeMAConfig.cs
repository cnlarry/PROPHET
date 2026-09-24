using System.Collections.Generic;

namespace Prophet.Client.Models;

/// <summary>
/// Volume MA线条配置
/// </summary>
public class VolumeMALineConfig
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
    /// 线形样式
    /// </summary>
    public LineStyle Style { get; set; } = LineStyle.Solid;
    
    /// <summary>
    /// 颜色
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";
    
    /// <summary>
    /// 线宽（1.0-5.0）
    /// </summary>
    public double Thickness { get; set; } = 1.5;
}

/// <summary>
/// Volume指标配置（包含MAVOL）
/// </summary>
public class VolumeIndicatorConfig
{
    /// <summary>
    /// 是否启用Volume副图
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 是否显示MAVOL均线
    /// </summary>
    public bool ShowMA { get; set; } = true;
    
    /// <summary>
    /// MAVOL线条配置（最多3条）
    /// </summary>
    public List<VolumeMALineConfig> MALines { get; set; } = new();
    
    public VolumeIndicatorConfig()
    {
        // 默认配置3条MAVOL线
        MALines = new List<VolumeMALineConfig>
        {
            new VolumeMALineConfig { IsEnabled = true, PERIOD = 5, Color = "#FFD700", Style = LineStyle.Solid, Thickness = 1.8 },   // MA5: 金色，实线，粗
            new VolumeMALineConfig { IsEnabled = true, PERIOD = 10, Color = "#00FFFF", Style = LineStyle.Dashed, Thickness = 1.5 },  // MA10: 青色，虚线，中
            new VolumeMALineConfig { IsEnabled = false, PERIOD = 20, Color = "#FF00FF", Style = LineStyle.Dotted, Thickness = 1.2 }  // MA20: 紫色，点线，细
        };
    }
}


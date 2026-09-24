using System.Collections.Generic;

namespace Prophet.Client.Models;

/// <summary>
/// 副图指标MA线配置（通用）
/// </summary>
public class SubChartMALineConfig
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
/// 副图指标MA配置（包含是否显示MA和MA线列表）
/// </summary>
public class SubChartMAConfig
{
    /// <summary>
    /// 是否显示MA均线
    /// </summary>
    public bool ShowMA { get; set; } = false;
    
    /// <summary>
    /// MA线配置（最多3条）
    /// </summary>
    public List<SubChartMALineConfig> MALines { get; set; } = new();
    
    /// <summary>
    /// 创建默认配置（3条MA线）
    /// </summary>
    public static SubChartMAConfig CreateDefault(int PERIOD1 = 5, int PERIOD2 = 10, int PERIOD3 = 20,
        string color1 = "#FFD700", string color2 = "#00FFFF", string color3 = "#FF00FF")
    {
        return new SubChartMAConfig
        {
            ShowMA = false,
            MALines = new List<SubChartMALineConfig>
            {
                new SubChartMALineConfig { IsEnabled = true, PERIOD = PERIOD1, Color = color1, Style = LineStyle.Solid, Thickness = 1.8 },
                new SubChartMALineConfig { IsEnabled = true, PERIOD = PERIOD2, Color = color2, Style = LineStyle.Dashed, Thickness = 1.5 },
                new SubChartMALineConfig { IsEnabled = false, PERIOD = PERIOD3, Color = color3, Style = LineStyle.Dotted, Thickness = 1.2 }
            }
        };
    }
}


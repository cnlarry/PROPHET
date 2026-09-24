using Prophet.Client.Models; // 使用现有的LineStyle枚举

namespace Prophet.Client.Indicators.Config;

/// <summary>
/// 线条配置（通用）
/// </summary>
public class LineConfig
{
    /// <summary>
    /// 是否启用该线
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 数据键（从IndicatorDataPoint中提取值的键）
    /// </summary>
    public string Key { get; set; } = "value";
    
    /// <summary>
    /// 线条标签（显示名称）
    /// </summary>
    public string Label { get; set; } = "";
    
    /// <summary>
    /// 颜色（十六进制格式，如 "#FFFFFF"）
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";
    
    /// <summary>
    /// 线形样式
    /// </summary>
    public LineStyle Style { get; set; } = LineStyle.Solid;
    
    /// <summary>
    /// 线宽
    /// </summary>
    public double Thickness { get; set; } = 1.5;
    
    /// <summary>
    /// 周期（仅MA类指标使用，其他指标可忽略）
    /// </summary>
    public int PERIOD { get; set; } = 0;
    
    /// <summary>
    /// 克隆配置
    /// </summary>
    public LineConfig Clone()
    {
        return new LineConfig
        {
            IsEnabled = this.IsEnabled,
            Key = this.Key,
            Label = this.Label,
            Color = this.Color,
            Style = this.Style,
            Thickness = this.Thickness,
            PERIOD = this.PERIOD
        };
    }
}


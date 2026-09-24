using System.Collections.Generic;

namespace Prophet.Client.Models;

/// <summary>
/// 指标位置枚举
/// </summary>
public enum IndicatorLocation
{
    MainChart,  // 主图
    SubChart    // 副图
}

/// <summary>
/// 指标配置接口（主副图统一）
/// </summary>
public interface IIndicatorConfig
{
    /// <summary>
    /// 指标名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// 指标位置
    /// </summary>
    IndicatorLocation Location { get; }
}

/// <summary>
/// 主图指标配置基类
/// </summary>
public abstract class MainChartIndicatorConfig : IIndicatorConfig
{
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; }
    public IndicatorLocation Location => IndicatorLocation.MainChart;
    
    /// <summary>
    /// 线条配置列表
    /// </summary>
    public List<IndicatorLineConfig> Lines { get; set; } = new();
}

/// <summary>
/// 单线配置（用于Lines列表）
/// </summary>
public class IndicatorLineConfig
{
    /// <summary>
    /// 是否启用该线
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 数据键（用于从DataPoint中提取值）
    /// </summary>
    public string? Key { get; set; }
    
    /// <summary>
    /// 线条标签（显示名称）
    /// </summary>
    public string Label { get; set; } = "";
    
    /// <summary>
    /// 颜色
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
}

/// <summary>
/// BOLL布林带配置
/// </summary>
public class BOLLIndicatorConfig : MainChartIndicatorConfig
{
    /// <summary>
    /// 计算周期
    /// </summary>
    public int PERIOD { get; set; } = 20;
    
    /// <summary>
    /// 标准差倍数
    /// </summary>
    public double StdDevMultiplier { get; set; } = 2.0;
    
    /// <summary>
    /// 是否显示填充区域
    /// </summary>
    public bool ShowFill { get; set; } = false;
    
    /// <summary>
    /// 填充区域透明度 (0.0-1.0)
    /// </summary>
    public double FillOpacity { get; set; } = 0.1;
    
    public BOLLIndicatorConfig()
    {
        Name = "BOLL";
        IsEnabled = false;
        
        Lines = new List<IndicatorLineConfig>
        {
            new() { IsEnabled = true, Key = "upper", Label = "Upper", Color = "#FF6B6B", Style = LineStyle.Solid, Thickness = 1.0 },
            new() { IsEnabled = true, Key = "middle", Label = "Middle", Color = "#FFD93D", Style = LineStyle.Solid, Thickness = 1.5 },
            new() { IsEnabled = true, Key = "lower", Label = "Lower", Color = "#6BCB77", Style = LineStyle.Solid, Thickness = 1.0 }
        };
    }
}

/// <summary>
/// Keltner通道配置
/// </summary>
public class KeltnerIndicatorConfig : MainChartIndicatorConfig
{
    public int PERIOD { get; set; } = 20;
    public int ATRPeriod { get; set; } = 10;
    public double Multiplier { get; set; } = 2.0;
    public bool ShowFill { get; set; } = false;
    public double FillOpacity { get; set; } = 0.1;
    
    public KeltnerIndicatorConfig()
    {
        Name = "Keltner";
        IsEnabled = false;
        
        Lines = new List<IndicatorLineConfig>
        {
            new() { IsEnabled = true, Key = "upper", Label = "Upper", Color = "#FF6B6B", Style = LineStyle.Dashed },
            new() { IsEnabled = true, Key = "middle", Label = "Middle", Color = "#FFD700", Style = LineStyle.Solid, Thickness = 1.5 },
            new() { IsEnabled = true, Key = "lower", Label = "Lower", Color = "#6BCB77", Style = LineStyle.Dashed }
        };
    }
}

/// <summary>
/// SAR抛物线指标配置
/// </summary>
public class SARIndicatorConfig : MainChartIndicatorConfig
{
    /// <summary>
    /// 加速因子
    /// </summary>
    public double Acceleration { get; set; } = 0.02;
    
    /// <summary>
    /// 最大加速因子
    /// </summary>
    public double MaxAcceleration { get; set; } = 0.2;
    
    /// <summary>
    /// 点的大小
    /// </summary>
    public double DotSize { get; set; } = 3.0;
    
    public SARIndicatorConfig()
    {
        Name = "SAR";
        IsEnabled = false;
        
        Lines = new List<IndicatorLineConfig>
        {
            new() { IsEnabled = true, Key = "sar", Label = "SAR", Color = "#00FFFF", Thickness = 3.0 }
        };
    }
}

/// <summary>
/// VWAP成交量加权平均价配置
/// </summary>
public class VWAPIndicatorConfig : MainChartIndicatorConfig
{
    /// <summary>
    /// 重置周期（Intraday/Daily/Weekly）
    /// </summary>
    public string ResetPeriod { get; set; } = "Daily";
    
    public VWAPIndicatorConfig()
    {
        Name = "VWAP";
        IsEnabled = false;
        
        Lines = new List<IndicatorLineConfig>
        {
            new() { IsEnabled = true, Key = "vwap", Label = "VWAP", Color = "#FFA500", Style = LineStyle.Solid, Thickness = 1.8 }
        };
    }
}

/// <summary>
/// Ichimoku一目均衡表配置
/// </summary>
public class IchimokuIndicatorConfig : MainChartIndicatorConfig
{
    public int TenkanPeriod { get; set; } = 9;
    public int KijunPeriod { get; set; } = 26;
    public int SenkouBPeriod { get; set; } = 52;
    public int Displacement { get; set; } = 26;
    public bool ShowCloud { get; set; } = true;
    
    public IchimokuIndicatorConfig()
    {
        Name = "Ichimoku";
        IsEnabled = false;
        
        Lines = new List<IndicatorLineConfig>
        {
            new() { IsEnabled = true, Key = "tenkan", Label = "Tenkan", Color = "#FF0000", Thickness = 1.0 },
            new() { IsEnabled = true, Key = "kijun", Label = "Kijun", Color = "#0000FF", Thickness = 1.0 },
            new() { IsEnabled = true, Key = "senkou_a", Label = "Senkou A", Color = "#00FF00", Thickness = 1.0 },
            new() { IsEnabled = true, Key = "senkou_b", Label = "Senkou B", Color = "#FF00FF", Thickness = 1.0 },
            new() { IsEnabled = true, Key = "chikou", Label = "Chikou", Color = "#FFD700", Thickness = 1.0 }
        };
    }
}

/// <summary>
/// 主图指标集合配置
/// </summary>
public class MainChartSettings
{
    /// <summary>
    /// MA配置（已有）
    /// </summary>
    public MATypeIndicatorConfig? MA { get; set; }
    
    /// <summary>
    /// EMA配置（已有）
    /// </summary>
    public MATypeIndicatorConfig? EMA { get; set; }
    
    /// <summary>
    /// WMA配置（已有）
    /// </summary>
    public MATypeIndicatorConfig? WMA { get; set; }
    
    /// <summary>
    /// DEMA配置（已有）
    /// </summary>
    public MATypeIndicatorConfig? DEMA { get; set; }
    
    /// <summary>
    /// TEMA配置（已有）
    /// </summary>
    public MATypeIndicatorConfig? TEMA { get; set; }
    
    /// <summary>
    /// BOLL配置（新架构）
    /// </summary>
    public BOLLIndicatorConfig BOLL { get; set; } = new();
    
    /// <summary>
    /// Keltner配置（新架构）
    /// </summary>
    public KeltnerIndicatorConfig Keltner { get; set; } = new();
    
    /// <summary>
    /// SAR配置（新架构）
    /// </summary>
    public SARIndicatorConfig SAR { get; set; } = new();
    
    /// <summary>
    /// VWAP配置（新架构）
    /// </summary>
    public VWAPIndicatorConfig VWAP { get; set; } = new();
    
    /// <summary>
    /// Ichimoku配置（新架构）
    /// </summary>
    public IchimokuIndicatorConfig Ichimoku { get; set; } = new();
    
    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static MainChartSettings CreateDefault()
    {
        return new MainChartSettings
        {
            MA = new MATypeIndicatorConfig { IsEnabled = true },
            BOLL = new BOLLIndicatorConfig { IsEnabled = false },
            SAR = new SARIndicatorConfig { IsEnabled = false },
            VWAP = new VWAPIndicatorConfig { IsEnabled = false },
            Keltner = new KeltnerIndicatorConfig { IsEnabled = false },
            Ichimoku = new IchimokuIndicatorConfig { IsEnabled = false }
        };
    }
}


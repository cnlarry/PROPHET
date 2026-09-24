using System.Collections.Generic;

namespace Prophet.Client.Models;

/// <summary>
/// 副图设置
/// </summary>
public class SubChartSettings
{
    /// <summary>
    /// 成交量副图是否启用
    /// </summary>
    public bool IsVolumeEnabled { get; set; } = true;
    
    /// <summary>
    /// MACD副图是否启用
    /// </summary>
    public bool IsMACDEnabled { get; set; } = true;
    
    /// <summary>
    /// RSI副图是否启用
    /// </summary>
    public bool IsRSIEnabled { get; set; } = true;
    
    /// <summary>
    /// ATR副图是否启用
    /// </summary>
    public bool IsATREnabled { get; set; } = true;
    
    /// <summary>
    /// MACD参数：快线周期
    /// </summary>
    public int MACDFastPeriod { get; set; } = 12;
    
    /// <summary>
    /// MACD参数：慢线周期
    /// </summary>
    public int MACDSlowPeriod { get; set; } = 26;
    
    /// <summary>
    /// MACD参数：信号线周期
    /// </summary>
    public int MACDSignalPeriod { get; set; } = 9;
    
    /// <summary>
    /// RSI参数：周期
    /// </summary>
    public int RSIPeriod { get; set; } = 14;
    
    /// <summary>
    /// ATR参数：周期
    /// </summary>
    public int ATRPeriod { get; set; } = 14;
    
    // ==================== 新增指标配置 ====================
    
    /// <summary>
    /// MFI副图是否启用
    /// </summary>
    public bool IsMFIEnabled { get; set; } = false;
    public int MFIPeriod { get; set; } = 14;
    
    /// <summary>
    /// OBV副图是否启用
    /// </summary>
    public bool IsOBVEnabled { get; set; } = false;
    
    /// <summary>
    /// KDJ副图是否启用
    /// </summary>
    public bool IsKDJEnabled { get; set; } = false;
    public int KDJPeriod { get; set; } = 9;
    public int KDJKPeriod { get; set; } = 3;
    public int KDJDPeriod { get; set; } = 3;
    
    /// <summary>
    /// StochRSI副图是否启用
    /// </summary>
    public bool IsStochRSIEnabled { get; set; } = false;
    public int StochRSIPeriod { get; set; } = 14;
    public int StochRSIKPeriod { get; set; } = 3;
    public int StochRSIDPeriod { get; set; } = 3;
    
    /// <summary>
    /// CCI副图是否启用
    /// </summary>
    public bool IsCCIEnabled { get; set; } = false;
    public int CCIPeriod { get; set; } = 20;
    
    /// <summary>
    /// DMI副图是否启用
    /// </summary>
    public bool IsDMIEnabled { get; set; } = false;
    public int DMIPeriod { get; set; } = 14;
    
    /// <summary>
    /// WR副图是否启用
    /// </summary>
    public bool IsWREnabled { get; set; } = false;
    public int WRPeriod { get; set; } = 14;
    
    /// <summary>
    /// CMF副图是否启用
    /// </summary>
    public bool IsCMFEnabled { get; set; } = false;
    public int CMFPeriod { get; set; } = 20;
    
    /// <summary>
    /// ROC副图是否启用
    /// </summary>
    public bool IsROCEnabled { get; set; } = false;
    public int ROCPeriod { get; set; } = 12;
    
    /// <summary>
    /// EMV副图是否启用
    /// </summary>
    public bool IsEMVEnabled { get; set; } = false;
    public int EMVPeriod { get; set; } = 14;
    
    /// <summary>
    /// MTM副图是否启用
    /// </summary>
    public bool IsMTMEnabled { get; set; } = false;
    public int MTMPeriod { get; set; } = 12;
    
    /// <summary>
    /// CMO副图是否启用
    /// </summary>
    public bool IsCMOEnabled { get; set; } = false;
    public int CMOPeriod { get; set; } = 14;
    
    /// <summary>
    /// Aroon副图是否启用
    /// </summary>
    public bool IsAroonEnabled { get; set; } = false;
    public int AroonPeriod { get; set; } = 25;
    
    // ==================== Volume MAVOL配置 ====================
    
    /// <summary>
    /// Volume是否显示MAVOL均线
    /// </summary>
    public bool VolumeShowMA { get; set; } = true;
    
    /// <summary>
    /// MAVOL线配置
    /// </summary>
    public List<VolumeMALineConfig> VolumeMALines { get; set; } = new()
    {
        new VolumeMALineConfig { IsEnabled = true, PERIOD = 5, Color = "#FFD700", Style = LineStyle.Solid, Thickness = 1.8 },   // MA5: 金色，实线，粗
        new VolumeMALineConfig { IsEnabled = true, PERIOD = 10, Color = "#00FFFF", Style = LineStyle.Dashed, Thickness = 1.5 },  // MA10: 青色，虚线，中
        new VolumeMALineConfig { IsEnabled = false, PERIOD = 20, Color = "#FF00FF", Style = LineStyle.Dotted, Thickness = 1.2 }  // MA20: 紫色，点线，细
    };
    
    // ==================== 单线副图指标MA配置 ====================
    
    /// <summary>
    /// RSI指标多周期配置
    /// </summary>
    public SubChartMAConfig RSIMultiPeriod { get; set; } = SubChartMAConfig.CreateDefault(7, 14, 21);
    
    /// <summary>
    /// MFI指标多周期配置
    /// </summary>
    public SubChartMAConfig MFIMultiPeriod { get; set; } = SubChartMAConfig.CreateDefault(7, 14, 21);
    
    /// <summary>
    /// CCI指标多周期配置
    /// </summary>
    public SubChartMAConfig CCIMultiPeriod { get; set; } = SubChartMAConfig.CreateDefault(14, 20, 50);
    
    /// <summary>
    /// WR指标多周期配置
    /// </summary>
    public SubChartMAConfig WRMultiPeriod { get; set; } = SubChartMAConfig.CreateDefault(9, 14, 21);
    
    /// <summary>
    /// CCI指标MA配置
    /// </summary>
    public SubChartMAConfig CCIMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// WR指标MA配置
    /// </summary>
    public SubChartMAConfig WRMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// ROC指标MA配置
    /// </summary>
    public SubChartMAConfig ROCMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// CMF指标MA配置
    /// </summary>
    public SubChartMAConfig CMFMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// EMV指标MA配置
    /// </summary>
    public SubChartMAConfig EMVMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// MTM指标MA配置
    /// </summary>
    public SubChartMAConfig MTMMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// CMO指标MA配置
    /// </summary>
    public SubChartMAConfig CMOMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// ATR指标MA配置
    /// </summary>
    public SubChartMAConfig ATRMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
    
    /// <summary>
    /// OBV指标MA配置
    /// </summary>
    public SubChartMAConfig OBVMA { get; set; } = SubChartMAConfig.CreateDefault(5, 10, 20);
}


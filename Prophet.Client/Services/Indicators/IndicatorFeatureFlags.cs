using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Indicators;

/// <summary>
/// 指标功能开关
/// 用于控制哪些指标功能启用，便于分阶段开发和调试
/// </summary>
public static class IndicatorFeatureFlags
{
    // ==================== 已实现的指标（C++端已有） ====================
    
    /// <summary>RSI - 已实现</summary>
    public const bool EnableRSI = true;
    
    /// <summary>MACD - 已实现</summary>
    public const bool EnableMACD = true;
    
    /// <summary>ATR - 已实现</summary>
    public const bool EnableATR = true;
    
    /// <summary>Volume - 不需要Native计算</summary>
    public const bool EnableVolume = true;
    
    // ==================== 新指标（需要C++端实现） ====================
    
    /// <summary>
    /// 主开关：是否启用所有新指标的Native接口
    /// 设置为false可以避免编译错误（如果C++端未实现）
    /// </summary>
    public const bool EnableNewIndicatorsNative = true;  // ✅ C++端已实现并编译成功
    
    /// <summary>MFI - 资金流量指标</summary>
    public static bool EnableMFI => EnableNewIndicatorsNative;
    
    /// <summary>OBV - 能量潮</summary>
    public static bool EnableOBV => EnableNewIndicatorsNative;
    
    /// <summary>KDJ - 随机指标</summary>
    public static bool EnableKDJ => EnableNewIndicatorsNative;
    
    /// <summary>StochRSI - 随机RSI</summary>
    public static bool EnableStochRSI => EnableNewIndicatorsNative;
    
    /// <summary>CCI - 商品通道指标</summary>
    public static bool EnableCCI => EnableNewIndicatorsNative;
    
    /// <summary>DMI - 趋向指标</summary>
    public static bool EnableDMI => EnableNewIndicatorsNative;
    
    /// <summary>WR - 威廉指标</summary>
    public static bool EnableWR => EnableNewIndicatorsNative;
    
    /// <summary>CMF - 蔡金资金流量</summary>
    public static bool EnableCMF => EnableNewIndicatorsNative;
    
    /// <summary>ROC - 变动率指标</summary>
    public static bool EnableROC => EnableNewIndicatorsNative;
    
    /// <summary>EMV - 简易波动指标</summary>
    public static bool EnableEMV => EnableNewIndicatorsNative;
    
    /// <summary>MTM - 动量指标</summary>
    public static bool EnableMTM => EnableNewIndicatorsNative;
    
    /// <summary>CMO - Chande动量振荡器</summary>
    public static bool EnableCMO => EnableNewIndicatorsNative;
    
    /// <summary>Aroon - 阿隆指标</summary>
    public static bool EnableAroon => EnableNewIndicatorsNative;
    
    // ==================== 功能开关 ====================
    
    /// <summary>是否在指标不可用时抛出异常（false则返回空数据）</summary>
    public const bool ThrowOnUnavailable = false;
    
    /// <summary>是否启用详细日志</summary>
    public const bool EnableVerboseLogging = true;
    
    /// <summary>是否启用性能分析</summary>
    public const bool EnablePerformanceProfiling = false;
    
    // ==================== 工具方法 ====================
    
    /// <summary>
    /// 检查指标是否可用
    /// </summary>
    public static bool IsIndicatorAvailable(string indicatorId)
    {
        return indicatorId.ToLower() switch
        {
            "rsi" => EnableRSI,
            "macd" => EnableMACD,
            "atr" => EnableATR,
            "volume" => EnableVolume,
            "mfi" => EnableMFI,
            "obv" => EnableOBV,
            "kdj" => EnableKDJ,
            "stochrsi" => EnableStochRSI,
            "cci" => EnableCCI,
            "dmi" => EnableDMI,
            "wr" => EnableWR,
            "cmf" => EnableCMF,
            "roc" => EnableROC,
            "emv" => EnableEMV,
            "mtm" => EnableMTM,
            "cmo" => EnableCMO,
            "aroon" => EnableAroon,
            _ => false
        };
    }
    
    /// <summary>
    /// 获取所有可用的指标ID列表
    /// </summary>
    public static List<string> GetAvailableIndicators()
    {
        var indicators = new List<string>();
        
        if (EnableRSI) indicators.Add("rsi");
        if (EnableMACD) indicators.Add("macd");
        if (EnableATR) indicators.Add("atr");
        if (EnableVolume) indicators.Add("volume");
        if (EnableMFI) indicators.Add("mfi");
        if (EnableOBV) indicators.Add("obv");
        if (EnableKDJ) indicators.Add("kdj");
        if (EnableStochRSI) indicators.Add("stochrsi");
        if (EnableCCI) indicators.Add("cci");
        if (EnableDMI) indicators.Add("dmi");
        if (EnableWR) indicators.Add("wr");
        if (EnableCMF) indicators.Add("cmf");
        if (EnableROC) indicators.Add("roc");
        if (EnableEMV) indicators.Add("emv");
        if (EnableMTM) indicators.Add("mtm");
        if (EnableCMO) indicators.Add("cmo");
        if (EnableAroon) indicators.Add("aroon");
        
        return indicators;
    }
    
    /// <summary>
    /// 打印功能状态
    /// </summary>
    public static void PrintStatus()
    {
    }
}


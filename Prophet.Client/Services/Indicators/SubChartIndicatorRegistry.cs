using Prophet.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Indicators;

/// <summary>
/// 副图指标注册表 - 元数据驱动
/// 所有指标的元数据集中在这里管理，避免分散到各处
/// 
/// 使用方式：
/// 1. 添加新指标只需在RegisterAllIndicators()中添加元数据
/// 2. UI自动根据元数据生成配置界面
/// 3. 绘制逻辑根据Type自动选择
/// </summary>
public static class SubChartIndicatorRegistry
{
    /// <summary>
    /// 指标元数据
    /// </summary>
    public class IndicatorMetadata
    {
        /// <summary>指标唯一标识</summary>
        public string Id { get; init; } = "";
        
        /// <summary>指标显示名称</summary>
        public string Name { get; init; } = "";
        
        /// <summary>指标分类（动量、趋势、成交量、波动性）</summary>
        public string Category { get; init; } = "";
        
        /// <summary>指标描述</summary>
        public string Description { get; init; } = "";
        
        /// <summary>指标类型（决定绘制方式）</summary>
        public SubChartIndicatorType Type { get; init; }
        
        /// <summary>参数定义</summary>
        public Dictionary<string, ParameterDefinition> Parameters { get; init; } = new();
        
        /// <summary>默认颜色配置</summary>
        public Dictionary<string, string> DefaultColors { get; init; } = new();
        
        /// <summary>Y轴最小值（NaN表示自动计算）</summary>
        public double MinValue { get; init; } = double.NaN;
        
        /// <summary>Y轴最大值（NaN表示自动计算）</summary>
        public double MaxValue { get; init; } = double.NaN;
        
        /// <summary>参考线列表</summary>
        public List<ReferenceLine> ReferenceLines { get; init; } = new();
        
        /// <summary>输出值的key列表（用于多输出指标）</summary>
        public List<string> OutputKeys { get; init; } = new() { "value" };
    }
    
    /// <summary>
    /// 参数定义
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>参数内部名称</summary>
        public string Name { get; init; } = "";
        
        /// <summary>参数显示名称</summary>
        public string DisplayName { get; init; } = "";
        
        /// <summary>参数值类型</summary>
        public Type ValueType { get; init; } = typeof(int);
        
        /// <summary>默认值</summary>
        public object DefaultValue { get; init; } = 14;
        
        /// <summary>最小值</summary>
        public object MinValue { get; init; } = 1;
        
        /// <summary>最大值</summary>
        public object MaxValue { get; init; } = 500;
        
        /// <summary>单位（可选）</summary>
        public string Unit { get; init; } = "";
        
        /// <summary>提示信息（可选）</summary>
        public string Tooltip { get; init; } = "";
    }
    
    /// <summary>
    /// 参考线定义
    /// </summary>
    public class ReferenceLine
    {
        /// <summary>参考线数值</summary>
        public double Value { get; init; }
        
        /// <summary>参考线颜色</summary>
        public string Color { get; init; } = "#666666";
        
        /// <summary>参考线标签</summary>
        public string Label { get; init; } = "";
        
        /// <summary>是否为虚线</summary>
        public bool IsDashed { get; init; } = true;
        
        /// <summary>线宽</summary>
        public double Thickness { get; init; } = 1.0;
    }
    
    private static readonly Dictionary<string, IndicatorMetadata> _indicators = new();
    private static bool _isInitialized = false;
    
    /// <summary>
    /// 静态构造函数
    /// </summary>
    static SubChartIndicatorRegistry()
    {
        RegisterAllIndicators();
    }
    
    /// <summary>
    /// 注册所有指标 - 添加新指标只需在这里添加元数据
    /// </summary>
    private static void RegisterAllIndicators()
    {
        if (_isInitialized) return;
        
        // ==================== 已实现的指标 ====================
        
        RegisterVolume();
        RegisterRSI();
        RegisterMACD();
        
        // ==================== 待实现的指标（第一批：高优先级） ====================
        
        RegisterMFI();
        RegisterOBV();
        RegisterKDJ();
        RegisterATR();
        RegisterStochRSI();
        
        // ==================== 待实现的指标（第二批：中优先级） ====================
        
        RegisterCCI();
        RegisterDMI();
        RegisterWR();
        RegisterCMF();
        RegisterROC();
        
        // ==================== 待实现的指标（第三批：低优先级） ====================
        
        RegisterEMV();
        RegisterMTM();
        RegisterCMO();
        RegisterAroon();
        
        _isInitialized = true;
    }
    
    // ==================== 已实现的指标注册方法 ====================
    
    private static void RegisterVolume()
    {
        Register(new IndicatorMetadata
        {
            Id = "volume",
            Name = "Volume 成交量",
            Category = "成交量",
            Description = "显示每根K线的成交量，上涨为绿色，下跌为红色",
            Type = SubChartIndicatorType.Histogram,
            MinValue = 0,
            MaxValue = double.NaN, // 自动计算
            Parameters = new(),
            DefaultColors = new()
            {
                ["up"] = "#26A69A",      // 绿色
                ["down"] = "#EF5350"     // 红色
            },
            ReferenceLines = new(),
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterRSI()
    {
        Register(new IndicatorMetadata
        {
            Id = "rsi",
            Name = "RSI 相对强弱指标",
            Category = "动量",
            Description = "衡量价格变动的速度和幅度，范围0-100。RSI>70超买，RSI<30超卖",
            Type = SubChartIndicatorType.Line,
            MinValue = 0,
            MaxValue = 100,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    ValueType = typeof(int),
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "计算RSI的周期数，常用值为6、12、14、24"
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#F0B90B"     // 金黄色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 70, Color = "#EF5350", Label = "超买", IsDashed = true, Thickness = 1 },
                new ReferenceLine { Value = 50, Color = "#666666", Label = "", IsDashed = true, Thickness = 0.5 },
                new ReferenceLine { Value = 30, Color = "#26A69A", Label = "超卖", IsDashed = true, Thickness = 1 }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterMACD()
    {
        Register(new IndicatorMetadata
        {
            Id = "macd",
            Name = "MACD 指数平滑移动平均",
            Category = "趋势",
            Description = "由DIF、DEA和柱状图组成。DIF上穿DEA为金叉（买入信号），下穿为死叉（卖出信号）",
            Type = SubChartIndicatorType.LineWithHistogram,
            MinValue = double.NaN,  // 自动计算
            MaxValue = double.NaN,  // 自动计算
            Parameters = new()
            {
                ["fastPeriod"] = new ParameterDefinition
                {
                    Name = "fastPeriod",
                    DisplayName = "快线周期",
                    ValueType = typeof(int),
                    DefaultValue = 12,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "快速EMA的周期，通常为12"
                },
                ["slowPeriod"] = new ParameterDefinition
                {
                    Name = "slowPeriod",
                    DisplayName = "慢线周期",
                    ValueType = typeof(int),
                    DefaultValue = 26,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "慢速EMA的周期，通常为26"
                },
                ["signalPeriod"] = new ParameterDefinition
                {
                    Name = "signalPeriod",
                    DisplayName = "信号线周期",
                    ValueType = typeof(int),
                    DefaultValue = 9,
                    MinValue = 2,
                    MaxValue = 50,
                    Tooltip = "DEA信号线的周期，通常为9"
                }
            },
            DefaultColors = new()
            {
                ["macd"] = "#2196F3",       // 蓝色
                ["signal"] = "#FF9800",       // 橙色
                ["histogram"] = "#26A69A", // 绿色（柱状图会根据正负变色）
                ["histogramNegative"] = "#EF5350"  // 红色（负柱）
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 0, Color = "#666666", Label = "", IsDashed = false, Thickness = 1 }
            },
            OutputKeys = new() { "macd", "signal", "histogram" }
        });
    }
    
    // ==================== 第一批：高优先级指标 ====================
    
    private static void RegisterMFI()
    {
        Register(new IndicatorMetadata
        {
            Id = "mfi",
            Name = "MFI 资金流量指标",
            Category = "成交量",
            Description = "结合价格和成交量的动量指标，范围0-100。MFI>80超买，MFI<20超卖",
            Type = SubChartIndicatorType.Line,
            MinValue = 0,
            MaxValue = 100,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    ValueType = typeof(int),
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "计算MFI的周期数，通常为14"
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#9C27B0"       // 紫色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 80, Color = "#EF5350", Label = "超买", IsDashed = true },
                new ReferenceLine { Value = 50, Color = "#666666", Label = "", IsDashed = true },
                new ReferenceLine { Value = 20, Color = "#26A69A", Label = "超卖", IsDashed = true }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterOBV()
    {
        Register(new IndicatorMetadata
        {
            Id = "obv",
            Name = "OBV 能量潮",
            Category = "成交量",
            Description = "累积成交量变化，判断资金流向。OBV上升表示资金流入，下降表示流出",
            Type = SubChartIndicatorType.Line,
            MinValue = double.NaN,  // 自动计算
            MaxValue = double.NaN,  // 自动计算
            Parameters = new(),
            DefaultColors = new()
            {
                ["line"] = "#2196F3"       // 蓝色
            },
            ReferenceLines = new(),
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterKDJ()
    {
        Register(new IndicatorMetadata
        {
            Id = "kdj",
            Name = "KDJ 随机指标",
            Category = "动量",
            Description = "由K、D、J三条线组成。KDJ>80超买区，KDJ<20超卖区。J线最敏感",
            Type = SubChartIndicatorType.MultiLine,
            MinValue = 0,
            MaxValue = 100,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "RSV周期",
                    DefaultValue = 9,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "计算RSV的周期，通常为9"
                },
                ["kPeriod"] = new ParameterDefinition
                {
                    Name = "kPeriod",
                    DisplayName = "K平滑周期",
                    DefaultValue = 3,
                    MinValue = 1,
                    MaxValue = 50,
                    Tooltip = "K值的平滑周期，通常为3"
                },
                ["dPeriod"] = new ParameterDefinition
                {
                    Name = "dPeriod",
                    DisplayName = "D平滑周期",
                    DefaultValue = 3,
                    MinValue = 1,
                    MaxValue = 50,
                    Tooltip = "D值的平滑周期，通常为3"
                }
            },
            DefaultColors = new()
            {
                ["k"] = "#FFFFFF",         // 白色
                ["d"] = "#FFD700",         // 金色
                ["j"] = "#FF00FF"          // 紫红色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 80, Color = "#EF5350", Label = "超买", IsDashed = true },
                new ReferenceLine { Value = 50, Color = "#666666", Label = "", IsDashed = true },
                new ReferenceLine { Value = 20, Color = "#26A69A", Label = "超卖", IsDashed = true }
            },
            OutputKeys = new() { "k", "d", "j" }
        });
    }
    
    private static void RegisterATR()
    {
        Register(new IndicatorMetadata
        {
            Id = "atr",
            Name = "ATR 平均真实波幅",
            Category = "波动性",
            Description = "衡量市场波动性，数值越高表示波动越大。用于评估市场活跃度和设置止损",
            Type = SubChartIndicatorType.Line,
            MinValue = 0,
            MaxValue = double.NaN,  // 自动计算
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 14,
                    MinValue = 1,
                    MaxValue = 100,
                    Tooltip = "计算ATR的周期数，通常为14"
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#2196F3"       // 蓝色
            },
            ReferenceLines = new(),
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterStochRSI()
    {
        Register(new IndicatorMetadata
        {
            Id = "stochrsi",
            Name = "StochRSI 随机RSI",
            Category = "动量",
            Description = "RSI的随机指标版本，比RSI更加敏感。StochRSI>0.8超买，StochRSI<0.2超卖",
            Type = SubChartIndicatorType.MultiLine,
            MinValue = 0,
            MaxValue = 100,
            Parameters = new()
            {
                ["rsiPeriod"] = new ParameterDefinition
                {
                    Name = "rsiPeriod",
                    DisplayName = "RSI周期",
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "RSI计算周期"
                },
                ["stochPeriod"] = new ParameterDefinition
                {
                    Name = "stochPeriod",
                    DisplayName = "Stoch周期",
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "随机指标周期"
                },
                ["kPeriod"] = new ParameterDefinition
                {
                    Name = "kPeriod",
                    DisplayName = "K平滑周期",
                    DefaultValue = 3,
                    MinValue = 1,
                    MaxValue = 50,
                    Tooltip = "K线平滑周期"
                },
                ["dPeriod"] = new ParameterDefinition
                {
                    Name = "dPeriod",
                    DisplayName = "D平滑周期",
                    DefaultValue = 3,
                    MinValue = 1,
                    MaxValue = 50,
                    Tooltip = "D线平滑周期"
                }
            },
            DefaultColors = new()
            {
                ["k"] = "#2196F3",         // 蓝色
                ["d"] = "#FF9800"          // 橙色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 80, Color = "#EF5350", Label = "超买", IsDashed = true },
                new ReferenceLine { Value = 20, Color = "#26A69A", Label = "超卖", IsDashed = true }
            },
            OutputKeys = new() { "k", "d" }
        });
    }
    
    // ==================== 第二批：中优先级指标 ====================
    
    private static void RegisterCCI()
    {
        Register(new IndicatorMetadata
        {
            Id = "cci",
            Name = "CCI 商品通道指标",
            Category = "动量",
            Description = "衡量价格偏离平均值的程度。CCI>100超买，CCI<-100超卖",
            Type = SubChartIndicatorType.Line,
            MinValue = -200,
            MaxValue = 200,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 20,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "CCI计算周期，通常为20"
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#9C27B0"       // 紫色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 100, Color = "#EF5350", Label = "超买", IsDashed = true },
                new ReferenceLine { Value = 0, Color = "#666666", Label = "", IsDashed = false },
                new ReferenceLine { Value = -100, Color = "#26A69A", Label = "超卖", IsDashed = true }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterDMI()
    {
        Register(new IndicatorMetadata
        {
            Id = "dmi",
            Name = "DMI 趋向指标",
            Category = "趋势",
            Description = "包含ADX、+DI、-DI三条线。ADX>25表示强趋势，+DI>-DI表示上升趋势",
            Type = SubChartIndicatorType.MultiLine,
            MinValue = 0,
            MaxValue = 100,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "DMI计算周期，通常为14"
                }
            },
            DefaultColors = new()
            {
                ["adx"] = "#FFFFFF",       // 白色
                ["plus_di"] = "#26A69A",    // 绿色
                ["minus_di"] = "#EF5350"    // 红色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 25, Color = "#F0B90B", Label = "强趋势", IsDashed = true }
            },
            OutputKeys = new() { "adx", "plus_di", "minus_di" }
        });
    }
    
    private static void RegisterWR()
    {
        Register(new IndicatorMetadata
        {
            Id = "wr",
            Name = "WR 威廉指标",
            Category = "动量",
            Description = "范围-100到0。WR>-20超买区，WR<-80超卖区",
            Type = SubChartIndicatorType.Line,
            MinValue = -100,
            MaxValue = 0,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "WR计算周期，通常为14"
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#F0B90B"       // 金黄色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = -20, Color = "#EF5350", Label = "超买", IsDashed = true },
                new ReferenceLine { Value = -50, Color = "#666666", Label = "", IsDashed = true },
                new ReferenceLine { Value = -80, Color = "#26A69A", Label = "超卖", IsDashed = true }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterCMF()
    {
        Register(new IndicatorMetadata
        {
            Id = "cmf",
            Name = "CMF 蔡金资金流量",
            Category = "成交量",
            Description = "范围-1到1。CMF>0资金流入，CMF<0资金流出",
            Type = SubChartIndicatorType.Line,
            MinValue = -1,
            MaxValue = 1,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 20,
                    MinValue = 2,
                    MaxValue = 100,
                    Tooltip = "CMF计算周期，通常为20"
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#2196F3"       // 蓝色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 0, Color = "#666666", Label = "", IsDashed = false }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterROC()
    {
        Register(new IndicatorMetadata
        {
            Id = "roc",
            Name = "ROC 变动率指标",
            Category = "动量",
            Description = "价格变动率。ROC>0上涨，ROC<0下跌，数值大小表示变动幅度",
            Type = SubChartIndicatorType.Line,
            MinValue = double.NaN,
            MaxValue = double.NaN,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 12,
                    MinValue = 1,
                    MaxValue = 100,
                    Tooltip = "ROC计算周期，通常为12"
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#9C27B0"       // 紫色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 0, Color = "#666666", Label = "", IsDashed = false }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    // ==================== 第三批：低优先级指标 ====================
    
    private static void RegisterEMV()
    {
        Register(new IndicatorMetadata
        {
            Id = "emv",
            Name = "EMV 简易波动指标",
            Category = "成交量",
            Description = "衡量价格变动与成交量的关系。EMV>0上涨容易，EMV<0下跌容易",
            Type = SubChartIndicatorType.Line,
            MinValue = double.NaN,
            MaxValue = double.NaN,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 14,
                    MinValue = 1,
                    MaxValue = 100
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#2196F3"
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 0, Color = "#666666", Label = "", IsDashed = false }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterMTM()
    {
        Register(new IndicatorMetadata
        {
            Id = "mtm",
            Name = "MTM 动量指标",
            Category = "动量",
            Description = "价格动量变化。MTM>0多头市场，MTM<0空头市场",
            Type = SubChartIndicatorType.Line,
            MinValue = double.NaN,
            MaxValue = double.NaN,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 12,
                    MinValue = 1,
                    MaxValue = 100
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#F0B90B"
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 0, Color = "#666666", Label = "", IsDashed = false }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterCMO()
    {
        Register(new IndicatorMetadata
        {
            Id = "cmo",
            Name = "CMO Chande动量振荡器",
            Category = "动量",
            Description = "范围-100到100。CMO>50超买，CMO<-50超卖",
            Type = SubChartIndicatorType.Line,
            MinValue = -100,
            MaxValue = 100,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100
                }
            },
            DefaultColors = new()
            {
                ["line"] = "#9C27B0"
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 50, Color = "#EF5350", Label = "超买", IsDashed = true },
                new ReferenceLine { Value = 0, Color = "#666666", Label = "", IsDashed = false },
                new ReferenceLine { Value = -50, Color = "#26A69A", Label = "超卖", IsDashed = true }
            },
            OutputKeys = new() { "value" }
        });
    }
    
    private static void RegisterAroon()
    {
        Register(new IndicatorMetadata
        {
            Id = "aroon",
            Name = "Aroon 阿隆指标",
            Category = "趋势",
            Description = "由AroonUp和AroonDown组成。AroonUp>AroonDown上升趋势",
            Type = SubChartIndicatorType.MultiLine,
            MinValue = 0,
            MaxValue = 100,
            Parameters = new()
            {
                ["PERIOD"] = new ParameterDefinition
                {
                    Name = "PERIOD",
                    DisplayName = "周期",
                    DefaultValue = 25,
                    MinValue = 2,
                    MaxValue = 100
                }
            },
            DefaultColors = new()
            {
                ["aroon_up"] = "#26A69A",        // 绿色
                ["aroon_down"] = "#EF5350"       // 红色
            },
            ReferenceLines = new()
            {
                new ReferenceLine { Value = 70, Color = "#666666", Label = "", IsDashed = true },
                new ReferenceLine { Value = 30, Color = "#666666", Label = "", IsDashed = true }
            },
            OutputKeys = new() { "aroon_up", "aroon_down" }
        });
    }
    
    // ==================== 公共API ====================
    
    /// <summary>
    /// 注册单个指标
    /// </summary>
    public static void Register(IndicatorMetadata metadata)
    {
        _indicators[metadata.Id] = metadata;
    }
    
    /// <summary>
    /// 获取指标元数据
    /// </summary>
    public static IndicatorMetadata? GetMetadata(string id)
    {
        return _indicators.TryGetValue(id, out var metadata) ? metadata : null;
    }
    
    /// <summary>
    /// 获取所有指标
    /// </summary>
    public static IEnumerable<IndicatorMetadata> GetAllIndicators()
    {
        return _indicators.Values.OrderBy(m => m.Category).ThenBy(m => m.Name);
    }
    
    /// <summary>
    /// 按分类获取指标
    /// </summary>
    public static IEnumerable<IndicatorMetadata> GetIndicatorsByCategory(string category)
    {
        return _indicators.Values
            .Where(m => m.Category == category)
            .OrderBy(m => m.Name);
    }
    
    /// <summary>
    /// 获取所有分类
    /// </summary>
    public static IEnumerable<string> GetAllCategories()
    {
        return _indicators.Values
            .Select(m => m.Category)
            .Distinct()
            .OrderBy(c => c);
    }
    
    /// <summary>
    /// 检查指标是否存在
    /// </summary>
    public static bool Contains(string id)
    {
        return _indicators.ContainsKey(id);
    }
    
    /// <summary>
    /// 获取指标数量
    /// </summary>
    public static int Count => _indicators.Count;
    
    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static SubChartIndicatorConfig CreateDefaultConfig(string indicatorId)
    {
        var metadata = GetMetadata(indicatorId);
        if (metadata == null)
        {
            throw new ArgumentException($"未找到指标: {indicatorId}");
        }
        
        var config = new SubChartIndicatorConfig
        {
            Id = metadata.Id,
            Name = metadata.Name,
            IsEnabled = false,
            Type = metadata.Type,
            MinValue = metadata.MinValue,
            MaxValue = metadata.MaxValue,
            Parameters = new Dictionary<string, object>(),
            Colors = new Dictionary<string, string>(metadata.DefaultColors)
        };
        
        // 设置默认参数
        foreach (var param in metadata.Parameters.Values)
        {
            config.Parameters[param.Name] = param.DefaultValue;
        }
        
        return config;
    }
}


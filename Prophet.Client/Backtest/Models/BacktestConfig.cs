using System;
using System.Collections.Generic;

namespace Prophet.Client.Backtest.Models;

/// <summary>
/// 回测配置
/// </summary>
public class BacktestConfig
{
    // ========== 时间范围 ==========
    /// <summary>回测开始时间</summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>回测结束时间</summary>
    public DateTime EndDate { get; set; }
    
    // ========== 交易标的 ==========
    /// <summary>
    /// 交易标的（InstrumentKey / symbol_key）
    /// 例：BTCUSDT-BINANCE-SWAP / BTCUSDT-OKX-SWAP
    /// </summary>
    public string Symbol { get; set; } = "BTCUSDT-BINANCE-SWAP";
    
    /// <summary>时间框架</summary>
    public string Interval { get; set; } = "5m";
    
    // ========== 资金参数（基于币安合约标准）==========
    /// <summary>初始资金</summary>
    public decimal InitialCapital { get; set; } = 10000m;
    
    /// <summary>
    /// 杠杆倍数（币安合约标准）
    /// 默认10倍，影响保证金计算和强平风险
    /// 保证金 = 仓位价值 / 杠杆 + 手续费
    /// </summary>
    public decimal Leverage { get; set; } = 10m;  // 默认10倍杠杆
    
    /// <summary>
    /// 单次开仓使用的资金比例（0-1，基于初始余额的百分比）
    /// 默认5%，表示每次使用初始资金的5%作为保证金
    /// 实际开仓价值 = 保证金 × 杠杆倍数
    /// 例如：初始10000 USDT，5%仓位，10倍杠杆 → 500保证金，5000开仓价值
    /// </summary>
    public decimal PositionSizePercent { get; set; } = 0.05m;  // 默认5%仓位
    
    /// <summary>是否允许做空（合约模式）</summary>
    public bool AllowShort { get; set; } = true; // 默认现货模式，不支持做空
    
    // ========== P2.6: 动态仓位大小配置 ==========
    /// <summary>仓位大小计算方法: "fixed", "atr", "kelly"（存储为小写，UI显示为: Fixed, ATR, Kelly）</summary>
    public string PositionSizeMethod { get; set; } = "fixed";
    
    /// <summary>ATR周期（用于ATR模式）</summary>
    public int ATRPeriod { get; set; } = 14;
    
    /// <summary>ATR止损倍数（用于ATR模式）</summary>
    public decimal ATRMultiplier { get; set; } = 2m;
    
    /// <summary>每单位风险百分比（用于ATR模式）</summary>
    public decimal RiskPercentPerTrade { get; set; } = 0.01m;  // 每笔交易风险1%
    
    /// <summary>最大Kelly仓位比例（用于Kelly模式）</summary>
    public decimal MaxKellyFraction { get; set; } = 0.25m;  // 最大25%
    
    /// <summary>Kelly计算最小样本数（用于Kelly模式）</summary>
    public int MinKellySampleSize { get; set; } = 20;
    
    // ========== 费用参数（用户可配置）==========
    /// <summary>手续费率（Taker）</summary>
    public decimal TakerFeeRate { get; set; } = 0.001m;  // 0.1%
    
    /// <summary>挂单手续费率（Maker，未来支持）</summary>
    public decimal MakerFeeRate { get; set; } = 0.0005m;  // 0.05%
    
    /// <summary>滑点率</summary>
    public decimal SlippageRate { get; set; } = 0.0005m;  // 0.05%
    
    // ========== 止盈止损配置（基于保证金比例，符合币安标准）==========
    /// <summary>
    /// 默认止盈百分比（基于保证金，不是价格！）
    /// 计算公式：
    ///   多单止盈价 = 开仓价 + (开仓价 / 杠杆 × 止盈百分比)
    ///   空单止盈价 = 开仓价 - (开仓价 / 杠杆 × 止盈百分比)
    /// 
    /// 示例（10倍杠杆，开仓价50000，止盈40%）：
    ///   多单止盈价 = 50000 + (50000/10 × 0.4) = 52000
    ///   实际收益 = 保证金的40%
    /// </summary>
    public decimal DefaultTakeProfitPercent { get; set; } = 0.40m;  // 40%（保证金收益）
    
    /// <summary>
    /// 默认止损百分比（基于保证金，不是价格！）
    /// 计算公式：
    ///   多单止损价 = 开仓价 - (开仓价 / 杠杆 × 止损百分比)
    ///   空单止损价 = 开仓价 + (开仓价 / 杠杆 × 止损百分比)
    /// 
    /// 示例（10倍杠杆，开仓价50000，止损20%）：
    ///   多单止损价 = 50000 - (50000/10 × 0.2) = 49000
    ///   实际亏损 = 保证金的20%
    /// </summary>
    public decimal DefaultStopLossPercent { get; set; } = 0.20m;  // 20%（保证金亏损）
    
    /// <summary>是否启用默认止盈止损（当信号未提供TP/SL时）</summary>
    public bool EnableDefaultTPSL { get; set; } = true;
    
    // ========== P2.8: 移动止损/追踪止盈配置 ==========
    /// <summary>是否启用移动止损（当信号持续提供新的SL值时，动态更新止损价）</summary>
    public bool EnableTrailingStop { get; set; } = false;
    
    /// <summary>是否启用追踪止盈（当信号持续提供新的TP值时，动态更新止盈价）</summary>
    public bool EnableTrailingTakeProfit { get; set; } = false;
    
    // ========== 滑点模式配置（P1.3）==========
    /// <summary>滑点模式</summary>
    public SlippageMode SlippageMode { get; set; } = SlippageMode.FixedBps;
    
    /// <summary>固定基点滑点（当 SlippageMode = FixedBps 时）</summary>
    public decimal FixedBps { get; set; } = 0.0005m;  // 5 基点 = 0.05%
    
    /// <summary>固定价格滑点（当 SlippageMode = FixedPrice 时）</summary>
    public decimal FixedPrice { get; set; } = 0.5m;  // 0.5 USDT
    
    /// <summary>价差百分比滑点（当 SlippageMode = PctOfSpread 时）</summary>
    public decimal PctOfSpread { get; set; } = 0.10m;  // 价差的10%
    
    /// <summary>市场冲击系数（当 SlippageMode = MarketImpact 时）</summary>
    public decimal ImpactCoefficient { get; set; } = 0.1m;
    
    /// <summary>市场冲击指数（订单大小的影响程度）</summary>
    public decimal ImpactExponent { get; set; } = 0.5m;
    
    /// <summary>启用滑点随机波动</summary>
    public bool SlippageRandomness { get; set; } = true;
    
    /// <summary>滑点随机因子（±20%波动）</summary>
    public decimal SlippageRandomFactor { get; set; } = 0.2m;
    
    /// <summary>市价单滑点倍数</summary>
    public decimal MarketOrderMultiplier { get; set; } = 1.0m;
    
    /// <summary>止损单滑点倍数（通常更大）</summary>
    public decimal StopOrderMultiplier { get; set; } = 1.5m;
    
    // ========== 策略参数（JSON序列化）==========
    /// <summary>策略自定义参数</summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    // ========== 性能优化配置 ==========
    /// <summary>启用K线转换缓存</summary>
    public bool EnableKlineCache { get; set; } = true;
    
    /// <summary>缓存大小（保留最近N次转换结果）</summary>
    public int MaxCacheSize { get; set; } = 100;
    
    // ========== 回测模式配置 ==========
    /// <summary>
    /// 信号采样频率（滑动窗口步长）
    /// 例如: "1m", "5m", "10m"
    /// 表示每隔多久获取一次交易信号
    /// </summary>
    public string SignalSamplingInterval { get; set; } = "5m";
    
    /// <summary>
    /// 是否从API获取回测数据（推荐）
    /// true: 从Prophet.API一次性获取所有数据
    /// false: 使用本地KlineCache（需预加载）
    /// </summary>
    public bool UseApiDataSource { get; set; } = true;
    
    // ========== 内部使用（运行时生成）==========
    /// <summary>回测运行ID（自动生成）</summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString();
}


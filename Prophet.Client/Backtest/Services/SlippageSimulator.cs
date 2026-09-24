using System;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Services;

/// <summary>
/// 滑点模拟器 - 模拟市价单的滑点影响
/// P1.3: 支持多种滑点模式（移植自 Python tools/slippage_simulator.py）
/// </summary>
public class SlippageSimulator
{
    private readonly BacktestConfig _config;
    private readonly bool _enabled;
    private readonly Random _random = new Random();
    
    public SlippageSimulator(BacktestConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _enabled = true;  // 始终启用，由配置控制模式
    }
    
    // 兼容旧构造函数
    public SlippageSimulator(decimal slippageRate = 0.0001m, bool enabled = true)
    {
        _config = new BacktestConfig
        {
            SlippageMode = SlippageMode.FixedBps,
            SlippageRate = slippageRate,
            FixedBps = slippageRate
        };
        _enabled = enabled;
    }
    
    /// <summary>
    /// P1.3: 计算实际成交价格（应用滑点）- 支持多种模式
    /// </summary>
    /// <param name="side">订单方向</param>
    /// <param name="intendedPrice">预期价格</param>
    /// <param name="quantity">订单数量</param>
    /// <param name="candle">当前K线数据</param>
    /// <param name="isOpening">是否为开仓（true=开仓，false=平仓）</param>
    /// <returns>实际成交价格</returns>
    public decimal CalculateExecutionPrice(
        OrderSide side,
        decimal intendedPrice,
        decimal quantity,
        Candlestick candle,
        bool isOpening = true)
    {
        if (!_enabled)
            return intendedPrice;
        
        // 1. 计算基础滑点
        var baseSlippage = CalculateBaseSlippage(intendedPrice, quantity, candle);
        
        // 2. 应用随机性（如果启用）
        if (_config.SlippageRandomness)
        {
            var randomFactor = (decimal)(_random.NextDouble() * 2 - 1) * _config.SlippageRandomFactor;
            baseSlippage *= (1 + randomFactor);
        }
        
        // 3. 根据交易方向应用滑点
        decimal actualPrice;
        if (side == OrderSide.BUY)
        {
            // 买入：价格上滑（不利）
            actualPrice = intendedPrice + baseSlippage;
        }
        else
        {
            // 卖出：价格下滑（不利）
            actualPrice = intendedPrice - baseSlippage;
        }
        
        // 4. 确保价格在K线的合理范围内
        var high = (decimal)candle.High;
        var low = (decimal)candle.Low;
        
        if (side == OrderSide.BUY)
        {
            // 买入价格不超过最高价的1.1倍
            actualPrice = Math.Min(actualPrice, high * 1.1m);
        }
        else
        {
            // 卖出价格不低于最低价的0.9倍
            actualPrice = Math.Max(actualPrice, low * 0.9m);
        }
        
        return actualPrice;
    }
    
    /// <summary>
    /// P1.3: 根据配置的模式计算基础滑点
    /// </summary>
    private decimal CalculateBaseSlippage(decimal intendedPrice, decimal quantity, Candlestick candle)
    {
        return _config.SlippageMode switch
        {
            SlippageMode.FixedBps => CalculateFixedBpsSlippage(intendedPrice),
            SlippageMode.FixedPrice => _config.FixedPrice,
            SlippageMode.PctOfSpread => CalculatePctOfSpreadSlippage(candle),
            SlippageMode.MarketImpact => CalculateMarketImpactSlippage(intendedPrice, quantity),
            _ => CalculateFixedBpsSlippage(intendedPrice)
        };
    }
    
    /// <summary>
    /// 模式1: 固定基点滑点
    /// </summary>
    private decimal CalculateFixedBpsSlippage(decimal price)
    {
        return price * _config.FixedBps;
    }
    
    /// <summary>
    /// 模式2: 基于K线价差的百分比滑点
    /// </summary>
    private decimal CalculatePctOfSpreadSlippage(Candlestick candle)
    {
        var spread = (decimal)(candle.High - candle.Low);
        return spread * _config.PctOfSpread;
    }
    
    /// <summary>
    /// 模式3: 市场冲击模型
    /// 滑点与订单大小呈幂次关系
    /// </summary>
    private decimal CalculateMarketImpactSlippage(decimal price, decimal quantity)
    {
        var positionValue = price * quantity;
        var avgDailyVolume = 10000000m;  // 假设平均日交易量1000万USDT
        var volumeRatio = positionValue / avgDailyVolume;
        
        // 冲击 = 价格 * 冲击系数 * (订单占比 ^ 冲击指数)
        var impact = price * _config.ImpactCoefficient * 
            (decimal)Math.Pow((double)volumeRatio, (double)_config.ImpactExponent);
        
        return impact;
    }
    
    /// <summary>
    /// 获取滑点统计信息
    /// </summary>
    public SlippageStatistics GetStatistics(
        OrderSide side,
        decimal intendedPrice,
        decimal actualPrice,
        decimal quantity)
    {
        var slippageAmount = Math.Abs(actualPrice - intendedPrice);
        var slippageCost = slippageAmount * quantity;
        var slippageBps = (slippageAmount / intendedPrice) * 10000; // 基点
        
        var direction = side == OrderSide.BUY
            ? (actualPrice > intendedPrice ? "不利" : "有利")
            : (actualPrice < intendedPrice ? "不利" : "有利");
        
        return new SlippageStatistics
        {
            SlippageAmount = slippageAmount,
            SlippageCost = slippageCost,
            SlippageBps = slippageBps,
            Direction = direction
        };
    }
}

/// <summary>
/// 滑点统计信息
/// </summary>
public class SlippageStatistics
{
    /// <summary>滑点金额</summary>
    public decimal SlippageAmount { get; set; }
    
    /// <summary>滑点成本</summary>
    public decimal SlippageCost { get; set; }
    
    /// <summary>滑点基点（万分之）</summary>
    public decimal SlippageBps { get; set; }
    
    /// <summary>滑点方向（有利/不利）</summary>
    public string Direction { get; set; } = string.Empty;
}


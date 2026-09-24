using System;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Services;

/// <summary>
/// 保证金计算器（基于币安合约标准）
/// 参考：backtest.py 第1082-1096行
/// </summary>
public class MarginCalculator
{
    /// <summary>
    /// 计算开仓保证金
    /// 公式：margin = (quantity × price / leverage) + opening_fee
    /// </summary>
    /// <param name="quantity">开仓数量</param>
    /// <param name="price">开仓价格</param>
    /// <param name="leverage">杠杆倍数</param>
    /// <param name="feeRate">手续费率</param>
    /// <returns>所需保证金</returns>
    public static decimal CalculateMargin(
        decimal quantity,
        decimal price,
        decimal leverage,
        decimal feeRate)
    {
        if (leverage <= 0)
            throw new ArgumentException("杠杆倍数必须大于0", nameof(leverage));
        
        // 仓位价值
        var positionValue = quantity * price;
        
        // 保证金 = 仓位价值 / 杠杆
        var margin = positionValue / leverage;
        
        // 开仓手续费
        var openingFee = positionValue * feeRate;
        
        // 总保证金 = 保证金 + 开仓手续费
        return margin + openingFee;
    }
    
    /// <summary>
    /// 计算强平价（基于币安合约标准）
    /// 多单强平价 = 开仓价 - 保证金
    /// 空单强平价 = 开仓价 + 保证金
    /// </summary>
    /// <param name="entryPrice">开仓价格</param>
    /// <param name="side">订单方向</param>
    /// <param name="margin">保证金</param>
    /// <returns>强平价格</returns>
    public static decimal CalculateLiquidationPrice(
        decimal entryPrice,
        OrderSide side,
        decimal margin)
    {
        if (side == OrderSide.BUY)
        {
            // 多单强平价 = 开仓价 - 保证金
            return entryPrice - margin;
        }
        else
        {
            // 空单强平价 = 开仓价 + 保证金
            return entryPrice + margin;
        }
    }
    
    /// <summary>
    /// 计算止盈价格（基于保证金比例）
    /// 多单止盈 = 开仓价 + (开仓价 / 杠杆 × 止盈百分比)
    /// 空单止盈 = 开仓价 - (开仓价 / 杠杆 × 止盈百分比)
    /// </summary>
    /// <param name="entryPrice">开仓价格</param>
    /// <param name="side">订单方向</param>
    /// <param name="leverage">杠杆倍数</param>
    /// <param name="takeProfitPercent">止盈百分比（如0.4表示40%）</param>
    /// <returns>止盈价格</returns>
    public static decimal CalculateTakeProfit(
        decimal entryPrice,
        OrderSide side,
        decimal leverage,
        decimal takeProfitPercent)
    {
        if (leverage <= 0)
            throw new ArgumentException("杠杆倍数必须大于0", nameof(leverage));
        
        // 止盈价格变动 = 开仓价 / 杠杆 × 止盈百分比
        var priceChange = entryPrice / leverage * takeProfitPercent;
        
        if (side == OrderSide.BUY)
        {
            // 多单止盈 = 开仓价 + 价格变动
            return entryPrice + priceChange;
        }
        else
        {
            // 空单止盈 = 开仓价 - 价格变动
            return entryPrice - priceChange;
        }
    }
    
    /// <summary>
    /// 计算止损价格（基于保证金比例）
    /// 多单止损 = 开仓价 - (开仓价 / 杠杆 × 止损百分比)
    /// 空单止损 = 开仓价 + (开仓价 / 杠杆 × 止损百分比)
    /// </summary>
    /// <param name="entryPrice">开仓价格</param>
    /// <param name="side">订单方向</param>
    /// <param name="leverage">杠杆倍数</param>
    /// <param name="stopLossPercent">止损百分比（如0.2表示20%）</param>
    /// <returns>止损价格</returns>
    public static decimal CalculateStopLoss(
        decimal entryPrice,
        OrderSide side,
        decimal leverage,
        decimal stopLossPercent)
    {
        if (leverage <= 0)
            throw new ArgumentException("杠杆倍数必须大于0", nameof(leverage));
        
        // 止损价格变动 = 开仓价 / 杠杆 × 止损百分比
        var priceChange = entryPrice / leverage * stopLossPercent;
        
        if (side == OrderSide.BUY)
        {
            // 多单止损 = 开仓价 - 价格变动
            return entryPrice - priceChange;
        }
        else
        {
            // 空单止损 = 开仓价 + 价格变动
            return entryPrice + priceChange;
        }
    }
    
    /// <summary>
    /// 计算开仓数量（基于杠杆和仓位比例）
    /// </summary>
    /// <param name="availableCash">可用资金</param>
    /// <param name="positionSizePercent">仓位比例（0-1）</param>
    /// <param name="leverage">杠杆倍数</param>
    /// <param name="price">开仓价格</param>
    /// <param name="feeRate">手续费率</param>
    /// <param name="slippageRate">滑点率</param>
    /// <returns>开仓数量</returns>
    public static decimal CalculatePositionSize(
        decimal availableCash,
        decimal positionSizePercent,
        decimal leverage,
        decimal price,
        decimal feeRate,
        decimal slippageRate = 0)
    {
        if (leverage <= 0)
            throw new ArgumentException("杠杆倍数必须大于0", nameof(leverage));
        if (price <= 0)
            throw new ArgumentException("价格必须大于0", nameof(price));
        
        // 可用保证金 = 可用资金 × 仓位比例
        var availableMargin = availableCash * positionSizePercent;
        
        // 购买力 = 可用保证金 × 杠杆倍数
        var buyingPower = availableMargin * leverage;
        
        // 预留手续费和滑点空间
        var effectiveBuyingPower = buyingPower / (1 + feeRate + slippageRate);
        
        // 开仓数量 = 有效购买力 / 价格
        var quantity = effectiveBuyingPower / price;
        
        // 四舍五入到8位小数
        return Math.Round(quantity, 8);
    }
    
    /// <summary>
    /// 计算平仓后应归还的资金
    /// 归还金额 = 保证金 + 盈亏 - 平仓手续费
    /// </summary>
    /// <param name="margin">开仓时的保证金</param>
    /// <param name="profit">盈亏（已扣除开仓手续费和资金费用）</param>
    /// <param name="closingFee">平仓手续费</param>
    /// <returns>应归还的资金</returns>
    public static decimal CalculateReturnAmount(
        decimal margin,
        decimal profit,
        decimal closingFee)
    {
        // 归还金额 = 保证金 + 盈亏 - 平仓手续费
        return margin + profit - closingFee;
    }
    
    /// <summary>
    /// 计算净盈亏（扣除所有费用）
    /// </summary>
    /// <param name="entryPrice">开仓价格</param>
    /// <param name="exitPrice">平仓价格</param>
    /// <param name="quantity">数量</param>
    /// <param name="side">订单方向</param>
    /// <param name="openingFee">开仓手续费</param>
    /// <param name="closingFee">平仓手续费</param>
    /// <param name="fundingFee">资金费用</param>
    /// <returns>净盈亏</returns>
    public static decimal CalculateNetProfit(
        decimal entryPrice,
        decimal exitPrice,
        decimal quantity,
        OrderSide side,
        decimal openingFee,
        decimal closingFee,
        decimal fundingFee = 0)
    {
        decimal grossProfit;
        
        if (side == OrderSide.BUY)
        {
            // 多单盈亏 = (平仓价 - 开仓价) × 数量
            grossProfit = (exitPrice - entryPrice) * quantity;
        }
        else
        {
            // 空单盈亏 = (开仓价 - 平仓价) × 数量
            grossProfit = (entryPrice - exitPrice) * quantity;
        }
        
        // 净盈亏 = 毛利 - 开仓手续费 - 平仓手续费 - 资金费用
        return grossProfit - openingFee - closingFee - fundingFee;
    }
}


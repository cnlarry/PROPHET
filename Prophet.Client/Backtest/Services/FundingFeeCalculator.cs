using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Database;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Services;

/// <summary>
/// 资金费用计算器 - 基于真实的资金费率数据
/// 参考 backtest.py 的 _calculate_funding_fee 函数
/// </summary>
public class FundingFeeCalculator
{
    private static readonly int[] FundingHours = { 0, 8, 16 }; // UTC时间：00:00, 08:00, 16:00
    private readonly string? _databasePath;
    
    public FundingFeeCalculator(string? databasePath = null)
    {
        _databasePath = databasePath;
    }

    private static string NormalizeSymbolKey(string symbolOrKey)
    {
        if (string.IsNullOrWhiteSpace(symbolOrKey))
        {
            return "BTCUSDT-BINANCE-SWAP";
        }

        if (InstrumentKey.TryParse(symbolOrKey, out var key))
        {
            return key.ToString().ToUpperInvariant();
        }

        return $"{symbolOrKey.ToUpperInvariant()}-BINANCE-SWAP";
    }
    
    /// <summary>
    /// 计算真实的资金费用
    /// </summary>
    /// <param name="symbol">交易对符号（如 BTCUSDT）</param>
    /// <param name="openTime">开仓时间</param>
    /// <param name="closeTime">平仓时间</param>
    /// <param name="positionValue">仓位价值（数量 * 开仓价格）</param>
    /// <param name="side">持仓方向（Buy 或 Sell）</param>
    /// <returns>资金费用（正数表示支付，负数表示收取）</returns>
    public decimal CalculateFundingFee(
        string symbolOrKey,
        DateTime openTime,
        DateTime closeTime,
        decimal positionValue,
        OrderSide side)
    {
        try
        {
            decimal totalFundingFee = 0m;
            
            // 转换为UTC时间
            var openUtc = openTime.ToUniversalTime();
            var closeUtc = closeTime.ToUniversalTime();
            
            // 遍历每个资金费收取时间点
            var currentTime = openUtc;
            
            while (currentTime < closeUtc)
            {
                // 找到下一个资金费收取时间点
                var nextFundingTime = GetNextFundingTime(currentTime);
                
                // 如果下一个资金费时间超过了平仓时间，跳出循环
                if (nextFundingTime > closeUtc)
                    break;
                
                // 查询该时间点的资金费率
                var fundingRate = GetFundingRate(symbolOrKey, nextFundingTime);
                
                if (fundingRate.HasValue)
                {
                    // 计算资金费
                    // 多单：支付正费率，收取负费率
                    // 空单：收取正费率，支付负费率
                    decimal fee = side == OrderSide.BUY
                        ? positionValue * fundingRate.Value
                        : positionValue * (-fundingRate.Value);
                    
                    totalFundingFee += fee;
                    
                    // Console.WriteLine($"   💰 {nextFundingTime:yyyy-MM-dd HH:mm} {side} " + $"{(fee > 0 ? "支付" : "收取")}资金费: " + $"{Math.Abs(fee):F4} (费率: {fundingRate.Value:F6})");
                }
                
                // 移动到下一个资金费时间点之后
                currentTime = nextFundingTime.AddMinutes(1);
            }
            
            return totalFundingFee;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 计算资金费失败: {ex.Message}，使用估算值");
            // 如果失败，返回基于时间的估算值
            var hours = (closeTime - openTime).TotalHours;
            var periods = (decimal)(hours / 8); // 8小时一个周期
            return positionValue * 0.0001m * periods; // 默认费率 0.01%
        }
    }
    
    /// <summary>
    /// 获取下一个资金费收取时间
    /// </summary>
    private DateTime GetNextFundingTime(DateTime currentTime)
    {
        var currentDate = currentTime.Date;
        
        // 在当前日期查找下一个收取时间点
        foreach (var hour in FundingHours)
        {
            var potentialTime = currentDate.AddHours(hour);
            if (potentialTime > currentTime)
                return potentialTime;
        }
        
        // 如果当天没有找到，移到第二天的第一个时间点
        return currentDate.AddDays(1).AddHours(FundingHours[0]);
    }
    
    /// <summary>
    /// 查询指定时间点的资金费率
    /// </summary>
    private decimal? GetFundingRate(string symbol, DateTime fundingTime)
    {
        try
        {
            var symbolKey = NormalizeSymbolKey(symbol);
            using var connection = string.IsNullOrWhiteSpace(_databasePath)
                ? DBHelper.CreateConnection()
                : DBHelper.CreateConnection(_databasePath);
            
            // 转换为毫秒时间戳
            var fundingTimeMs = new DateTimeOffset(fundingTime.ToUniversalTime()).ToUnixTimeMilliseconds();
            
            // 查询最近的资金费率
            var sql = @"
                SELECT last_funding_rate 
                FROM fundingrate 
                WHERE symbol_key = @SymbolKey 
                  AND calc_time <= @CalcTime 
                ORDER BY calc_time DESC 
                LIMIT 1";
            
            var rate = connection.QueryFirstOrDefault<decimal?>(
                sql,
                new { SymbolKey = symbolKey, CalcTime = fundingTimeMs });
            
            return rate;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 查询资金费率失败: {ex.Message}");
            return null;
        }
    }
}


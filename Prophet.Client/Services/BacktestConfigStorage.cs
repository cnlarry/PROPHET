using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dapper;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;
using Prophet.Client.Core;
using Prophet.Client.Database;

namespace Prophet.Client.Services;

/// <summary>
/// 回测配置存储服务 - 负责保存和加载回测配置模板
/// </summary>
public static class BacktestConfigStorage
{
    /// <summary>
    /// 保存回测配置（只保留一条默认记录）
    /// </summary>
    public static bool SaveConfig(BacktestConfig config)
    {
        try
        {
            // ✅ 表初始化已迁移到 ClientMigrations (2025-12-31_02_backtest_symbol_to_symbol_key)
            // 不再需要 EnsureTableExists()
            
            using var connection = DBHelper.CreateConnection();

            var parametersJson = config.Parameters.Count > 0 
                ? JsonSerializer.Serialize(config.Parameters) 
                : null;

            var sql = @"
                INSERT OR REPLACE INTO backtest_configs (
                    id, symbol_key, interval, start_date, end_date,
                    initial_capital, leverage, position_size_percent, allow_short,
                    position_size_method, atr_period, atr_multiplier, risk_percent_per_trade,
                    max_kelly_fraction, min_kelly_sample_size,
                    taker_fee_rate, maker_fee_rate, slippage_rate,
                    default_take_profit_percent, default_stop_loss_percent,
                    enable_default_tpsl, enable_trailing_stop, enable_trailing_take_profit,
                    slippage_mode, fixed_bps, fixed_price, pct_of_spread,
                    impact_coefficient, impact_exponent, slippage_randomness,
                    slippage_random_factor, market_order_multiplier, stop_order_multiplier,
                    parameters_json, enable_kline_cache, max_cache_size,
                    signal_sampling_interval, use_api_data_source,
                    updated_at
                ) VALUES (
                    1, @SymbolKey, @Interval, @StartDate, @EndDate,
                    @InitialCapital, @Leverage, @PositionSizePercent, @AllowShort,
                    @PositionSizeMethod, @ATRPeriod, @ATRMultiplier, @RiskPercentPerTrade,
                    @MaxKellyFraction, @MinKellySampleSize,
                    @TakerFeeRate, @MakerFeeRate, @SlippageRate,
                    @DefaultTakeProfitPercent, @DefaultStopLossPercent,
                    @EnableDefaultTPSL, @EnableTrailingStop, @EnableTrailingTakeProfit,
                    @SlippageMode, @FixedBps, @FixedPrice, @PctOfSpread,
                    @ImpactCoefficient, @ImpactExponent, @SlippageRandomness,
                    @SlippageRandomFactor, @MarketOrderMultiplier, @StopOrderMultiplier,
                    @ParametersJson, @EnableKlineCache, @MaxCacheSize,
                    @SignalSamplingInterval, @UseApiDataSource,
                    datetime('now', 'localtime')
                )
            ";

            // 🔧 v4.0: 确保保存UTC时间字符串，不进行时区转换
            // 如果config中的时间是UTC，直接使用；否则转换为UTC
            var startDateUtc = config.StartDate.Kind == DateTimeKind.Utc 
                ? config.StartDate 
                : config.StartDate.ToUniversalTime();
            var endDateUtc = config.EndDate.Kind == DateTimeKind.Utc 
                ? config.EndDate 
                : config.EndDate.ToUniversalTime();
            
            connection.Execute(sql, new
            {
                SymbolKey = config.Symbol,
                Interval = config.Interval,
                StartDate = startDateUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate = endDateUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                InitialCapital = config.InitialCapital,
                Leverage = config.Leverage,
                PositionSizePercent = config.PositionSizePercent,
                AllowShort = config.AllowShort ? 1 : 0,
                PositionSizeMethod = config.PositionSizeMethod,
                ATRPeriod = config.ATRPeriod,
                ATRMultiplier = config.ATRMultiplier,
                RiskPercentPerTrade = config.RiskPercentPerTrade,
                MaxKellyFraction = config.MaxKellyFraction,
                MinKellySampleSize = config.MinKellySampleSize,
                TakerFeeRate = config.TakerFeeRate,
                MakerFeeRate = config.MakerFeeRate,
                SlippageRate = config.SlippageRate,
                DefaultTakeProfitPercent = config.DefaultTakeProfitPercent,
                DefaultStopLossPercent = config.DefaultStopLossPercent,
                EnableDefaultTPSL = config.EnableDefaultTPSL ? 1 : 0,
                EnableTrailingStop = config.EnableTrailingStop ? 1 : 0,
                EnableTrailingTakeProfit = config.EnableTrailingTakeProfit ? 1 : 0,
                SlippageMode = config.SlippageMode.ToString(),
                FixedBps = config.FixedBps,
                FixedPrice = config.FixedPrice,
                PctOfSpread = config.PctOfSpread,
                ImpactCoefficient = config.ImpactCoefficient,
                ImpactExponent = config.ImpactExponent,
                SlippageRandomness = config.SlippageRandomness ? 1 : 0,
                SlippageRandomFactor = config.SlippageRandomFactor,
                MarketOrderMultiplier = config.MarketOrderMultiplier,
                StopOrderMultiplier = config.StopOrderMultiplier,
                ParametersJson = parametersJson,
                EnableKlineCache = config.EnableKlineCache ? 1 : 0,
                MaxCacheSize = config.MaxCacheSize,
                SignalSamplingInterval = config.SignalSamplingInterval,
                UseApiDataSource = config.UseApiDataSource ? 1 : 0
            });

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BacktestConfigStorage] 保存配置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 加载回测配置（加载唯一的一条记录）
    /// </summary>
    public static BacktestConfig? LoadConfig()
    {
        try
        {
            // ✅ 表初始化已迁移到 ClientMigrations
            
            using var connection = DBHelper.CreateConnection();

            var sql = "SELECT * FROM backtest_configs WHERE id = 1";
            var row = connection.QueryFirstOrDefault<dynamic>(sql);

            if (row == null)
            {
                return null;
            }

            // 🔧 v4.0: 确保加载时解析为UTC时间，不进行时区转换
            // 数据库中的时间字符串是UTC时间（格式：yyyy-MM-dd HH:mm:ss），需要明确指定为UTC
            var startDateStr = row.start_date?.ToString() ?? "";
            var endDateStr = row.end_date?.ToString() ?? "";
            
            // 解析字符串为DateTime，然后指定为UTC时间
            var startDateParsed = DateTime.Parse(startDateStr);
            var endDateParsed = DateTime.Parse(endDateStr);
            
            // 明确指定为UTC时间，避免时区转换
            var startDateUtc = startDateParsed.Kind == DateTimeKind.Utc 
                ? startDateParsed 
                : DateTime.SpecifyKind(startDateParsed, DateTimeKind.Utc);
            var endDateUtc = endDateParsed.Kind == DateTimeKind.Utc 
                ? endDateParsed 
                : DateTime.SpecifyKind(endDateParsed, DateTimeKind.Utc);
            
            var config = new BacktestConfig
            {
                // 语义升级：Symbol 存储 InstrumentKey（symbol_key）
                Symbol = row.symbol_key ?? "BTCUSDT-BINANCE-SWAP",
                Interval = row.interval ?? "5m",
                StartDate = startDateUtc,
                EndDate = endDateUtc,
                InitialCapital = (decimal)row.initial_capital,
                Leverage = (decimal)row.leverage,
                PositionSizePercent = (decimal)row.position_size_percent,
                AllowShort = row.allow_short == 1,
                // 确保读取时统一为小写，保持一致性
                PositionSizeMethod = (row.position_size_method ?? "fixed").ToLower(),
                ATRPeriod = (int)row.atr_period,
                ATRMultiplier = (decimal)row.atr_multiplier,
                RiskPercentPerTrade = (decimal)row.risk_percent_per_trade,
                MaxKellyFraction = (decimal)row.max_kelly_fraction,
                MinKellySampleSize = (int)row.min_kelly_sample_size,
                TakerFeeRate = (decimal)row.taker_fee_rate,
                MakerFeeRate = (decimal)row.maker_fee_rate,
                SlippageRate = (decimal)row.slippage_rate,
                DefaultTakeProfitPercent = (decimal)row.default_take_profit_percent,
                DefaultStopLossPercent = (decimal)row.default_stop_loss_percent,
                EnableDefaultTPSL = row.enable_default_tpsl == 1,
                EnableTrailingStop = row.enable_trailing_stop == 1,
                EnableTrailingTakeProfit = row.enable_trailing_take_profit == 1,
                SlippageMode = Enum.Parse<SlippageMode>(row.slippage_mode ?? "FixedBps", ignoreCase: true),
                FixedBps = (decimal)row.fixed_bps,
                FixedPrice = (decimal)row.fixed_price,
                PctOfSpread = (decimal)row.pct_of_spread,
                ImpactCoefficient = (decimal)row.impact_coefficient,
                ImpactExponent = (decimal)row.impact_exponent,
                SlippageRandomness = row.slippage_randomness == 1,
                SlippageRandomFactor = (decimal)row.slippage_random_factor,
                MarketOrderMultiplier = (decimal)row.market_order_multiplier,
                StopOrderMultiplier = (decimal)row.stop_order_multiplier,
                EnableKlineCache = row.enable_kline_cache == 1,
                MaxCacheSize = (int)row.max_cache_size,
                SignalSamplingInterval = row.signal_sampling_interval ?? "5m",
                UseApiDataSource = row.use_api_data_source == 1
            };

            // 解析Parameters JSON
            if (!string.IsNullOrEmpty(row.parameters_json))
            {
                try
                {
                    config.Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(row.parameters_json) 
                        ?? new Dictionary<string, object>();
                }
                catch
                {
                    config.Parameters = new Dictionary<string, object>();
                }
            }

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BacktestConfigStorage] 加载配置失败: {ex.Message}");
            return null;
        }
    }
}

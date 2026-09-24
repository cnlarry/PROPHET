using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;

namespace Prophet.Client.Database;

/// <summary>
/// 示例数据初始化器
/// 用于首次启动时创建示例策略和数据
/// </summary>
public static class SampleDataInitializer
{
    /// <summary>
    /// 初始化示例数据
    /// </summary>
    public static async Task InitializeAsync()
    {
        try
        {
            // 检查是否已初始化
            var isInitialized = await IsInitializedAsync();
            if (isInitialized)
            {
                Console.WriteLine("✅ 示例数据已存在，跳过初始化");
                return;
            }

            Console.WriteLine("🔧 首次启动，正在初始化示例数据...");

            // 创建示例策略
            await CreateSampleStrategiesAsync();

            // 标记为已初始化
            await MarkAsInitializedAsync();

            Console.WriteLine("✅ 示例数据初始化完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 示例数据初始化失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 检查是否已初始化
    /// </summary>
    private static async Task<bool> IsInitializedAsync()
    {
        var value = await DBHelper.QueryFirstOrDefaultAsync<string>(
            "SELECT value FROM app_settings WHERE key = 'sample_data_initialized'");
        
        return value == "true";
    }

    /// <summary>
    /// 标记为已初始化
    /// </summary>
    private static async Task MarkAsInitializedAsync()
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await DBHelper.ExecuteAsync(@"
            INSERT OR REPLACE INTO app_settings (key, value, description, updated_at)
            VALUES ('sample_data_initialized', 'true', '示例数据已初始化', @now)",
            new { now });
    }

    /// <summary>
    /// 创建示例策略
    /// </summary>
    private static async Task CreateSampleStrategiesAsync()
    {
        var repo = new StrategyRepository();

        var sampleStrategies = new[]
        {
            new StrategyInfo
            {
                Name = "双均线交叉策略",
                Dsl = @"// 双均线交叉策略
// 快线上穿慢线时买入，下穿时卖出

// 定义均线参数
FAST_PERIOD = 10
SLOW_PERIOD = 30

// 计算均线
ma_fast = MA(CLOSE, FAST_PERIOD)
ma_slow = MA(CLOSE, SLOW_PERIOD)

// 交易信号
when ma_fast cross_above ma_slow:
    BUY()

when ma_fast cross_below ma_slow:
    SELL()",
                Status = "draft",
                Description = "经典的双均线交叉策略，适合趋势行情",
                StrategyType = "趋势跟踪",
                RiskLevel = "medium"
            },

            new StrategyInfo
            {
                Name = "RSI超买超卖策略",
                Dsl = @"// RSI 超买超卖策略
// RSI < 30 买入，RSI > 70 卖出

// 计算RSI
rsi = RSI(CLOSE, 14)

// 交易信号
when rsi < 30:
    BUY()

when rsi > 70:
    SELL()",
                Status = "draft",
                Description = "基于RSI指标的超买超卖策略",
                StrategyType = "震荡交易",
                RiskLevel = "medium-low"
            },

            new StrategyInfo
            {
                Name = "布林带突破策略",
                Dsl = @"// 布林带突破策略
// 价格突破上轨买入，跌破下轨卖出

// 计算布林带
bb = BOLL(CLOSE, 20, 2)

// 交易信号
when CLOSE cross_above bb.upper:
    BUY()

when CLOSE cross_below bb.lower:
    SELL()",
                Status = "draft",
                Description = "基于布林带的突破策略",
                StrategyType = "突破",
                RiskLevel = "medium"
            },

            new StrategyInfo
            {
                Name = "MACD金叉死叉策略",
                Dsl = @"// MACD 金叉死叉策略
// DIF上穿DEA为金叉买入，下穿为死叉卖出

// 计算MACD
macd = MACD(CLOSE, 12, 26, 9)

// 交易信号
when macd.dif cross_above macd.dea:
    BUY()

when macd.dif cross_below macd.dea:
    SELL()",
                Status = "draft",
                Description = "经典的MACD指标策略",
                StrategyType = "趋势跟踪",
                RiskLevel = "medium"
            },

            new StrategyInfo
            {
                Name = "多指标综合策略",
                Dsl = @"// 多指标综合策略
// 结合均线、RSI和MACD的信号

// 计算指标
ma_fast = MA(CLOSE, 10)
ma_slow = MA(CLOSE, 30)
rsi = RSI(CLOSE, 14)
macd = MACD(CLOSE, 12, 26, 9)

// 多条件买入信号
when ma_fast > ma_slow and rsi < 40 and macd.dif > macd.dea:
    BUY()

// 多条件卖出信号
when ma_fast < ma_slow or rsi > 70 or macd.dif < macd.dea:
    SELL()",
                Status = "draft",
                Description = "综合多个技术指标的策略，提高信号可靠性",
                StrategyType = "综合",
                RiskLevel = "medium-high"
            }
        };

        Console.WriteLine($"📝 创建 {sampleStrategies.Length} 个示例策略...");

        foreach (var strategy in sampleStrategies)
        {
            var id = await repo.CreateStrategyAsync(strategy);
            Console.WriteLine($"   ✅ {strategy.Name} (ID: {id})");
        }

        Console.WriteLine($"✅ 成功创建 {sampleStrategies.Length} 个示例策略");
    }

    /// <summary>
    /// 清除所有示例数据（用于重置）
    /// </summary>
    public static async Task ClearAllDataAsync()
    {
        Console.WriteLine("🗑️ 清除所有数据...");

        await DBHelper.TruncateTableAsync("strategies");
        await DBHelper.TruncateTableAsync("strategy_versions");
        await DBHelper.TruncateTableAsync("klines");
        await DBHelper.TruncateTableAsync("fear_greed_index");
        await DBHelper.TruncateTableAsync("fundingrate");

        // 重置初始化标记
        await DBHelper.ExecuteAsync(
            "DELETE FROM app_settings WHERE key = 'sample_data_initialized'");

        Console.WriteLine("✅ 所有数据已清除");
    }

    /// <summary>
    /// 重新初始化（清除后重新创建）
    /// </summary>
    public static async Task ReinitializeAsync()
    {
        await ClearAllDataAsync();
        await InitializeAsync();
    }
}

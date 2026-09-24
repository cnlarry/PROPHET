using System;
using System.Collections.Generic;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 市场数据采集配置
/// 定义各种市场数据的采集频率、缓存策略等
/// </summary>
public static class MarketDataCollectionConfig
{
    /// <summary>
    /// 数据采集配置项
    /// </summary>
    public class DataCollectionConfig
    {
        /// <summary>
        /// 数据名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数据来源（Binance/Alternative.me等）
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 采集间隔（秒）
        /// </summary>
        public int CollectionIntervalSeconds { get; set; }

        /// <summary>
        /// 缓存时间（秒）
        /// </summary>
        public int CacheDurationSeconds { get; set; }

        /// <summary>
        /// 是否需要持久化到数据库
        /// </summary>
        public bool PersistToDatabase { get; set; }

        /// <summary>
        /// 是否需要定时采集（true）或按需采集（false）
        /// </summary>
        public bool ScheduledCollection { get; set; }

        /// <summary>
        /// 特殊采集时间（如UTC 00:05）
        /// </summary>
        public string? SpecialSchedule { get; set; }
    }

    /// <summary>
    /// 所有市场数据的采集配置
    /// </summary>
    public static readonly Dictionary<string, DataCollectionConfig> Configs = new()
    {
        // 1. 恐惧与贪婪指数
        {
            "FearGreed",
            new DataCollectionConfig
            {
                Name = "恐惧与贪婪指数",
                Source = "Alternative.me",
                CollectionIntervalSeconds = 86400,  // 24小时（实际由FearGreedCollector在UTC 00:05采集）
                CacheDurationSeconds = 86400,       // 缓存24小时
                PersistToDatabase = true,
                ScheduledCollection = true,
                SpecialSchedule = "UTC 00:05"       // 每天UTC 00:05采集
            }
        },

        // 2. 资金费率
        {
            "FundingRate",
            new DataCollectionConfig
            {
                Name = "资金费率",
                Source = "Binance",
                CollectionIntervalSeconds = 28800,  // 8小时（币安每8小时更新一次：00:00, 08:00, 16:00 UTC）
                CacheDurationSeconds = 28800,        // 缓存8小时
                PersistToDatabase = true,
                ScheduledCollection = true
            }
        },

        // 3. 多空比
        {
            "LongShortRatio",
            new DataCollectionConfig
            {
                Name = "多空比",
                Source = "Binance",
                CollectionIntervalSeconds = 300,     // 5分钟
                CacheDurationSeconds = 300,          // 缓存5分钟
                PersistToDatabase = true,
                ScheduledCollection = true
            }
        },

        // 4. 持仓量（Open Interest）
        {
            "OpenInterest",
            new DataCollectionConfig
            {
                Name = "持仓量",
                Source = "Binance",
                CollectionIntervalSeconds = 60,       // 1分钟
                CacheDurationSeconds = 60,            // 缓存1分钟
                PersistToDatabase = true,
                ScheduledCollection = true
            }
        },

        // 5. 24小时Ticker数据
        {
            "Ticker24h",
            new DataCollectionConfig
            {
                Name = "24小时Ticker",
                Source = "Binance",
                CollectionIntervalSeconds = 30,       // 30秒
                CacheDurationSeconds = 30,            // 缓存30秒
                PersistToDatabase = false,            // 不需要持久化（数据变化太快）
                ScheduledCollection = true
            }
        },

        // 6. 爆仓数据
        {
            "Liquidation",
            new DataCollectionConfig
            {
                Name = "爆仓数据",
                Source = "Binance",
                CollectionIntervalSeconds = 60,       // 1分钟
                CacheDurationSeconds = 60,            // 缓存1分钟
                PersistToDatabase = true,
                ScheduledCollection = true
            }
        },

        // 7. 价格数据（用于主流币种价格显示）
        {
            "Price",
            new DataCollectionConfig
            {
                Name = "价格数据",
                Source = "Binance",
                CollectionIntervalSeconds = 10,       // 10秒
                CacheDurationSeconds = 10,            // 缓存10秒
                PersistToDatabase = false,            // 不需要持久化
                ScheduledCollection = false           // 按需采集（通过Ticker24h获取）
            }
        }
    };

    /// <summary>
    /// 获取指定数据类型的配置
    /// </summary>
    public static DataCollectionConfig? GetConfig(string dataType)
    {
        return Configs.TryGetValue(dataType, out var config) ? config : null;
    }

    /// <summary>
    /// 数据采集优先级（数字越小优先级越高）
    /// </summary>
    public static readonly Dictionary<string, int> CollectionPriority = new()
    {
        { "Ticker24h", 1 },      // 最高优先级（最常用）
        { "Price", 1 },
        { "OpenInterest", 2 },
        { "Liquidation", 2 },
        { "LongShortRatio", 3 },
        { "FundingRate", 4 },
        { "FearGreed", 5 }       // 最低优先级（更新频率最低）
    };

    /// <summary>
    /// HTTP请求频率限制配置
    /// </summary>
    public static class RequestLimits
    {
        /// <summary>
        /// 币安API限制：每个IP每分钟最多1200次请求
        /// </summary>
        public const int BinanceMaxRequestsPerMinute = 1200;

        /// <summary>
        /// 建议的请求间隔（毫秒）
        /// </summary>
        public const int RecommendedRequestIntervalMs = 50; // 50ms = 每秒最多20次请求

        /// <summary>
        /// Alternative.me API限制：每分钟最多60次请求
        /// </summary>
        public const int AlternativeMaxRequestsPerMinute = 60;
    }

    /// <summary>
    /// 数据采集策略说明
    /// </summary>
    public static class CollectionStrategy
    {
        /// <summary>
        /// 智能采集策略：
        /// 1. 先检查数据库是否有最新数据
        /// 2. 如果数据过期，才发起API请求
        /// 3. 对于更新频率低的数据（如恐惧与贪婪指数），优先使用数据库缓存
        /// 4. 对于更新频率高的数据（如Ticker24h），使用内存缓存+API
        /// </summary>
        public const string Description = @"
数据采集策略：
1. 恐惧与贪婪指数：每天UTC 00:05采集一次，缓存24小时
2. 资金费率：每8小时采集一次，缓存8小时
3. 多空比：每5分钟采集一次，缓存5分钟
4. 持仓量：每1分钟采集一次，缓存1分钟
5. 24h Ticker：每30秒采集一次，缓存30秒（不持久化）
6. 爆仓数据：每1分钟采集一次，缓存1分钟
7. 价格数据：通过Ticker24h获取，按需采集

HTTP请求优化：
- 使用请求队列，避免并发请求过多
- 对于相同数据类型的请求，合并处理
- 优先使用数据库缓存，减少API调用
- 实现请求重试机制，处理网络异常
";
    }
}


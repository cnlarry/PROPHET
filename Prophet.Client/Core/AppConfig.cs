using System;
using System.IO;

namespace Prophet.Client.Core;

/// <summary>
/// 应用程序系统级配置（只包含系统级配置，用户可配置项已迁移到 AppSettings）
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// 数据库配置
    /// </summary>
    public static class Database
    {
        /// <summary>
        /// 是否为离线模式（完全本地化）
        /// </summary>
        public const bool IsOfflineMode = true;
        
        /// <summary>
        /// 本地数据库路径（主数据库：策略、版本、市场数据、回测结果）
        /// 开发阶段：位于 Prophet.Client 项目根目录
        /// 发布阶段：位于 %AppData%/Prophet/
        /// </summary>
        public static string LocalDatabasePath
        {
            get
            {
                if (_cachedDatabasePath == null)
                {
                    _cachedDatabasePath = GetDatabasePath();
                    Console.WriteLine($"📁 [AppConfig] 数据库路径: {_cachedDatabasePath}");
                }
                return _cachedDatabasePath;
            }
        }
        
        /// <summary>
        /// 回测数据库路径（与主数据库相同，统一使用 PROPHET.db）
        /// </summary>
        public static string BacktestDatabasePath => LocalDatabasePath;
        
        private static string? _cachedDatabasePath;
        
        /// <summary>
        /// 获取项目根目录下的数据库路径
        /// </summary>
        private static string GetDatabasePath()
        {
            const string dbFileName = "PROPHET.db";

#if DEBUG
            // 开发阶段：定位到 Prophet.Client 项目根目录（从 bin/Debug/net8.0 向上回溯）
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            baseDir = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.FullName;
            if (projectRoot == null)
            {
                throw new DirectoryNotFoundException($"无法找到 Prophet.Client 项目根目录，当前路径: {baseDir}");
            }

            return Path.Combine(projectRoot, dbFileName);
#else
            // 发布阶段：统一放到 %AppData%/Prophet/
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                // 极端情况下回退到当前目录
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbFileName);
            }

            var prophetDir = Path.Combine(appData, "Prophet");
            Directory.CreateDirectory(prophetDir);
            return Path.Combine(prophetDir, dbFileName);
#endif
        }
        
        /// <summary>
        /// 数据库架构文件路径
        /// </summary>
        public static string SchemaFilePath => 
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "schema.sql");
        
        /// <summary>
        /// 是否自动初始化数据库（设为false，由用户手动维护）
        /// </summary>
        public const bool AutoInitialize = false;
        
        /// <summary>
        /// 是否自动创建示例数据（设为false，数据库由用户手动准备）
        /// </summary>
        public const bool AutoCreateSampleData = false;
    }
    
    /// <summary>
    /// 交易系统配置（只包含系统级配置）
    /// </summary>
    public static class Trading
    {
        /// <summary>
        /// 每次加载更多数据的数量
        /// </summary>
        public const int LoadMoreCount = 1000;
        
        /// <summary>
        /// 要采集数据的交易对列表（USDT-M期货合约）
        /// 可以添加多个交易对，每个交易对都会独立采集数据
        /// </summary>
        public static readonly string[] Symbols = new[]
        {
            "BTCUSDT",   // 比特币
            "ETHUSDT",   // 以太坊
            "SOLUSDT",   // Solana
            "BNBUSDT",   // 币安币
            "ADAUSDT",   // Cardano
            "DOGEUSDT",  // Dogecoin
            "SUIUSDT"    // Sui
        };

        /// <summary>
        /// 币安API配置（系统级配置）
        /// </summary>
        public static class BinanceApi
        {
            public const string RestBaseUrl = "https://fapi.binance.com/fapi/v1";
            public const string WebSocketBaseUrl = "wss://fstream.binance.com/ws";
            public const int RestTimeoutSeconds = 30; // 增加到30秒，避免网络慢时超时
        }
    }
}


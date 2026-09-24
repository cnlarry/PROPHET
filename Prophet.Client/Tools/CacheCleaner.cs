using System;
using System.IO;
using System.Threading.Tasks;
using Prophet.Client.Services.Data;

namespace Prophet.Client.Tools;

/// <summary>
/// 缓存清理工具
/// 
/// 用途：当数据库直接更新后，清理客户端的本地缓存
/// 使用场景：
/// - 数据库中直接插入新数据后
/// - 需要强制刷新数据时
/// - 缓存数据有问题时
/// </summary>
public static class CacheCleaner
{
    /// <summary>
    /// 清理指定交易对和时间框架的缓存
    /// </summary>
    public static async Task ClearCacheAsync(string symbol, string interval)
    {
        try
        {
            var cache = new KlineCache();
            await cache.ClearCacheAsync(symbol, interval);
            Console.WriteLine($"✅ 已清除缓存: {symbol} {interval}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 清除缓存失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 清理所有缓存
    /// </summary>
    public static async Task ClearAllCacheAsync()
    {
        try
        {
            var cache = new KlineCache();
            
            // 清理常用的交易对和时间框架
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT" };
            var intervals = new[] { "1m", "3m", "5m", "15m", "30m", "1h", "2h", "4h", "6h", "8h", "12h", "1d" };
            
            foreach (var symbol in symbols)
            {
                foreach (var interval in intervals)
                {
                    await cache.ClearCacheAsync(symbol, interval);
                }
            }
            
            Console.WriteLine($"✅ 已清除所有缓存");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 清除缓存失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 删除整个缓存数据库文件
    /// </summary>
    public static void DeleteCacheDatabase()
    {
        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Prophet",
                "kline_cache.db"
            );
            
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
                Console.WriteLine($"✅ 已删除缓存数据库: {dbPath}");
            }
            else
            {
                Console.WriteLine($"ℹ️  缓存数据库不存在: {dbPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 删除缓存数据库失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 查看缓存统计
    /// </summary>
    public static async Task ShowCacheStatsAsync(string symbol, string interval)
    {
        try
        {
            var cache = new KlineCache();
            var (earliest, latest, count) = await cache.GetCacheStatsAsync(symbol, interval);
            
            Console.WriteLine($"\n【缓存统计】 {symbol} {interval}");
            Console.WriteLine($"  数量: {count} 条");
            
            if (earliest.HasValue && latest.HasValue)
            {
                Console.WriteLine($"  最早: {earliest:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  最新: {latest:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  跨度: {(latest.Value - earliest.Value).TotalDays:F1} 天");
            }
            else
            {
                Console.WriteLine($"  无缓存数据");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 获取缓存统计失败: {ex.Message}");
        }
    }
}


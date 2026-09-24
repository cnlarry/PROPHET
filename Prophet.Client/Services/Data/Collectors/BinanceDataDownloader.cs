using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;

namespace Prophet.Client.Services.Data.Collectors;

/// <summary>
/// 币安期货数据下载器
/// 从 data.binance.vision 下载期货历史K线数据（按日或按月）
/// 支持 USDT-M 期货合约（BTCUSDT、ETHUSDT、SOLUSDT等）
/// </summary>
public class BinanceDataDownloader
{
    private readonly HttpClient _httpClient;
    private readonly MarketDataRepository _repository;
    
    // 币安期货数据（USDT-M合约）下载地址
    private const string BINANCE_DATA_BASE_URL = "https://data.binance.vision/data/futures/um";
    
    public BinanceDataDownloader(HttpClient httpClient, MarketDataRepository repository)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    /// <summary>
    /// 下载指定日期的K线数据
    /// </summary>
    public async Task<int> DownloadDailyDataAsync(string symbol, string interval, DateTime date)
    {
        try
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            var url = $"{BINANCE_DATA_BASE_URL}/daily/klines/{symbol}/{interval}/{symbol}-{interval}-{dateStr}.zip";
            
            Console.WriteLine($"📥 [BinanceDataDownloader] 下载日数据: {symbol} {interval} {dateStr}");
            Console.WriteLine($"   🔗 URL: {url}");
            
            // 检查是否是未来日期（未来日期肯定没有数据）
            if (date.Date > DateTime.UtcNow.Date)
            {
                Console.WriteLine($"   ⚠️ [BinanceDataDownloader] 跳过未来日期: {dateStr}");
                return 0;
            }
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"   ⚠️ [BinanceDataDownloader] 文件不存在 (404): {dateStr} - 该日期可能没有数据文件或尚未生成");
                }
                else
                {
                    Console.WriteLine($"   ⚠️ [BinanceDataDownloader] 下载失败: HTTP {response.StatusCode} - {url}");
                }
                return 0;
            }
            
            // 下载ZIP文件
            var zipBytes = await response.Content.ReadAsByteArrayAsync();
            
            // 解压并解析CSV数据
            var candles = await ParseZipDataAsync(zipBytes, symbol, interval);
            
            if (candles.Count > 0)
            {
                // 增量更新到数据库
                await _repository.BulkInsertKlinesAsync(symbol, interval, candles);
                Console.WriteLine($"✅ [BinanceDataDownloader] 日数据下载完成: {candles.Count} 条");
            }
            
            return candles.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceDataDownloader] 下载日数据失败: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// 下载指定月份的K线数据
    /// </summary>
    public async Task<int> DownloadMonthlyDataAsync(string symbol, string interval, DateTime month)
    {
        try
        {
            var monthStr = month.ToString("yyyy-MM");
            var url = $"{BINANCE_DATA_BASE_URL}/monthly/klines/{symbol}/{interval}/{symbol}-{interval}-{monthStr}.zip";
            
            Console.WriteLine($"📥 [BinanceDataDownloader] 下载月数据: {symbol} {interval} {monthStr}");
            Console.WriteLine($"   🔗 URL: {url}");
            
            // 检查是否是未来月份（未来月份肯定没有数据）
            var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var requestMonth = new DateTime(month.Year, month.Month, 1);
            if (requestMonth > currentMonth)
            {
                Console.WriteLine($"   ⚠️ [BinanceDataDownloader] 跳过未来月份: {monthStr}");
                return 0;
            }
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"   ⚠️ [BinanceDataDownloader] 文件不存在 (404): {monthStr} - 该月份可能没有数据文件或尚未生成");
                }
                else
                {
                    Console.WriteLine($"   ⚠️ [BinanceDataDownloader] 下载失败: HTTP {response.StatusCode} - {url}");
                }
                return 0;
            }
            
            // 下载ZIP文件
            var zipBytes = await response.Content.ReadAsByteArrayAsync();
            
            // 解压并解析CSV数据
            var candles = await ParseZipDataAsync(zipBytes, symbol, interval);
            
            if (candles.Count > 0)
            {
                // 增量更新到数据库
                await _repository.BulkInsertKlinesAsync(symbol, interval, candles);
                Console.WriteLine($"✅ [BinanceDataDownloader] 月数据下载完成: {candles.Count} 条");
            }
            
            return candles.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceDataDownloader] 下载月数据失败: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// 批量下载日期范围的数据（按日下载）
    /// </summary>
    public async Task<int> DownloadDateRangeAsync(string symbol, string interval, DateTime startDate, DateTime endDate)
    {
        var totalCount = 0;
        var currentDate = startDate.Date;
        var end = endDate.Date;
        
        while (currentDate <= end)
        {
            var count = await DownloadDailyDataAsync(symbol, interval, currentDate);
            totalCount += count;
            
            // 避免请求过快
            await Task.Delay(500);
            
            currentDate = currentDate.AddDays(1);
        }
        
        return totalCount;
    }
    
    /// <summary>
    /// 批量下载月份范围的数据（按月下载）
    /// </summary>
    public async Task<int> DownloadMonthRangeAsync(string symbol, string interval, DateTime startMonth, DateTime endMonth)
    {
        var totalCount = 0;
        var currentMonth = new DateTime(startMonth.Year, startMonth.Month, 1);
        var end = new DateTime(endMonth.Year, endMonth.Month, 1);
        
        while (currentMonth <= end)
        {
            var count = await DownloadMonthlyDataAsync(symbol, interval, currentMonth);
            totalCount += count;
            
            // 避免请求过快
            await Task.Delay(1000);
            
            currentMonth = currentMonth.AddMonths(1);
        }
        
        return totalCount;
    }
    
    /// <summary>
    /// 解析ZIP文件中的CSV数据
    /// CSV格式：开盘时间,开盘价,最高价,最低价,收盘价,成交量,收盘时间,成交额,交易笔数,主动买入成交量,主动买入成交额,忽略字段
    /// </summary>
    private async Task<List<Candlestick>> ParseZipDataAsync(byte[] zipBytes, string symbol, string interval)
    {
        var candles = new List<Candlestick>();
        
        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            
            // ZIP文件中应该只有一个CSV文件
            var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv"));
            if (entry == null)
            {
                Console.WriteLine($"⚠️ [BinanceDataDownloader] ZIP文件中未找到CSV文件");
                return candles;
            }
            
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            
            bool isFirstLine = true;
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                // 跳过CSV文件的第一行（表头）
                if (isFirstLine)
                {
                    isFirstLine = false;
                    // 检查是否是表头（包含列名如 open_time）
                    if (line.Contains("open_time", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                
                var candle = ParseCsvLine(line);
                if (candle != null)
                {
                    candles.Add(candle);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceDataDownloader] 解析ZIP数据失败: {ex.Message}");
        }
        
        return candles;
    }
    
    /// <summary>
    /// 解析CSV行数据
    /// </summary>
    private Candlestick? ParseCsvLine(string line)
    {
        try
        {
            var parts = line.Split(',');
            if (parts.Length < 6)
                return null;
            
            // CSV格式：开盘时间(毫秒),开盘价,最高价,最低价,收盘价,成交量,收盘时间,成交额,交易笔数,主动买入成交量,主动买入成交额,忽略字段
            var openTimeMs = long.Parse(parts[0]);
            var openTime = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime;
            
            return new Candlestick
            {
                Time = openTime,
                Open = double.Parse(parts[1]),
                High = double.Parse(parts[2]),
                Low = double.Parse(parts[3]),
                Close = double.Parse(parts[4]),
                Volume = double.Parse(parts[5])
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [BinanceDataDownloader] 解析CSV行失败: {ex.Message}");
            return null;
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Data.Collectors;

/// <summary>
/// 币安期货K线数据采集器
/// 智能采集策略：
/// 1. 检查数据库中最新数据的时间
/// 2. 如果超过1天，使用 data.binance.vision 按日下载
/// 3. 如果超过1个月（跨月），按月下载
/// 4. 只有近期数据（1天内）才通过API获取
/// 支持 USDT-M 期货合约（BTCUSDT、ETHUSDT、SOLUSDT等）
/// </summary>
public class BinanceKlineCollector : BaseDataCollector<List<Candlestick>>
{
    private readonly IBinanceExchangeGateway _gateway;
    private readonly MarketDataRepository _repository;
    private readonly BinanceDataDownloader _dataDownloader;
    private readonly string _symbol;
    private readonly string _interval;
    private readonly int _limit;
    
    public BinanceKlineCollector(
        IBinanceExchangeGateway gateway,
        MarketDataRepository repository,
        string symbol,
        string interval = "1m",
        int limit = 1000,
        int intervalSeconds = 60)
        : base($"BinanceKline-{symbol}-{interval}", intervalSeconds)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _dataDownloader = new BinanceDataDownloader(gateway.HttpClient, repository);
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        _interval = interval ?? throw new ArgumentNullException(nameof(interval));
        _limit = limit;
    }
    
    /// <summary>
    /// 启动定时采集（重写以添加数据连续性检查）
    /// </summary>
    public new async Task StartAsync()
    {
        if (IsRunning)
        {
            Console.WriteLine($"⚠️ [{Name}] 采集器已在运行");
            return;
        }
        
        if (!IsEnabled)
        {
            Console.WriteLine($"⚠️ [{Name}] 采集器已禁用，无法启动");
            return;
        }
        
        // 🔑 启动前检查数据连续性并补齐缺失
        await CheckAndFillDataGapsAsync();
        
        // 调用基类的StartAsync
        await base.StartAsync();
    }
    
    /// <summary>
    /// 检查并补齐数据缺失
    /// </summary>
    private async Task CheckAndFillDataGapsAsync()
    {
        try
        {
            Console.WriteLine($"🔍 [{Name}] 检查数据连续性...");
            
            // 获取数据时间范围
            var (startTime, endTime) = await _repository.GetKlineTimeRangeAsync(_symbol, _interval);
            
            if (!startTime.HasValue || !endTime.HasValue)
            {
                Console.WriteLine($"📊 [{Name}] 数据库中没有数据，跳过连续性检查");
                return;
            }
            
            Console.WriteLine($"📊 [{Name}] 数据时间范围: {startTime.Value:yyyy-MM-dd HH:mm:ss} -> {endTime.Value:yyyy-MM-dd HH:mm:ss}");
            
            // 检查数据连续性
            var gaps = await _repository.CheckDataContinuityAsync(_symbol, _interval);
            
            if (gaps.Count == 0)
            {
                Console.WriteLine($"✅ [{Name}] 数据连续性检查通过，无缺失数据");
                return;
            }
            
            Console.WriteLine($"⚠️ [{Name}] 发现 {gaps.Count} 个数据缺失时间段");
            
            // 统计缺失的时间范围
            var totalMissingMinutes = 0;
            foreach (var (start, end) in gaps)
            {
                var minutes = (int)(end - start).TotalMinutes;
                totalMissingMinutes += minutes;
                Console.WriteLine($"   📅 缺失: {start:yyyy-MM-dd HH:mm:ss} -> {end:yyyy-MM-dd HH:mm:ss} ({minutes} 分钟)");
            }
            
            Console.WriteLine($"📥 [{Name}] 开始补齐缺失数据（共 {totalMissingMinutes} 分钟）...");
            
            // 补齐缺失的数据
            var filledCount = 0;
            foreach (var (start, end) in gaps)
            {
                var filled = await FillDataGapAsync(start, end);
                filledCount += filled;
            }
            
            Console.WriteLine($"✅ [{Name}] 数据补齐完成，共补齐 {filledCount} 条数据");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [{Name}] 数据连续性检查失败: {ex.Message}");
            // 不抛出异常，允许继续启动
        }
    }
    
    /// <summary>
    /// 补齐指定时间段的数据
    /// </summary>
    private async Task<int> FillDataGapAsync(DateTime start, DateTime end)
    {
        var now = DateTime.UtcNow;
        var timeDiff = (now - start).TotalDays;
        
        // 如果缺失超过1个月，按月下载
        if (timeDiff > 30)
        {
            var startMonth = new DateTime(start.Year, start.Month, 1);
            var endMonth = new DateTime(end.Year, end.Month, 1);
            
            // 下载整个月份的数据（会自动跳过已存在的数据）
            return await _dataDownloader.DownloadMonthRangeAsync(_symbol, _interval, startMonth, endMonth);
        }
        // 如果缺失超过1天，按日下载
        else if (timeDiff > 1)
        {
            var startDate = start.Date;
            var endDate = end.Date;
            
            // 下载日期范围的数据（会自动跳过已存在的数据）
            return await _dataDownloader.DownloadDateRangeAsync(_symbol, _interval, startDate, endDate);
        }
        // 如果缺失在1天内，尝试使用API获取（但通常API只能获取最近的数据）
        else
        {
            // 对于1分钟内的缺失，暂时跳过（因为API限制，无法获取历史1分钟数据）
            Console.WriteLine($"   ⚠️ 缺失时间段在1天内，无法通过API补齐，将在下次采集时自动更新");
            return 0;
        }
    }
    
    /// <summary>
    /// 执行K线数据采集（智能策略）
    /// 策略：
    /// 1. 大规模数据缺失（超过1天）：使用Download下载
    /// 2. 短期数据缺失（1天内）：使用REST API补充
    /// 注意：此采集器主要用于补充短期缺失数据，实时数据应由WebSocket采集器处理
    /// </summary>
    protected override async Task<CollectResult<List<Candlestick>>> DoCollectAsync()
    {
        try
        {
            // 🔑 步骤1: 检查数据库中最新数据的时间
            var (_, latestTime) = await _repository.GetKlineTimeRangeAsync(_symbol, _interval);
            
            if (latestTime.HasValue)
            {
                var now = DateTime.UtcNow;
                var timeDiff = now - latestTime.Value;
                
                Console.WriteLine($"📊 [BinanceKlineCollector] 数据库最新数据: {latestTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"📊 [BinanceKlineCollector] 时间差距: {timeDiff.TotalDays:F2} 天");
                
                // 🔑 步骤2: 如果超过1个月（跨月），按月下载
                if (timeDiff.TotalDays > 30 || latestTime.Value.Month != now.Month || latestTime.Value.Year != now.Year)
                {
                    Console.WriteLine($"📥 [BinanceKlineCollector] 数据缺失超过1个月，使用按月下载策略");
                    await DownloadMissingMonthlyDataAsync(latestTime.Value, now);
                    // 下载完成后，返回空结果（不继续使用REST API，因为WebSocket会处理实时数据）
                    return new CollectResult<List<Candlestick>>
                    {
                        Success = true,
                        Data = new List<Candlestick>(),
                        DataCount = 0,
                        CollectTime = DateTime.UtcNow
                    };
                }
                // 🔑 步骤3: 如果超过1天，按日下载
                else if (timeDiff.TotalDays > 1)
                {
                    Console.WriteLine($"📥 [BinanceKlineCollector] 数据缺失超过1天，使用按日下载策略");
                    await DownloadMissingDailyDataAsync(latestTime.Value, now);
                    // 下载完成后，返回空结果（不继续使用REST API，因为WebSocket会处理实时数据）
                    return new CollectResult<List<Candlestick>>
                    {
                        Success = true,
                        Data = new List<Candlestick>(),
                        DataCount = 0,
                        CollectTime = DateTime.UtcNow
                    };
                }
                // 🔑 步骤4: 如果缺失在1天内，使用REST API补充
                else if (timeDiff.TotalMinutes > 5) // 如果缺失超过5分钟，使用REST API补充
                {
                    Console.WriteLine($"📥 [BinanceKlineCollector] 数据缺失在1天内（{timeDiff.TotalMinutes:F0}分钟），使用REST API补充");
                    return await CollectLatestDataViaApiAsync();
                }
                else
                {
                    // 数据很新（缺失少于5分钟），不需要补充
                    Console.WriteLine($"✅ [BinanceKlineCollector] 数据很新，无需补充");
                    return new CollectResult<List<Candlestick>>
                    {
                        Success = true,
                        Data = new List<Candlestick>(),
                        DataCount = 0,
                        CollectTime = DateTime.UtcNow
                    };
                }
            }
            else
            {
                Console.WriteLine($"📊 [BinanceKlineCollector] 数据库中没有数据，使用REST API获取初始数据");
                // 首次采集，使用REST API获取数据
                return await CollectLatestDataViaApiAsync();
            }
        }
        catch (Exception ex)
        {
            return new CollectResult<List<Candlestick>>
            {
                Success = false,
                ErrorMessage = $"采集失败: {ex.Message}",
                CollectTime = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// 下载缺失的月数据
    /// </summary>
    private async Task DownloadMissingMonthlyDataAsync(DateTime latestTime, DateTime now)
    {
        var startMonth = new DateTime(latestTime.Year, latestTime.Month, 1);
        var endMonth = new DateTime(now.Year, now.Month, 1);
        
        // 从下一个月开始下载（因为最新数据可能不完整）
        startMonth = startMonth.AddMonths(1);
        
        if (startMonth <= endMonth)
        {
            Console.WriteLine($"📥 [BinanceKlineCollector] 下载月份范围: {startMonth:yyyy-MM} 到 {endMonth:yyyy-MM}");
            var count = await _dataDownloader.DownloadMonthRangeAsync(_symbol, _interval, startMonth, endMonth);
            Console.WriteLine($"✅ [BinanceKlineCollector] 月数据下载完成，共 {count} 条");
        }
    }
    
    /// <summary>
    /// 下载缺失的日数据
    /// </summary>
    private async Task DownloadMissingDailyDataAsync(DateTime latestTime, DateTime now)
    {
        var startDate = latestTime.Date.AddDays(1); // 从下一天开始
        var endDate = now.Date.AddDays(-1); // 到昨天为止（今天的数据通过API获取）
        
        if (startDate <= endDate)
        {
            Console.WriteLine($"📥 [BinanceKlineCollector] 下载日期范围: {startDate:yyyy-MM-dd} 到 {endDate:yyyy-MM-dd}");
            var count = await _dataDownloader.DownloadDateRangeAsync(_symbol, _interval, startDate, endDate);
            Console.WriteLine($"✅ [BinanceKlineCollector] 日数据下载完成，共 {count} 条");
        }
    }
    
    /// <summary>
    /// 通过API获取最新数据（1天内的数据）
    /// </summary>
    private async Task<CollectResult<List<Candlestick>>> CollectLatestDataViaApiAsync()
    {
        try
        {
            var candles = await _gateway.FetchKlinesAsync(
                _symbol,
                _interval,
                _limit);
            
            if (candles.Count == 0)
            {
                return new CollectResult<List<Candlestick>>
                {
                    Success = false,
                    ErrorMessage = "API返回空数据",
                    CollectTime = DateTime.UtcNow
                };
            }
            
            return new CollectResult<List<Candlestick>>
            {
                Success = true,
                Data = candles.ToList(),
                DataCount = candles.Count,
                CollectTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new CollectResult<List<Candlestick>>
            {
                Success = false,
                ErrorMessage = $"采集失败: {ex.Message}",
                CollectTime = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// 保存K线数据到数据库
    /// </summary>
    protected override async Task SaveDataAsync(List<Candlestick> data)
    {
        if (data == null || data.Count == 0)
            return;
        
        // 使用MarketDataRepository的批量插入方法
        await _repository.BulkInsertKlinesAsync(_symbol, _interval, data);
    }
}

